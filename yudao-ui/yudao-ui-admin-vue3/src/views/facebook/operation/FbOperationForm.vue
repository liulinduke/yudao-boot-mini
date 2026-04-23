<template>
  <Dialog :title="dialogTitle" v-model="dialogVisible" :width="formType === 'view' ? '90%' : '700px'">
    <div v-loading="formLoading">
      <!-- 主表信息 -->
      <el-card class="mb-4" v-if="formType === 'view' && taskDetail">
        <template #header>
          <div class="card-header">
            <span>📋 任务基本信息</span>
          </div>
        </template>
        <el-descriptions :column="3" border>
          <el-descriptions-item label="任务ID">{{ taskDetail.task?.id }}</el-descriptions-item>
          <el-descriptions-item label="任务类型">
            <el-tag v-if="taskDetail.task?.taskType === 1" type="primary">链接加组</el-tag>
            <el-tag v-else-if="taskDetail.task?.taskType === 2" type="success">转贴</el-tag>
            <el-tag v-else-if="taskDetail.task?.taskType === 3" type="warning">群发私信</el-tag>
          </el-descriptions-item>
          <el-descriptions-item label="状态">
            <el-tag v-if="taskDetail.task?.status === 0" type="info">待执行</el-tag>
            <el-tag v-else-if="taskDetail.task?.status === 1" type="primary">执行中</el-tag>
            <el-tag v-else-if="taskDetail.task?.status === 2" type="success">已完成</el-tag>
            <el-tag v-else-if="taskDetail.task?.status === 3" type="warning">已停止</el-tag>
            <el-tag v-else-if="taskDetail.task?.status === 4" type="danger">失败</el-tag>
          </el-descriptions-item>
          <el-descriptions-item label="期望数量">{{ taskDetail.task?.expectedCount || 0 }}</el-descriptions-item>
          <el-descriptions-item label="实际完成">{{ taskDetail.task?.actualCount || 0 }}</el-descriptions-item>
          <el-descriptions-item label="进度">
            <el-progress 
              :percentage="getTotalProgress()" 
              :status="taskDetail.task?.status === 2 ? 'success' : undefined"
            />
          </el-descriptions-item>
          <el-descriptions-item label="开始时间">{{ formatDate(taskDetail.task?.startTime) }}</el-descriptions-item>
          <el-descriptions-item label="结束时间">{{ formatDate(taskDetail.task?.endTime) }}</el-descriptions-item>
          <el-descriptions-item label="备注" :span="3">{{ taskDetail.task?.remark || '-' }}</el-descriptions-item>
        </el-descriptions>
      </el-card>

      <!-- 新建/编辑表单 -->
      <el-form
        ref="formRef"
        :model="formData"
        :rules="formRules"
        label-width="140px"
        v-if="formType === 'create'"
      >
        <!-- 风控警告 -->
        <div v-if="formData.taskType === 1" style="margin-bottom: 12px;">
          <el-alert
            title="建议每个账号每日加组不超过10个，避免触发风控机制"
            type="warning"
            :closable="false"
            show-icon
          />
        </div>

        <el-form-item label="执行账号" prop="accountIds">
          <el-select
            v-model="formData.accountIds"
            multiple
            placeholder="请选择执行账号"
            style="width: 100%"
            filterable
          >
            <el-option
              v-for="account in accounts"
              :key="account.id"
              :label="account.fbAccount + (account.remark ? '(' + account.remark + ')' : '')"
              :value="account.id"
            />
          </el-select>
        </el-form-item>

        <!-- 链接加组特殊UI -->
        <el-form-item v-if="formData.taskType === 1" label="选择群组" prop="targetGroupIds">
          <div class="w-full">
            <el-button type="primary" @click="openGroupSelector" class="mb-2">
              <Icon icon="ep:plus" class="mr-5px" /> 选择群组
            </el-button>
            <div v-if="selectedGroups.length > 0" class="mt-2">
              <el-tag
                v-for="group in selectedGroups"
                :key="group.id"
                closable
                @close="removeSelectedGroup(group.id)"
                class="mr-2 mb-2"
              >
                {{ group.groupName }}
              </el-tag>
              <div class="text-gray-500 text-sm mt-2">
                已选择 {{ selectedGroups.length }} 个群组，将自动对每个群组的成员执行加组操作
              </div>
            </div>
            <div v-else class="text-gray-400 text-sm mt-2">
              暂未选择群组，请点击上方按钮选择
            </div>
          </div>
        </el-form-item>

        <el-form-item label="备注" prop="remark">
          <el-input v-model="formData.remark" type="textarea" placeholder="请输入备注" />
        </el-form-item>
      </el-form>

      <!-- 编辑模式下的Tab -->
      <el-tabs v-if="formType === 'view'" v-model="activeTab" type="border-card">
        <!-- Tab 1: 采集明细 -->
        <el-tab-pane label="📊 采集明细" name="details">
          <el-table :data="detailList" stripe border max-height="500">
            <el-table-column label="明细ID" prop="id" width="100" />
            <el-table-column label="FB账号" prop="fbAccount" width="150" />
            <el-table-column label="期望/已采" width="120">
              <template #default="scope">
                {{ scope.row.expectedCount }}/{{ scope.row.actualCount || 0 }}
              </template>
            </el-table-column>
            <el-table-column label="进度" width="150">
              <template #default="scope">
                <el-progress 
                  :percentage="getDetailProgress(scope.row)" 
                  :status="scope.row.status === 2 ? 'success' : undefined"
                  :stroke-width="12"
                />
              </template>
            </el-table-column>
            <el-table-column label="状态" width="100">
              <template #default="scope">
                <el-tag v-if="scope.row.status === 0" type="info">待执行</el-tag>
                <el-tag v-else-if="scope.row.status === 1" type="primary">执行中</el-tag>
                <el-tag v-else-if="scope.row.status === 2" type="success">已完成</el-tag>
                <el-tag v-else-if="scope.row.status === 3" type="danger">失败</el-tag>
              </template>
            </el-table-column>
          </el-table>
        </el-tab-pane>

        <!-- Tab 2: 采集结果（仅链接加组显示） -->
        <el-tab-pane v-if="taskDetail?.task?.taskType === 1" label="👥 加组结果" name="results">
          <el-table :data="resultList" stripe border max-height="500">
            <el-table-column label="Facebook ID" prop="accountId" width="180" />
            <el-table-column label="FB账号" prop="fbAccount" width="150" />
            <el-table-column label="用户链接" prop="targetUrl" min-width="250" show-overflow-tooltip />
            <el-table-column label="群组名称" prop="groupName" width="150" />
            <el-table-column label="群组链接" prop="groupUrl" min-width="250" show-overflow-tooltip />
            <el-table-column label="加组状态" width="100">
              <template #default="scope">
                <el-tag v-if="scope.row.joinStatus === 0" type="info">待处理</el-tag>
                <el-tag v-else-if="scope.row.joinStatus === 1" type="success">成功</el-tag>
                <el-tag v-else-if="scope.row.joinStatus === 2" type="danger">失败</el-tag>
                <el-tag v-else-if="scope.row.joinStatus === 3" type="warning">已加入</el-tag>
              </template>
            </el-table-column>
            <el-table-column label="失败原因" prop="failReason" min-width="150" show-overflow-tooltip />
            <el-table-column label="加入时间" prop="joinTime" width="160">
              <template #default="scope">
                {{ formatDate(scope.row.joinTime) }}
              </template>
            </el-table-column>
          </el-table>
          <el-empty v-if="resultList.length === 0" description="暂无加组数据" />
        </el-tab-pane>
      </el-tabs>
    </div>

    <template #footer>
      <el-button type="primary" @click="submitForm" :loading="formLoading" v-if="formType === 'create'">
        确 定
      </el-button>
      <el-button @click="dialogVisible = false">关 闭</el-button>
    </template>
  </Dialog>

  <!-- 群组选择器组件 -->
  <GroupSelector
    v-model="groupSelectorVisible"
    @confirm="handleGroupConfirm"
  />
