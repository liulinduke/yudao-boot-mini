<template>
  <el-dialog
    v-model="visible"
    title="选择群组"
    width="800px"
    :close-on-click-modal="false"
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
        <el-form-item>
          <el-button type="primary" @click="loadGroups">搜索</el-button>
        </el-form-item>
      </el-form>

      <!-- 已选账号提示 -->
      <div v-if="accountIds.length > 0" class="mb-10px p-10px bg-blue-50 rounded">
        <div class="text-sm text-gray-700">
          <span class="font-medium">已选账号：</span>
          {{ accountIds.length }} 个
        </div>
      </div>

      <!-- 群组列表 -->
      <el-table
        ref="tableRef"
        :data="groupList"
        stripe
        border
        max-height="400"
        @selection-change="handleSelectionChange"
      >
        <el-table-column type="selection" width="55" />
        <el-table-column label="群组ID" prop="groupId" width="150" />
        <el-table-column label="群组名称" prop="groupName" min-width="200" show-overflow-tooltip />
        <el-table-column label="群组链接" prop="groupUrl" min-width="250" show-overflow-tooltip />
        <el-table-column label="加组状态" width="100">
          <template #default="scope">
            <el-tag v-if="scope.row.joinStatus === 1" type="success">成功</el-tag>
            <el-tag v-else-if="scope.row.joinStatus === 3" type="warning">已加入</el-tag>
            <el-tag v-else type="info">未知</el-tag>
          </template>
        </el-table-column>
      </el-table>

      <!-- 分页 -->
      <Pagination
        :total="total"
        v-model:page="queryParams.pageNo"
        v-model:limit="queryParams.pageSize"
        @pagination="loadGroups"
      />
    </div>

    <template #footer>
      <el-button type="primary" @click="handleConfirm">确 定</el-button>
      <el-button @click="visible = false">取 消</el-button>
    </template>
  </el-dialog>
</template>

<script setup lang="ts">
import { ref, watch } from 'vue'
import { FbOperationAddGroupResultApi } from '@/api/facebook/operation/addgroupresult'

const props = defineProps<{
  modelValue: boolean
  selectedGroupIds?: string[]
  accountIds?: string[]
}>()

const emit = defineEmits(['update:modelValue', 'confirm'])

const visible = ref(false)
const loading = ref(false)
const tableRef = ref()
const searchKeyword = ref('')
const selectedRows = ref<any[]>([])

// 查询参数
const queryParams = ref({
  pageNo: 1,
  pageSize: 10,
  joinStatus: 1 // 只查询成功的记录
})

const groupList = ref<any[]>([])
const total = ref(0)

// 监听modelValue变化
watch(() => props.modelValue, (val) => {
  visible.value = val
  if (val) {
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
    const data = await FbOperationAddGroupResultApi.getAddGroupResultPage(queryParams.value)
    groupList.value = data.list || []
    total.value = data.total || 0

    // 设置默认选中
    if (props.selectedGroupIds && props.selectedGroupIds.length > 0) {
      setTimeout(() => {
        groupList.value.forEach(row => {
          if (props.selectedGroupIds?.includes(row.groupId)) {
            tableRef.value?.toggleRowSelection(row, true)
          }
        })
      }, 100)
    }
  } catch (error) {
    console.error('加载群组列表失败:', error)
  } finally {
    loading.value = false
  }
}

/** 处理选择变化 */
const handleSelectionChange = (rows: any[]) => {
  selectedRows.value = rows
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
