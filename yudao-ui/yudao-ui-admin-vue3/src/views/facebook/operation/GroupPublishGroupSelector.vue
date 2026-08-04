<template>
  <Dialog
    v-model="visible"
    title="选择群组"
    width="900px"
    :close-on-click-modal="false"
    :fullscreen="true"
  >
    <div v-loading="loading">
      <!-- 搜索栏 -->
      <el-form :inline="true" class="mb-10px">
        <el-form-item label="群组名称">
          <el-input
            v-model="searchKeyword"
            placeholder="请输入群组名称"
            clearable
            @keyup.enter="loadGroups"
          />
        </el-form-item>
        <el-form-item label="群组分组">
          <ResourceGroupControl v-model="queryParams.resourceGroupId" resource-type="GROUP" title="群组分组" @change="loadGroups" />
        </el-form-item>
        <el-form-item label="加组时间">
          <el-select v-model="queryParams.joinedBeforeDays" class="!w-140px" @change="loadGroups">
            <el-option label="不限" :value="0" />
            <el-option label="超过三天" :value="3" />
            <el-option label="超过七天" :value="7" />
          </el-select>
        </el-form-item>
        <el-form-item>
          <el-button type="primary" @click="loadGroups">
            <Icon icon="ep:search" class="mr-5px" /> 搜索
          </el-button>
          <el-button @click="resetSearch">重置</el-button>
        </el-form-item>
      </el-form>

      <!-- 群组列表 -->
      <el-table
        ref="tableRef"
        :data="displayRows"
        row-key="rowKey"
        stripe
        border
        max-height="400"
        :span-method="accountSpanMethod"
        @selection-change="handleSelectionChange"
      >
        <el-table-column type="selection" width="55" />
        <el-table-column label="账号" prop="accountLabel" width="180" show-overflow-tooltip />
        <el-table-column label="群组名称" prop="groupName" min-width="180" show-overflow-tooltip />
        <el-table-column label="群组链接" prop="groupUrl" min-width="220" show-overflow-tooltip />
        <el-table-column label="加组时间" prop="joinTime" width="160" />
        <el-table-column label="发帖情况" width="130">
          <template #default="scope">
            <el-tag v-if="!scope.row.publishCount" type="success">未发过</el-tag>
            <span v-else>已发 {{ scope.row.publishCount }} 次</span>
          </template>
        </el-table-column>
        <el-table-column label="最近发帖" prop="lastPublishTime" width="160" />
      </el-table>
      <el-empty v-if="displayRows.length === 0" description="没有符合条件的已加入群组" :image-size="70" />

      <!-- 分页 -->
      <div class="group-selector-pagination">
        <Pagination
          :total="total"
          v-model:page="queryParams.pageNo"
          v-model:limit="queryParams.pageSize"
          @pagination="loadGroups"
        />
      </div>
    </div>

    <template #footer>
      <el-button type="primary" @click="handleConfirm" :disabled="selectedRows.length === 0">
        确 定 ({{ selectedRows.length }})
      </el-button>
      <el-button @click="visible = false">取 消</el-button>
    </template>
  </Dialog>
</template>

<script setup lang="ts">
import { ref, watch, computed } from 'vue'
import { FbOperationAddGroupResultApi } from '@/api/facebook/operation/addgroupresult'
import ResourceGroupControl from '../resource/components/ResourceGroupControl.vue'
import { Dialog } from '@/components/Dialog'

const props = defineProps<{
  modelValue: boolean
  selectedGroupIds?: string[]
  accountIds?: Array<string | number>
  groupsPerAccount?: number
  resourceGroupId?: number
  joinedBeforeDays?: number
  expectedAccountCount?: number
  accountSelectionMode?: 'AUTO' | 'MANUAL'
  targetAccountCount?: number
  actionType?: 'group_post' | 'repost'
}>()

const emit = defineEmits(['update:modelValue', 'confirm'])
const message = useMessage()

const visible = ref(false)
const loading = ref(false)
const tableRef = ref()
const searchKeyword = ref('')
const selectedRows = ref<any[]>([])

// 查询参数
const queryParams = ref({
  pageNo: 1,
  pageSize: 20,
  joinStatus: 1, // 只查询成功的记录
  groupName: '',
  accountIds: [] as string[],
  resourceGroupId: undefined as number | undefined,
  joinedBeforeDays: 3 as number | undefined
})

const groupList = ref<any[]>([])
const total = ref(0)
const groupedGroups = computed(() => {
  const groups = new Map<string, any[]>()
  groupList.value.forEach((row) => {
    const accountId = String(row.accountId || row.fbAccount || '未知账号')
    if (!groups.has(accountId)) groups.set(accountId, [])
    groups.get(accountId)!.push({ ...row, rowKey: `${accountId}-${row.groupId}` })
  })
  const entries = Array.from(groups.entries())
  const visibleEntries = props.expectedAccountCount && props.expectedAccountCount < entries.length
    ? entries.slice(0, props.expectedAccountCount)
    : entries
  return visibleEntries.map(([accountId, rows]) => ({ accountId, rows }))
})
const displayRows = computed(() => groupedGroups.value.flatMap((account) =>
  account.rows.map((row, index) => ({
    ...row,
    accountLabel: `${row.fbAccount || '未识别账号'}（${account.rows.length}个已加入群组）`,
    accountFirst: index === 0,
    accountRowspan: index === 0 ? account.rows.length : 0
  }))
))