</template>

<script setup lang="ts">
import { ref, reactive } from 'vue'
import { Dialog } from '@/components/Dialog'
import { FbAccountApi } from '@/api/facebook/account'
import { FbCollectGroupApi, FbCollectGroup } from '@/api/facebook/fbcollectgroup'
import {
  createFbOperationTask,
  getFbOperationTask,
  FbOperationTaskSaveReqVO,
  FbOperationTaskDetailRespVO
} from '@/api/facebook/operation'
import { startBrowserCollect } from '@/utils/wpfBridge'
import GroupSelector from '../collect/components/GroupSelector.vue'

const message = useMessage()
const { t } = useI18n()

const dialogVisible = ref(false)
const dialogTitle = ref('')
const formLoading = ref(false)
const formType = ref('')
const activeTab = ref('details')
const taskDetail = ref<FbOperationTaskDetailRespVO | null>(null)
const detailList = ref<any[]>([])
const resultList = ref<any[]>([])

const formData = ref({
  id: undefined,
  taskType: 1, // 默认链接加组
  taskName: '',
  accountIds: [] as string[],
  targetUrls: '',
  targetGroupIds: '',
  expectedCount: 100,
  remark: ''
})

const formRules = reactive({
  accountIds: [{ required: true, message: '请选择执行账号', trigger: 'change' }],
  targetGroupIds: [
    {
      required: true,
      message: '请选择目标群组',
      trigger: 'change',
      validator: (rule: any, value: any, callback: any) => {
        if (formData.value.taskType === 1 && (!value || selectedGroups.value.length === 0)) {
          callback(new Error('请选择目标群组'))
        } else {
          callback()
        }
      }
    }
  ]
})

