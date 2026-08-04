<template>
  <Dialog v-model="visible" title="选择帖子" width="80%">
    <div v-loading="loading">
      <!-- 查询条件 -->
      <el-form :inline="true" :model="queryParams" class="mb-4">
        <el-form-item label="帖子内容">
          <el-input
            v-model="queryParams.postContent"
            placeholder="请输入帖子内容关键词"
            clearable
            @keyup.enter="loadList"
          />
        </el-form-item>
        <el-form-item label="发帖人">
          <el-input
            v-model="queryParams.postUser"
            placeholder="请输入发帖人"
            clearable
            @keyup.enter="loadList"
          />
        </el-form-item>
        <el-form-item>
          <el-button type="primary" @click="loadList">查询</el-button>
          <el-button @click="resetQuery">重置</el-button>
        </el-form-item>
      </el-form>

      <!-- 帖子列表表格 -->
      <el-table
        :data="list"
        stripe
        border
        max-height="400"
        @selection-change="handleSelectionChange"
      >
        <el-table-column type="selection" width="55" />
        <!-- <el-table-column label="帖子ID" prop="id" width="100" /> -->
        <el-table-column label="发帖人" prop="postUser" width="150" show-overflow-tooltip />
        <el-table-column label="帖子链接" prop="url" min-width="250" show-overflow-tooltip />
        <el-table-column
          label="帖子内容"
          prop="postContent"
          min-width="300"
          show-overflow-tooltip
        />
        <el-table-column label="点赞数" prop="reactionCount" width="100" />
        <el-table-column label="评论数" prop="commentCount" width="100" />
        <el-table-column label="转发数" prop="reshareCount" width="100" />
        <el-table-column label="来源" prop="fromResource" width="120" />
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
import { FbCollectPostApi, FbCollectPost } from '@/api/facebook/fbcollectpost'
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
  confirm: [selectedPosts: FbCollectPost[]]
}>()

// 状态
const visible = ref(false)
const loading = ref(false)
const list = ref<FbCollectPost[]>([])
const total = ref(0)
const pageNo = ref(1)
const pageSize = ref(20)
const selectedPosts = ref<FbCollectPost[]>([])

// 查询参数
const queryParams = reactive({
  postContent: '',
  postUser: ''
})

// 监听 modelValue 变化
watch(
  () => props.modelValue,
  (val) => {
    visible.value = val
    if (val) {
      // 打开时重置并加载数据
      pageNo.value = 1
      loadList()
    }
  }
)

// 监听 visible 变化
watch(visible, (val) => {
  emit('update:modelValue', val)
})

/** 加载帖子列表 */
const loadList = async () => {
  loading.value = true
  try {
    const params: any = {
      pageNo: pageNo.value,
      pageSize: pageSize.value
    }

    // 添加查询条件
    if (queryParams.postContent) {
      params.postContent = queryParams.postContent
    }
    if (queryParams.postUser) {
      params.postUser = queryParams.postUser
    }

    const response = await FbCollectPostApi.getFbCollectPostPage(params)
    list.value = response.list || []
    total.value = response.total || 0
  } catch (error) {
    console.error('加载帖子列表失败:', error)
    message.error('加载帖子列表失败')
  } finally {
    loading.value = false
  }
}

/** 重置查询 */
const resetQuery = () => {
  queryParams.postContent = ''
  queryParams.postUser = ''
  pageNo.value = 1
  loadList()
}

/** 处理选择变化 */
const handleSelectionChange = (selection: FbCollectPost[]) => {
  selectedPosts.value = selection
}

/** 确认选择 */
const handleConfirm = () => {
  if (selectedPosts.value.length === 0) {
    message.warning('请至少选择一个帖子')
    return
  }

  emit('confirm', selectedPosts.value)
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