// 监听modelValue变化
watch(() => props.modelValue, (val) => {
  visible.value = val
  if (val) {
    queryParams.value.resourceGroupId = props.resourceGroupId
    queryParams.value.joinedBeforeDays = props.joinedBeforeDays ?? 3
    loadGroups()
  }
})

// 监听visible变化
watch(visible, (val) => {
  emit('update:modelValue', val)
})

/** 加载群组列表 */
const loadGroups = async () => {
  loading.value = true
  try {
    queryParams.value.groupName = searchKeyword.value
    let accountIds = (props.accountIds || []).map(String)
    if (props.accountSelectionMode === 'AUTO') {
      if (!props.targetAccountCount || props.targetAccountCount <= 0) {
        groupList.value = []
        total.value = 0
        return
      }
      const accountResult = await FbOperationAddGroupResultApi.getSelectorAccounts({
        accountSelectionMode: 'AUTO',
        targetAccountCount: props.targetAccountCount,
        minGroupCount: props.groupsPerAccount || 1,
        joinedBeforeDays: queryParams.value.joinedBeforeDays,
        resourceGroupId: queryParams.value.resourceGroupId,
        groupName: queryParams.value.groupName,
        actionType: props.actionType || 'group_post'
      })
      accountIds = (accountResult || []).map(String)
      if (accountIds.length === 0) {
        groupList.value = []
        total.value = 0
        message.warning('没有符合条件的可执行账号，请检查账号已加入群组数量、加组时间或每日额度')
        return
      }
      if (accountIds.length < props.targetAccountCount) {
        message.warning(`当前仅找到 ${accountIds.length} 个符合条件的账号，将按实际账号数执行`)
      }
    }
    if (accountIds.length === 0) {
      groupList.value = []
      total.value = 0
      return
    }
    queryParams.value.accountIds = accountIds
    
    const data = await FbOperationAddGroupResultApi.getAddGroupResultPage(queryParams.value)
    groupList.value = data.list || []
    total.value = data.total || 0
    selectedRows.value = []

    // 默认优先选择该账号未发过、且数量不超过每个账号发帖数的群组。
    if (props.selectedGroupIds && props.selectedGroupIds.length > 0) {
      setTimeout(() => {
        groupList.value.forEach(row => {
          if (props.selectedGroupIds?.includes(row.groupId)) {
            const tableRow = displayRows.value.find((item) => item.accountId === String(row.accountId) && item.groupId === row.groupId)
            if (tableRow) tableRef.value?.toggleRowSelection(tableRow, true)
          }
        })
      }, 100)
    } else {
      setTimeout(() => {
        groupedGroups.value.forEach((account) => {
          account.rows.slice(0, props.groupsPerAccount || 5).forEach((row) => {
            const tableRow = displayRows.value.find((item) => item.rowKey === row.rowKey)
            if (tableRow) tableRef.value?.toggleRowSelection(tableRow, true)
          })
        })
      }, 100)
    }
  } catch (error) {
    console.error('加载群组列表失败:', error)
  } finally {
    loading.value = false
  }
}

/** 重置搜索 */
const resetSearch = () => {
  searchKeyword.value = ''
  queryParams.value.pageNo = 1
  loadGroups()
}

/** 处理选择变化 */
const handleSelectionChange = (rows: any[]) => {
  selectedRows.value = rows
}

const accountSpanMethod = ({ row, column }: any) => {
  if (column.property === 'accountLabel') {
    return row.accountFirst ? [row.accountRowspan, 1] : [0, 0]
  }
  return [1, 1]
}

/** 确认选择 */
const handleConfirm = () => {
  if (selectedRows.value.length === 0) {
    return
  }
  emit('confirm', selectedRows.value)
  visible.value = false
}
</script>

<style scoped lang="scss">
.text-sm {
  font-size: 12px;
}
.text-gray-700 {
  color: #374151;
}
.font-medium {
  font-weight: 500;
}
.mb-5px {
  margin-bottom: 5px;
}
.mt-5px {
  margin-top: 5px;
}
.mt-10px {
  margin-top: 10px;
}
.mb-10px {
  margin-bottom: 10px;
}
.p-10px {
  padding: 10px;
}
.bg-blue-50 {
  background-color: #eff6ff;
}
.bg-green-50 {
  background-color: #f0fdf4;
}
.rounded {
  border-radius: 4px;
}
.account-section {
  margin-bottom: 12px;
  border: 1px solid var(--el-border-color-lighter);
}
.account-section-title {
  display: flex;
  justify-content: space-between;
  padding: 8px 12px;
  background: var(--el-fill-color-light);
  font-size: 13px;
  font-weight: 600;
}
.group-selector-pagination {
  display: flow-root;
  min-height: 46px;
}
</style>