const formRef = ref()
const accounts = ref<any[]>([])

// 群组选择相关
const groupSelectorVisible = ref(false)
const selectedGroups = ref<FbCollectGroup[]>([])

/** 打开弹窗 */
const open = async (type: string, id?: number, taskTypeValue?: number) => {
  dialogVisible.value = true
  dialogTitle.value = type === 'view' ? '任务详情' : '新建任务'
  formType.value = type
  resetForm()

  if (type === 'create' && taskTypeValue !== undefined) {
    formData.value.taskType = taskTypeValue
  }

  await loadAccounts()

  if (id) {
    formLoading.value = true
    try {
      const data = await getFbOperationTask(id)
      taskDetail.value = data
      detailList.value = data.details || []
      resultList.value = data.results || []
    } finally {
      formLoading.value = false
    }
  }
}
defineExpose({ open })

/** 加载账号列表 */
const loadAccounts = async () => {
  try {
    const data = await FbAccountApi.getFbAccountPage({ pageNo: 1, pageSize: 200 })
    if (data && data.list && Array.isArray(data.list)) {
      accounts.value = data.list
    } else {
      accounts.value = []
    }
  } catch (error) {
    console.error('加载账号列表失败:', error)
    accounts.value = []
  }
}

/** 提交表单 */
const emit = defineEmits(['success'])
const submitForm = async () => {
  if (!formRef.value) return
  await formRef.value.validate()
  
  formLoading.value = true
  try {
    // 自动生成任务名称
    const taskNamePrefix = formData.value.taskType === 1 ? '链接加组' : '运营任务'
    const timestamp = new Date().toLocaleString('zh-CN', {
      year: 'numeric',
      month: '2-digit',
      day: '2-digit',
      hour: '2-digit',
      minute: '2-digit',
      second: '2-digit'
    }).replace(/[\/\s:]/g, '')
    
    const data = {
      ...formData.value,
      taskName: `${taskNamePrefix}_${timestamp}`,
      expectedCount: selectedGroups.value.length // 期望数量 = 选择的群组数量
    } as unknown as FbOperationTaskSaveReqVO
    
    const result = await createFbOperationTask(data)
    const respData = result.data || result
    
    message.success('任务创建成功')
    dialogVisible.value = false
    emit('success')
  } finally {
    formLoading.value = false
  }
}

/** 重置表单 */
const resetForm = () => {
  formData.value = {
    id: undefined,
    taskType: 1,
    taskName: '',
    accountIds: [],
    targetUrls: '',
    targetGroupIds: '',
    expectedCount: 100,
    remark: ''
  }
  selectedGroups.value = []
  taskDetail.value = null
  detailList.value = []
  resultList.value = []
  activeTab.value = 'details'
  formRef.value?.resetFields()
}

/** 计算总进度 */
const getTotalProgress = () => {
  if (!taskDetail.value?.task || !taskDetail.value.task.expectedCount) return 0
  return Math.min(100, Math.round(
    ((taskDetail.value.task.actualCount || 0) / taskDetail.value.task.expectedCount) * 100
  ))
}

/** 计算明细进度 */
const getDetailProgress = (detail: any) => {
  if (!detail.expectedCount) return 0
  return Math.min(100, Math.round(
    ((detail.actualCount || 0) / detail.expectedCount) * 100
  ))
}

/** 格式化日期 */
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

/** 打开群组选择弹框 */
const openGroupSelector = () => {
  groupSelectorVisible.value = true
}

/** 移除已选择的群组 */
const removeSelectedGroup = (groupId: number) => {
  const index = selectedGroups.value.findIndex(g => g.id === groupId)
  if (index > -1) {
    selectedGroups.value.splice(index, 1)
    if (selectedGroups.value.length > 0) {
      formData.value.targetGroupIds = selectedGroups.value.map(g => g.id).join(',')
    } else {
      formData.value.targetGroupIds = ''
    }
  }
}

/** 确认群组选择 */
const handleGroupConfirm = (groups: FbCollectGroup[]) => {
  selectedGroups.value = groups
  formData.value.targetGroupIds = groups.map(g => g.id).join(',')
  message.success(`已选择 ${groups.length} 个群组`)
}
</script>
