<template>
  <Dialog v-model="visible" title="选择群组" width="80%">
    <div v-loading="loading">
      <!-- 查询条件 -->
      <el-form :inline="true" :model="queryParams" class="mb-4">
        <el-form-item label="群组名称">
          <el-input
            v-model="queryParams.groupName"
            placeholder="请输入群组名称"
            clearable
            @keyup.enter="loadList"
          />
        </el-form-item>
        <el-form-item label="群组类型">
          <el-select v-model="queryParams.type" placeholder="请选择类型" clearable>
            <el-option label="公开" value="Publik" />
            <el-option label="私密" value="Private" />
          </el-select>
        </el-form-item>
        <el-form-item>
          <el-button type="primary" @click="loadList">查询</el-button>
          <el-button @click="resetQuery">重置</el-button>
        </el-form-item>
      </el-form>

      <!-- 群组列表表格 -->
      <el-table
        :data="list"
        stripe
        border
        max-height="400"
        @selection-change="handleSelectionChange"
      >
        <el-table-column type="selection" width="55" />
        <el-table-column label="群组ID" prop="id" width="100" />
        <el-table-column label="群组名称" prop="groupName" min-width="200" show-overflow-tooltip />
        <el-table-column label="群组链接" prop="url" min-width="250" show-overflow-tooltip />
        <el-table-column label="类型" prop="type" width="100">
          <template #default="scope">
            <el-tag :type="scope.row.type === 'Publik' ? 'success' : 'warning'">
              {{ scope.row.type }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column label="成员数" prop="memberQuantity" width="120" />
        <el-table-column label="活跃度" prop="activeQuantity" width="150" />
      </el-table>

      <!-- 分页 -->
      <el-pagination
        v-if="total > 0"
        class="mt-4"
        v-model:current-page="pageNo"
        v-model:page-size="pageSize"
        :total="total"
        :page-sizes="[10, 20, 50, 100]"
        layout="total, sizes, prev, pager, next, jumper"
        @size-change="loadList"
        @current-change="loadList"
      />
    </div>

    <template #footer>
      <el-button type="primary" @click="handleConfirm">确 定</el-button>
      <el-button @click="handleCancel">取 消</el-button>
    </template>
  </Dialog>
</template>

<script setup lang="ts">
import { ref, reactive, watch } from 'vue'
import { Dialog } from '@/components/Dialog'
import { FbCollectGroupApi, FbCollectGroup } from '@/api/facebook/fbcollectgroup'
import { useMessage } from '@/hooks/web/useMessage'

const message = useMessage()

// Props
interface Props {
  modelValue: boolean
  multiple?: boolean // 是否支持多选，默认true
}

const props = withDefaults(defineProps<Props>(), {
  modelValue: false,
  multiple: true
})

// Emits
const emit = defineEmits<{
  'update:modelValue': [value: boolean]
  'confirm': [selectedGroups: FbCollectGroup[]]
}>()

// 状态
const visible = ref(false)
const loading = ref(false)
const list = ref<FbCollectGroup[]>([])
const total = ref(0)
const pageNo = ref(1)
const pageSize = ref(20)
const selectedGroups = ref<FbCollectGroup[]>([])

// 查询参数
const queryParams = reactive({
  groupName: '',
  type: ''
})

// 监听 modelValue 变化
watch(() => props.modelValue, (val) => {
  visible.value = val
  if (val) {
    // 打开时重置并加载数据
    pageNo.value = 1
    loadList()
  }
})

// 监听 visible 变化
watch(visible, (val) => {
  emit('update:modelValue', val)
})

/** 加载群组列表 */
const loadList = async () => {
  loading.value = true
  try {
    const params: any = {
      pageNo: pageNo.value,
      pageSize: pageSize.value
    }
    
    // 添加查询条件
    if (queryParams.groupName) {
      params.groupName = queryParams.groupName
    }
    if (queryParams.type) {
      params.type = queryParams.type
    }
    
    const response = await FbCollectGroupApi.getFbCollectGroupPage(params)
    list.value = response.list || []
    total.value = response.total || 0
  } catch (error) {
    console.error('加载群组列表失败:', error)
    message.error('加载群组列表失败')
  } finally {
    loading.value = false
  }
}

/** 重置查询 */
const resetQuery = () => {
  queryParams.groupName = ''
  queryParams.type = ''
  pageNo.value = 1
  loadList()
}

/** 处理选择变化 */
const handleSelectionChange = (selection: FbCollectGroup[]) => {
  selectedGroups.value = selection
}

/** 确认选择 */
const handleConfirm = () => {
  if (selectedGroups.value.length === 0) {
    message.warning('请至少选择一个群组')
    return
  }
  
  emit('confirm', selectedGroups.value)
  visible.value = false
}

/** 取消选择 */
const handleCancel = () => {
  visible.value = false
}

/** 暴露方法供外部调用 */
defineExpose({
  loadList
})
</script>
