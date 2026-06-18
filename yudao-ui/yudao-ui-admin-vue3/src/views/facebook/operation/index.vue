<template>
  <div>
    <el-row :gutter="10">
      <el-col :span="8">
        <ContentWrap>
          <div class="operation-section">
            <h3 class="section-title">
              <el-icon :size="20"><Tools /></el-icon>
              <span class="ml-6px">运营工具</span>
            </h3>
            <div class="operation-grid">
              <OperationCard
                v-for="(tool, index) in operationTools"
                :key="index"
                :title="tool.title"
                :icon="tool.icon"
                :active="activeTool === tool.type"
                :disabled="tool.disabled"
                @click="selectTool(tool.type)"
              />
            </div>
          </div>
        </ContentWrap>
      </el-col>

      <el-col :span="16">
        <ContentWrap>
          <div class="task-section">
            <h3 class="section-title">
              <el-icon :size="20"><List /></el-icon>
              <span class="ml-6px">任务列表</span>
            </h3>
          </div>

          <el-form
            class="search-form"
            :model="queryParams"
            ref="queryFormRef"
            :inline="true"
            label-width="68px"
          >
            <el-form-item label="任务类型" prop="taskType">
              <el-select
                v-model="queryParams.taskType"
                placeholder="请选择任务类型"
                clearable
                class="!w-140px"
              >
                <el-option label="链接加组" :value="9" />
                <el-option label="转帖" :value="10" />
                <el-option label="帖子评论" :value="15" />
                <el-option label="群发私信" :value="14" />
                <el-option label="发个人帖" :value="12" />
                <el-option label="发群帖" :value="13" />
              </el-select>
            </el-form-item>
            <el-form-item label="状态" prop="status">
              <el-select
                v-model="queryParams.status"
                placeholder="请选择状态"
                clearable
                class="!w-140px"
              >
                <el-option label="待执行" :value="0" />
                <el-option label="执行中" :value="1" />
                <el-option label="已完成" :value="2" />
                <el-option label="已停止" :value="3" />
                <el-option label="失败" :value="4" />
              </el-select>
            </el-form-item>
            <el-form-item label="创建时间" prop="createTime">
              <el-date-picker
                v-model="queryParams.createTime"
                value-format="YYYY-MM-DD HH:mm:ss"
                type="daterange"
                start-placeholder="开始日期"
                end-placeholder="结束日期"
                :default-time="[new Date('1 00:00:00'), new Date('1 23:59:59')]"
                class="!w-220px"
              />
            </el-form-item>
            <el-form-item>
              <el-button @click="handleQuery">
                <Icon icon="ep:search" class="mr-5px" /> 搜索
              </el-button>
            </el-form-item>
          </el-form>

          <el-table
            row-key="id"
            v-loading="loading"
            :data="list"
            :stripe="true"
            :show-overflow-tooltip="true"
          >
            <el-table-column label="任务类型" align="center" prop="taskType" width="100">
              <template #default="scope">
                <el-tag v-if="scope.row.taskType === 9" type="primary">链接加组</el-tag>
                <el-tag v-else-if="scope.row.taskType === 10" type="success">转帖</el-tag>
                <el-tag v-else-if="scope.row.taskType === 15" type="warning">帖子评论</el-tag>
                <el-tag v-else-if="scope.row.taskType === 14" type="warning">群发私信</el-tag>
                <el-tag v-else-if="scope.row.taskType === 12" type="info">发个人帖</el-tag>
                <el-tag v-else-if="scope.row.taskType === 13" type="danger">发群帖</el-tag>
              </template>
            </el-table-column>
            <el-table-column label="期望/实际" align="center" width="120">
              <template #default="scope">
                {{ scope.row.expectedCount }}/{{ scope.row.actualCount || 0 }}
              </template>
            </el-table-column>
            <el-table-column label="进度" align="center" width="120">
              <template #default="scope">
                <el-progress
                  :percentage="getProgress(scope.row)"
                  :status="scope.row.status === 2 ? 'success' : undefined"
                  :stroke-width="15"
                />
              </template>
            </el-table-column>
            <el-table-column label="状态" align="center" prop="status" width="100">
              <template #default="scope">
                <el-tag v-if="scope.row.status === 0" type="info">待执行</el-tag>
                <el-tag v-else-if="scope.row.status === 1" type="primary">执行中</el-tag>
                <el-tag v-else-if="scope.row.status === 2" type="success">已完成</el-tag>
                <el-tag v-else-if="scope.row.status === 3" type="warning">已停止</el-tag>
                <el-tag v-else-if="scope.row.status === 4" type="danger">失败</el-tag>
              </template>
            </el-table-column>
            <el-table-column label="开始时间" align="center" prop="startTime" width="160">
              <template #default="scope">
                {{ formatDate(scope.row.startTime) }}
              </template>
            </el-table-column>
            <el-table-column label="结束时间" align="center" prop="endTime" width="160">
              <template #default="scope">
                {{ formatDate(scope.row.endTime) }}
              </template>
            </el-table-column>
            <el-table-column label="操作" align="center" width="120" fixed="right">
              <template #default="scope">
                <el-button link type="primary" @click="openForm('view', scope.row)">
                  详情
                </el-button>
                <el-button link type="danger" @click="handleDelete(scope.row)">删除</el-button>
              </template>
            </el-table-column>
          </el-table>

          <Pagination
            :total="total"
            v-model:page="queryParams.pageNo"
            v-model:limit="queryParams.pageSize"
            @pagination="getList"
          />
        </ContentWrap>
      </el-col>
    </el-row>

    <FbOperationForm ref="formRef" @success="getList" />
    <RepostForm ref="repostFormRef" @success="getList" />
    <PostCommentForm ref="postCommentFormRef" @success="getList" />
    <DmTaskForm ref="dmTaskFormRef" @success="getList" />
    <PublishPostForm ref="publishPostFormRef" @success="getList" />
    <GroupPublishForm ref="groupPublishFormRef" @success="getList" />
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted, onUnmounted } from 'vue'
import { Tools, List } from '@element-plus/icons-vue'
import ContentWrap from '@/components/ContentWrap/src/ContentWrap.vue'
import OperationCard from './components/OperationCard.vue'
import FbOperationForm from './FbOperationForm.vue'
import RepostForm from './RepostForm.vue'
import PostCommentForm from './PostCommentForm.vue'
import DmTaskForm from './dmtask/DmTaskForm.vue'
import PublishPostForm from './PublishPostForm.vue'
import GroupPublishForm from './GroupPublishForm.vue'
import { FbAccountApi } from '@/api/facebook/account'
import {
  getFbOperationTaskPage,
  deleteFbOperationTask,
  batchSaveAddGroupResult,
  FbOperationTask
} from '@/api/facebook/operation'
import { DmTaskApi } from '@/api/facebook/dmtask'
import { onCollectionComplete } from '@/utils/wpfBridge'

