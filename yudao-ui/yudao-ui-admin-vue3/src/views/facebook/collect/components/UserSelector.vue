<template>
  <Dialog v-model="visible" title="选择用户" width="80%">
    <div v-loading="loading">
      <!-- 查询条件 -->
      <el-form :inline="true" :model="queryParams" class="mb-4">
        <el-form-item label="用户名">
          <el-input
            v-model="queryParams.userName"
            placeholder="请输入用户名"
            clearable
            @keyup.enter="loadList"
          />
        </el-form-item>
        <el-form-item label="所在地">
          <el-input
            v-model="queryParams.city"
            placeholder="请输入所在地"
            clearable
          />
        </el-form-item>
        <el-form-item>
          <el-button type="primary" @click="loadList">查询</el-button>
          <el-button @click="resetQuery">重置</el-button>
        </el-form-item>
      </el-form>

      <!-- 用户列表表格 -->
      <el-table
        :data="list"
        stripe
        border
        max-height="400"
        @selection-change="handleSelectionChange"
      >
        <el-table-column type="selection" width="55" />
        <el-table-column label="用户ID" prop="id" width="100" />
        <el-table-column label="用户名" prop="userName" min-width="150" show-overflow-tooltip />
        <el-table-column label="Facebook ID" prop="fbUserId" width="180" />
        <el-table-column label="主页链接" prop="url" min-width="250" show-overflow-tooltip />
        <el-table-column label="粉丝数" prop="followers" width="100" />
        <el-table-column label="所在地" prop="city" width="120" />
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
import { ref, reactive } from 'vue'
import { Dialog } from '@/components/Dialog'
import { FbCollectUserApi, FbCollectUser } from '@/api/facebook/collectuser'
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
  'confirm': [selectedUsers: FbCollectUser[]]
}>()

// 状态
const visible = ref(false)
const loading = ref(false)
const list = ref<FbCollectUser[]>([])
const total = ref(0)
const pageNo = ref(1)
const pageSize = ref(20)
const selectedUsers = ref<FbCollectUser[]>([])

// 查询参数
const queryParams = reactive({
  userName: '',
  city: ''
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

/** 加载用户列表 */
const loadList = async () => {
  loading.value = true
  try {
    const params: any = {
      pageNo: pageNo.value,
      pageSize: pageSize.value
    }
    
    // 添加查询条件
    if (queryParams.userName) {
      params.userName = queryParams.userName
    }
    if (queryParams.city) {
      params.city = queryParams.city
    }
    
    const response = await FbCollectUserApi.getFbCollectUserPage(params)
    list.value = response.list || []
    total.value = response.total || 0
  } catch (error) {
    console.error('加载用户列表失败:', error)
    message.error('加载用户列表失败')
  } finally {
    loading.value = false
  }
}

/** 重置查询 */
const resetQuery = () => {
  queryParams.userName = ''
  queryParams.city = ''
  pageNo.value = 1
  loadList()
}

/** 处理选择变化 */
const handleSelectionChange = (selection: FbCollectUser[]) => {
  selectedUsers.value = selection
}

/** 确认选择 */
const handleConfirm = () => {
  if (selectedUsers.value.length === 0) {
    message.warning('请至少选择一个用户')
    return
  }
  
  emit('confirm', selectedUsers.value)
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