defineOptions({ name: 'FbOperation' })

const message = useMessage()
const { t } = useI18n()

const operationTools = [
  { type: 'add-group', title: '链接加组', icon: 'ep:user-filled', disabled: false },
  { type: 'repost', title: '转帖', icon: 'ep:share', disabled: false },
  { type: 'post-comment', title: '帖子评论', icon: 'ep:chat-dot-round', disabled: false },
  { type: 'mass-message', title: '群发私信', icon: 'ep:message', disabled: false },
  { type: 'publish-post', title: '发个人帖', icon: 'ep:document-add', disabled: false },
  { type: 'group-publish', title: '发群帖', icon: 'ep:connection', disabled: false }
]

const activeTool = ref('')
const loading = ref(true)
const list = ref<FbOperationTask[]>([])
const total = ref(0)
const queryParams = reactive({
  pageNo: 1,
  pageSize: 10,
  taskType: undefined as number | undefined,
  status: undefined as number | undefined,
  createTime: [] as string[]
})
const queryFormRef = ref()
const handledAddGroupDetailIds = new Set<string>()
const accountCache = ref<any[]>([])

const getList = async () => {
  loading.value = true
  try {
    const data = await getFbOperationTaskPage(queryParams)
    list.value = data.list || []
    total.value = data.total || 0
  } catch (error) {
    console.error('获取列表失败:', error)
    list.value = []
    total.value = 0
  } finally {
    loading.value = false
  }
}

const selectTool = (type: string) => {
  if (operationTools.find((f) => f.type === type)?.disabled) {
    message.warning('该功能开发中')
    return
  }
  activeTool.value = type
  if (type === 'repost') {
    repostFormRef.value.open()
  } else if (type === 'post-comment') {
    postCommentFormRef.value.open()
  } else if (type === 'mass-message') {
    dmTaskFormRef.value.open('create')
  } else if (type === 'publish-post') {
    publishPostFormRef.value.open()
  } else if (type === 'group-publish') {
    groupPublishFormRef.value.open()
  } else {
    formRef.value.open('create', undefined, getTaskTypeByTool(type))
  }
}

const getTaskTypeByTool = (toolType: string): number => {
  const typeMap: Record<string, number> = {
    'add-group': 9,
    repost: 10,
    'post-comment': 15,
    'mass-message': 14,
    'publish-post': 12,
    'group-publish': 13
  }
  return typeMap[toolType] || 9
}

const getProgress = (task: FbOperationTask) => {
  if (!task.expectedCount || task.expectedCount === 0) return 0
  if (task.status === 2) return 100
  return Math.min(100, Math.round(((task.actualCount || 0) / task.expectedCount) * 100))
}

const formatDate = (date: string | Date | null | undefined) => {
  if (!date) return '-'
  const d = new Date(date)
  return d.toLocaleString('zh-CN', {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit'
  })
}

const handleQuery = () => {
  queryParams.pageNo = 1
  getList()
}

const formRef = ref()
const repostFormRef = ref()
const postCommentFormRef = ref()
const dmTaskFormRef = ref()
const publishPostFormRef = ref()
const groupPublishFormRef = ref()

const openForm = (type: string, row: FbOperationTask) => {
  formRef.value.open(type, row.id)
}

const handleDelete = async (row: FbOperationTask) => {
  try {
    await message.delConfirm()
    if (row.sourceType === 'dm' || row.taskType === 14) {
      await DmTaskApi.deleteDmTask(row.id!)
    } else {
      await deleteFbOperationTask(Number(row.id))
    }
    message.success(t('common.delSuccess'))
    await getList()
  } catch {}
}

const loadAccountCache = async () => {
  try {
    const data = await FbAccountApi.getFbAccountPage({ pageNo: 1, pageSize: 500 })
    accountCache.value = data?.list || []
  } catch (error) {
    console.error('加载账号列表失败:', error)
    accountCache.value = []
  }
}

const saveAddGroupResults = async (data: any) => {
  if (data.taskType !== 9) return
  const detailId = String(data.detailId || '')
  if (!detailId || handledAddGroupDetailIds.has(detailId)) return
  handledAddGroupDetailIds.add(detailId)

  const results = Array.isArray(data.results)
    ? data.results.map((item: any) => {
        const accountId = String(item.accountId || data.accountId || '')
        const accountInfo = accountCache.value.find((acc) => String(acc.id) === accountId)
        return {
          ...item,
          accountId,
          groupId: item.groupId != null ? String(item.groupId) : undefined,
          fbAccount: accountInfo?.fbAccount || item.fbAccount || ''
        }
      })
    : []

  if (results.length === 0) {
    console.warn(`明细 ${detailId} 加组结果为空，跳过保存`)
    return
  }

  await batchSaveAddGroupResult({ detailId, results })
  message.success(`明细 ${detailId} 加组结果已保存，共 ${results.length} 条`)
  await getList()
}

onMounted(() => {
  getList()
  loadAccountCache()
  window.addEventListener('fb:dm:result:saved', getList as EventListener)
  window.addEventListener('fb:repost:result:saved', getList as EventListener)
  window.addEventListener('fb:group-publish:result:saved', getList as EventListener)
  onCollectionComplete(async (data) => {
    try {
      await saveAddGroupResults(data)
    } catch (error) {
      console.error('保存加组结果失败:', error)
      message.error('保存加组结果失败')
      handledAddGroupDetailIds.delete(String(data.detailId || ''))
    }
  })
})

onUnmounted(() => {
  window.removeEventListener('fb:dm:result:saved', getList as EventListener)
  window.removeEventListener('fb:repost:result:saved', getList as EventListener)
  window.removeEventListener('fb:group-publish:result:saved', getList as EventListener)
})
</script>

<style scoped lang="scss">
.operation-section {
  .section-title {
    margin: 0 0 16px 0;
    color: var(--el-text-color-primary);
    font-size: 18px;
    font-weight: 600;
    display: flex;
    align-items: center;
    line-height: 1;

    .el-icon {
      display: flex;
      align-items: center;
    }

    span {
      display: flex;
      align-items: center;
    }
  }

  .operation-grid {
    display: grid;
    grid-template-columns: repeat(2, 1fr);
    gap: 12px;
  }
}

.task-section {
  .section-title {
    margin: 0 0 16px 0;
    color: var(--el-text-color-primary);
    font-size: 18px;
    font-weight: 600;
    display: flex;
    align-items: center;
    line-height: 1;

    .el-icon {
      display: flex;
      align-items: center;
    }

    span {
      display: flex;
      align-items: center;
    }
  }
}

:deep(.el-col:last-child) {
  .el-form {
    padding-bottom: 16px;
    border-bottom: 2px solid var(--el-border-color);
  }
}
</style>
