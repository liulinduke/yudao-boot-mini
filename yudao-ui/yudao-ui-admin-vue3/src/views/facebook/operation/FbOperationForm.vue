<template>
  <Dialog
    :title="dialogTitle"
    v-model="dialogVisible"
    :width="formType === 'view' ? '90%' : '700px'"
  >
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
            <el-tag v-if="taskDetail.task?.taskType === 9" type="primary">链接加组</el-tag>
            <el-tag v-else-if="taskDetail.task?.taskType === 10" type="success">转贴</el-tag>
            <el-tag v-else-if="taskDetail.task?.taskType === 13" type="info">发群帖</el-tag>
            <el-tag v-else-if="taskDetail.task?.taskType === 14" type="warning">群发私信</el-tag>
          </el-descriptions-item>
          <el-descriptions-item label="状态">
            <el-tag v-if="taskDetail.task?.status === 0" type="info">待执行</el-tag>
            <el-tag v-else-if="taskDetail.task?.status === 1" type="primary">执行中</el-tag>
            <el-tag v-else-if="taskDetail.task?.status === 2" type="success">已完成</el-tag>
            <el-tag v-else-if="taskDetail.task?.status === 3" type="warning">已停止</el-tag>
            <el-tag v-else-if="taskDetail.task?.status === 4" type="danger">失败</el-tag>
          </el-descriptions-item>
          <el-descriptions-item label="期望数量">{{
            taskDetail.task?.expectedCount || 0
          }}</el-descriptions-item>
          <el-descriptions-item label="实际完成">{{
            taskDetail.task?.actualCount || 0
          }}</el-descriptions-item>
          <el-descriptions-item label="进度">
            <el-progress
              :percentage="getTotalProgress()"
              :status="taskDetail.task?.status === 2 ? 'success' : undefined"
            />
          </el-descriptions-item>
          <el-descriptions-item label="开始时间">{{
            formatDate(taskDetail.task?.startTime)
          }}</el-descriptions-item>
          <el-descriptions-item label="结束时间">{{
            formatDate(taskDetail.task?.endTime)
          }}</el-descriptions-item>
          <el-descriptions-item v-if="taskDetail.task?.taskType === 10" label="帖链接" :span="3">
            <el-link
              v-if="repostPostUrl"
              :href="repostPostUrl"
              target="_blank"
              type="primary"
              class="break-all"
            >
              {{ repostPostUrl }}
            </el-link>
            <span v-else>-</span>
          </el-descriptions-item>
          <el-descriptions-item v-if="taskDetail.task?.taskType === 10" label="执行项" :span="3">
            <div v-if="repostActionTags.length > 0" class="flex flex-wrap gap-1">
              <el-tag v-for="tag in repostActionTags" :key="tag" size="small">{{ tag }}</el-tag>
            </div>
            <span v-else>-</span>
          </el-descriptions-item>
          <el-descriptions-item label="备注" :span="3">{{
            taskDetail.task?.remark || '-'
          }}</el-descriptions-item>
        </el-descriptions>
      </el-card>

      <!-- 转帖结果（转帖任务直接展示，不显示任务明细 Tab） -->
      <el-card v-if="formType === 'view' && taskDetail?.task?.taskType === 10" class="mb-4">
        <template #header>
          <div class="card-header flex items-center justify-between">
            <span>🔄 转帖结果</span>
            <span class="text-sm text-gray-500 font-normal">
              共 {{ repostResultList.length }} 条 · 成功 {{ repostSuccessCount }} · 待审核
              {{ repostPendingCount }} · 失败 {{ repostFailCount }}
            </span>
          </div>
        </template>
        <el-table :data="repostResultList" stripe border max-height="520">
          <el-table-column label="执行账号" min-width="160" show-overflow-tooltip>
            <template #default="scope">
              <div class="font-medium">{{ scope.row.fbAccount || scope.row.accountId || '-' }}</div>
            </template>
          </el-table-column>
          <el-table-column label="执行动作" width="140">
            <template #default="scope">
              <el-tag :type="getRepostActionTagType(scope.row.actionType)" size="small">
                {{ getRepostActionLabel(scope.row.actionType) }}
              </el-tag>
            </template>
          </el-table-column>
          <el-table-column label="目标" min-width="180" show-overflow-tooltip>
            <template #default="scope">
              {{ getRepostTargetLabel(scope.row) }}
            </template>
          </el-table-column>
          <el-table-column label="状态" width="90" align="center">
            <template #default="scope">
              <el-tag v-if="scope.row.status === 0" type="info" size="small">待处理</el-tag>
              <el-tag v-else-if="scope.row.status === 1" type="success" size="small">成功</el-tag>
              <el-tag v-else-if="scope.row.status === 3" type="warning" size="small">待审核</el-tag>
              <el-tag v-else-if="scope.row.status === 2" type="danger" size="small">失败</el-tag>
              <span v-else>-</span>
            </template>
          </el-table-column>
          <el-table-column label="备注" prop="remark" min-width="160" show-overflow-tooltip>
            <template #default="scope">
              {{ scope.row.remark || scope.row.failReason || '-' }}
            </template>
          </el-table-column>
          <el-table-column label="执行时间" width="165">
            <template #default="scope">
              {{ formatDate(scope.row.executeTime) }}
            </template>
          </el-table-column>
        </el-table>
        <el-empty v-if="repostResultList.length === 0" description="暂无转帖执行记录" />
      </el-card>

      <!-- 发群帖结果（发群帖任务直接展示，不显示任务明细 Tab） -->
      <el-card v-if="formType === 'view' && taskDetail?.task?.taskType === 13" class="mb-4">
        <template #header>
          <div class="card-header flex items-center justify-between">
            <span>📝 发群帖结果</span>
            <span class="text-sm text-gray-500 font-normal">
              共 {{ groupPublishResultList.length }} 条 · 成功 {{ groupPublishSuccessCount }} · 失败
              {{ groupPublishFailCount }}
            </span>
          </div>
        </template>
        <el-table :data="groupPublishResultList" stripe border max-height="520">
          <el-table-column label="执行账号" min-width="160" show-overflow-tooltip>
            <template #default="scope">
              <div class="font-medium">{{ scope.row.fbAccount || scope.row.accountId || '-' }}</div>
            </template>
          </el-table-column>
          <el-table-column label="群名/群链接" min-width="200" show-overflow-tooltip>
            <template #default="scope">
              <div v-if="scope.row.groupName" class="font-medium">{{ scope.row.groupName }}</div>
              <div v-if="scope.row.groupUrl" class="text-sm text-gray-500">
                <el-link :href="scope.row.groupUrl" target="_blank" type="primary">
                  {{ scope.row.groupUrl }}
                </el-link>
              </div>
              <span v-if="!scope.row.groupName && !scope.row.groupUrl">-</span>
            </template>
          </el-table-column>
          <el-table-column label="帖子内容" min-width="250" show-overflow-tooltip>
            <template #default="scope">
              {{ scope.row.postContent || scope.row.targetUrl || '-' }}
            </template>
          </el-table-column>
          <el-table-column label="发布状态" width="100" align="center">
            <template #default="scope">
              <el-tag
                v-if="scope.row.status === 0 || scope.row.joinStatus === 0"
                type="info"
                size="small"
                >待执行</el-tag
              >
              <el-tag
                v-else-if="scope.row.status === 1 || scope.row.joinStatus === 1"
                type="success"
                size="small"
                >成功</el-tag
              >
              <el-tag
                v-else-if="scope.row.status === 2 || scope.row.joinStatus === 2"
                type="danger"
                size="small"
                >失败</el-tag
              >
              <el-tag v-else-if="scope.row.joinStatus === 3" type="warning" size="small"
                >已加入</el-tag
              >
              <span v-else>-</span>
            </template>
          </el-table-column>
          <el-table-column label="失败原因" min-width="180" show-overflow-tooltip>
            <template #default="scope">
              {{ scope.row.failReason || '-' }}
            </template>
          </el-table-column>
          <el-table-column label="发布时间" width="165">
            <template #default="scope">
              {{ formatDate(scope.row.executeTime || scope.row.joinTime) }}
            </template>
          </el-table-column>
        </el-table>
        <el-empty v-if="groupPublishResultList.length === 0" description="暂无发群帖执行记录" />
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
        <div v-if="formData.taskType === 9" style="margin-bottom: 12px">
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
              :value="String(account.id)"
            />
          </el-select>
        </el-form-item>

        <!-- 链接加组特殊UI -->
        <el-form-item v-if="formData.taskType === 9" label="选择群组" prop="targetGroupIds">
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
            <div v-else class="text-gray-400 text-sm mt-2"> 暂未选择群组，请点击上方按钮选择 </div>
          </div>
        </el-form-item>

        <el-form-item label="备注" prop="remark">
          <el-input v-model="formData.remark" type="textarea" placeholder="请输入备注" />
        </el-form-item>
      </el-form>

      <!-- 编辑模式下的Tab（转帖和发群帖任务不显示任务明细，仅上方展示结果） -->
      <el-tabs
        v-if="
          formType === 'view' &&
          taskDetail?.task?.taskType !== 10 &&
          taskDetail?.task?.taskType !== 13
        "
        v-model="activeTab"
        type="border-card"
      >
        <!-- Tab 1: 任务明细 -->
        <el-tab-pane
          :label="
            taskDetail?.task?.taskType === 9
              ? '📊 加组明细'
              : taskDetail?.task?.taskType === 14
                ? '📨 私发明细'
                : '📊 任务明细'
          "
          name="details"
        >
          <!-- 群发私信明细 -->
          <el-table
            v-if="taskDetail?.task?.taskType === 14"
            :data="detailList"
            stripe
            border
            max-height="500"
          >
            <el-table-column label="明细ID" prop="id" width="90" />
            <el-table-column label="FB账号" prop="fbAccount" width="150" show-overflow-tooltip />
            <el-table-column
              label="目标用户ID"
              prop="targetUserId"
              width="160"
              show-overflow-tooltip
            />
            <el-table-column
              label="话术"
              prop="scriptContent"
              min-width="200"
              show-overflow-tooltip
            />
            <el-table-column label="状态" width="100">
              <template #default="scope">
                <el-tag v-if="scope.row.status === 0" type="info">待执行</el-tag>
                <el-tag v-else-if="scope.row.status === 2" type="success">成功</el-tag>
                <el-tag v-else-if="scope.row.status === 3" type="danger">失败</el-tag>
              </template>
            </el-table-column>
            <el-table-column label="发送时间" prop="sendTime" width="160">
              <template #default="scope">
                {{ formatDate(scope.row.sendTime) }}
              </template>
            </el-table-column>
            <el-table-column
              label="错误信息"
              prop="errorMsg"
              min-width="150"
              show-overflow-tooltip
            />
          </el-table>
          <!-- 其他运营任务明细 -->
          <el-table v-else :data="detailList" stripe border max-height="500">
            <el-table-column label="明细ID" prop="id" width="100" />
            <el-table-column label="FB账号" prop="fbAccount" width="150" />
            <el-table-column v-if="taskDetail?.task?.taskType !== 9" label="期望/已采" width="120">
              <template #default="scope">
                {{ scope.row.expectedCount }}/{{ scope.row.actualCount || 0 }}
              </template>
            </el-table-column>
            <el-table-column v-if="taskDetail?.task?.taskType === 9" label="群组数量" width="120">
              <template #default="scope">
                {{ scope.row.expectedCount }}
              </template>
            </el-table-column>
            <el-table-column v-if="taskDetail?.task?.taskType !== 9" label="进度" width="150">
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
          <el-empty v-if="detailList.length === 0" description="暂无明细数据" />
        </el-tab-pane>

        <!-- Tab 2: 采集结果（仅链接加组显示） -->
        <el-tab-pane v-if="taskDetail?.task?.taskType === 9" label="👥 加组结果" name="results">
          <el-table :data="resultList" stripe border max-height="500">
            <el-table-column label="Facebook ID" prop="accountId" width="180" />
            <el-table-column label="FB账号" prop="fbAccount" width="150" />
            <el-table-column
              label="用户链接"
              prop="targetUrl"
              min-width="250"
              show-overflow-tooltip
            />
            <el-table-column label="群组名称" prop="groupName" width="150" />
            <el-table-column
              label="群组链接"
              prop="groupUrl"
              min-width="250"
              show-overflow-tooltip
            />
            <el-table-column label="加组状态" width="100">
              <template #default="scope">
                <el-tag v-if="scope.row.joinStatus === 0" type="info">待处理</el-tag>
                <el-tag v-else-if="scope.row.joinStatus === 1" type="success">成功</el-tag>
                <el-tag v-else-if="scope.row.joinStatus === 2" type="danger">失败</el-tag>
                <el-tag v-else-if="scope.row.joinStatus === 3" type="warning">已加入</el-tag>
              </template>
            </el-table-column>
            <el-table-column
              label="失败原因"
              prop="failReason"
              min-width="150"
              show-overflow-tooltip
            />
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
      <el-button
        type="primary"
        @click="submitForm"
        :loading="formLoading"
        v-if="formType === 'create'"
      >
        确 定
      </el-button>
      <el-button @click="dialogVisible = false">关 闭</el-button>
    </template>
  </Dialog>

  <!-- 群组选择器组件 -->
  <GroupSelector v-model="groupSelectorVisible" @confirm="handleGroupConfirm" />
</template>

<script setup lang="ts">
import { ref, reactive, computed } from 'vue'
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
const repostResultList = ref<any[]>([]) // 转帖结果列表
const groupPublishResultList = ref<any[]>([]) // 发群帖结果列表

const REPOST_ACTION_LABELS: Record<number, string> = {
  1: '点赞',
  2: '转发到动态消息',
  3: '个人中心(已废弃)',
  4: '转贴到好友',
  5: '转发到群组'
}

const parsedRepostConfig = computed(() => {
  const raw = taskDetail.value?.task?.actionConfig || detailList.value[0]?.actionConfig || ''
  if (!raw) return null
  try {
    return typeof raw === 'string' ? JSON.parse(raw) : raw
  } catch {
    return null
  }
})

const repostPostUrl = computed(
  () => detailList.value[0]?.postUrl || repostResultList.value[0]?.postUrl || ''
)

const repostActionTags = computed(() => {
  const config = parsedRepostConfig.value
  if (!config?.actions?.length) return []
  const tags: string[] = []
  for (const action of config.actions) {
    if (action === 1) tags.push('点赞')
    else if (action === 2) tags.push('转发到动态消息')
    else if (action === 4) {
      const count = config.shareToFriendCount || 1
      tags.push(`转贴到好友 ×${count}`)
    } else if (action === 5) {
      const groups = config.selectedGroups || []
      if (groups.length > 0) {
        groups.forEach((g: any) => tags.push(`群组：${g.groupName || g.name || g.groupId}`))
      } else {
        tags.push(`转发到群组 ×${config.shareToGroupCount || 1}`)
      }
    }
  }
  return tags
})

const repostSuccessCount = computed(
  () => repostResultList.value.filter((r) => r.status === 1).length
)
const repostPendingCount = computed(
  () => repostResultList.value.filter((r) => r.status === 3).length
)
const repostFailCount = computed(() => repostResultList.value.filter((r) => r.status === 2).length)

const groupPublishSuccessCount = computed(
  () => groupPublishResultList.value.filter((r) => r.status === 1).length
)
const groupPublishFailCount = computed(
  () => groupPublishResultList.value.filter((r) => r.status === 2).length
)

const getRepostActionLabel = (actionType?: number) =>
  (actionType && REPOST_ACTION_LABELS[actionType]) || '未知操作'

const getRepostActionTagType = (actionType?: number) => {
  if (actionType === 1) return 'primary'
  if (actionType === 2) return 'success'
  if (actionType === 4) return 'info'
  if (actionType === 5) return 'warning'
  return 'info'
}

const getRepostTargetLabel = (row: any) => {
  if (row.actionType === 1) return '原帖'
  if (row.actionType === 2) return '本人动态（Feed）'
  if (row.actionType === 4) return row.targetName || 'Messenger 推荐好友'
  if (row.actionType === 5) return row.targetName || row.targetId || '未命名群组'
  return row.targetName || '-'
}

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
        if (formData.value.taskType === 9 && (!value || selectedGroups.value.length === 0)) {
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
const open = async (type: string, id?: string | number, taskTypeValue?: number) => {
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
      repostResultList.value = data.task?.taskType === 10 ? data.repostResults || [] : []
      groupPublishResultList.value =
        data.task?.taskType === 13 ? data.groupPublishResults || [] : []
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
    const taskNamePrefix = formData.value.taskType === 9 ? '链接加组' : '运营任务'
    const timestamp = new Date()
      .toLocaleString('zh-CN', {
        year: 'numeric',
        month: '2-digit',
        day: '2-digit',
        hour: '2-digit',
        minute: '2-digit',
        second: '2-digit'
      })
      .replace(/[\/\s:]/g, '')

    const accountsToUse =
      formData.value.taskType === 9
        ? Math.min(formData.value.accountIds.length, selectedGroups.value.length)
        : formData.value.accountIds.length
    const usedAccountIds = formData.value.accountIds.slice(0, accountsToUse)

    const data = {
      ...formData.value,
      accountIds: usedAccountIds.map((id) => String(id)),
      taskName: `${taskNamePrefix}_${timestamp}`,
      expectedCount: selectedGroups.value.length // 期望数量 = 选择的群组数量
    } as unknown as FbOperationTaskSaveReqVO

    const result = await createFbOperationTask(data)
    const respData = result.data || result
    const taskId = respData?.id || respData
    const createdTaskDetail = taskId ? await getFbOperationTask(String(taskId)) : null
    const createdDetails = createdTaskDetail?.details || []

    message.success('任务创建成功')

    // 如果是链接加组任务，创建成功后启动浏览器
    if (formData.value.taskType === 9 && taskId) {
      const startedAccounts = new Set<string>()
      const totalGroups = selectedGroups.value.length
      const totalAccounts = usedAccountIds.length

      // 群组平均分配：将群组列表分配给账号
      // 规则：10账号+10群组 → 每个账号1个群组；10账号+1群组 → 1个账号处理；5账号+12群组 → 2个账号处理3个，3个账号处理2个
      const accountsToUse = Math.min(totalAccounts, totalGroups)

      // 获取全局配置中的每日加组限制
      let dailyLimit = 10
      try {
        const config = await import('@/api/facebook/dailylimit').then((m) =>
          m.DailyLimitApi.getConfig()
        )
        dailyLimit = config.data?.join_group_daily_limit || 10
      } catch (e) {
        console.warn('⚠️ 获取全局配置失败，使用默认每日加组限制: 10')
      }

      for (let i = 0; i < accountsToUse; i++) {
        const accountId = usedAccountIds[i]
        if (startedAccounts.has(accountId)) continue

        const accountInfo = accounts.value.find((acc) => String(acc.id) === String(accountId))
        if (!accountInfo) continue

        // 调用API获取该账号今日剩余加组次数
        let remainingCount = dailyLimit
        try {
          const limitData = await import('@/api/facebook/dailylimit').then((m) =>
            m.DailyLimitApi.getRemainingCount(String(accountId), 'join_group')
          )
          remainingCount = limitData.data?.remaining || dailyLimit
        } catch (e) {
          console.warn(
            `⚠️ 获取账号 ${accountInfo.fbAccount} 剩余加组次数失败，默认允许 ${dailyLimit} 次`
          )
        }

        // 检查剩余次数
        if (remainingCount <= 0) {
          console.warn(
            `⚠️ 账号 ${accountInfo.fbAccount} 今日加组次数已达上限(${dailyLimit}次)，跳过`
          )
          continue
        }

        // 计算该账号需要处理的群组索引范围
        const groupsPerAccount = Math.ceil(totalGroups / accountsToUse)
        const startIndex = i * groupsPerAccount
        const endIndex = Math.min(startIndex + groupsPerAccount, totalGroups)

        // 获取分配给该账号的群组（不超过剩余次数）
        const maxGroupsToProcess = Math.min(remainingCount, endIndex - startIndex)
        const assignedGroups = selectedGroups.value.slice(
          startIndex,
          startIndex + maxGroupsToProcess
        )
        if (assignedGroups.length === 0) continue

        const cookie = accountInfo.cookie || null
        const operationDetail = createdDetails.find(
          (detail) => String(detail.accountId) === String(accountId)
        )
        if (!operationDetail?.id) {
          console.warn(`⚠️ 未找到账号 ${accountInfo.fbAccount} 对应的任务明细ID，跳过启动`)
          continue
        }

        try {
          // 构造该账号的群组配置
          const groupConfig = {
            groups: assignedGroups.map((g) => ({
              groupId: g.id,
              groupName: g.groupName,
              groupUrl: g.url
            }))
          }
          const configJson = JSON.stringify(groupConfig)

          const startUrl = assignedGroups[0]?.url || 'https://www.facebook.com'
          const detailId = String(operationDetail.id)

          startBrowserCollect(
            detailId,
            String(accountInfo.id), // 传数字ID字符串，不是fbAccount
            cookie,
            startUrl,
            assignedGroups.length,
            9, // 任务类型9：链接加组
            configJson,
            true // isOperation: 运营任务
          )
          startedAccounts.add(accountId)
          console.log(
            `🚀 启动链接加组任务: 任务ID=${taskId}, 账号=${accountInfo.fbAccount}, 分配群组=${assignedGroups.length}个[${startIndex + 1}-${startIndex + maxGroupsToProcess}]`
          )
        } catch (error) {
          console.error(`启动账号 ${accountInfo.fbAccount} 的浏览器失败:`, error)
        }
      }

      if (startedAccounts.size > 0) {
        message.success(
          `已启动 ${startedAccounts.size} 个账号的浏览器执行任务，共处理 ${totalGroups} 个群组`
        )
      } else {
        console.warn('⚠️ 未找到有效的账号信息或所有账号已达每日加组上限')
        message.warning('未启动浏览器：所有账号已达每日加组上限或无有效账号')
      }
    }

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
  repostResultList.value = []
  activeTab.value = 'details'
  formRef.value?.resetFields()
}

/** 计算总进度 */
const getTotalProgress = () => {
  const task = taskDetail.value?.task
  if (!task?.expectedCount) return 0
  if (task.status === 2) return 100
  return Math.min(100, Math.round(((task.actualCount || 0) / task.expectedCount) * 100))
}

/** 计算明细进度 */
const getDetailProgress = (detail: any) => {
  if (!detail.expectedCount) return 0
  if (detail.status === 2) return 100
  return Math.min(100, Math.round(((detail.actualCount || 0) / detail.expectedCount) * 100))
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
  const index = selectedGroups.value.findIndex((g) => g.id === groupId)
  if (index > -1) {
    selectedGroups.value.splice(index, 1)
    if (selectedGroups.value.length > 0) {
      formData.value.targetGroupIds = selectedGroups.value.map((g) => g.id).join(',')
    } else {
      formData.value.targetGroupIds = ''
    }
  }
}

/** 确认群组选择 */
const handleGroupConfirm = (groups: FbCollectGroup[]) => {
  selectedGroups.value = groups
  formData.value.targetGroupIds = groups.map((g) => g.id).join(',')
  message.success(`已选择 ${groups.length} 个群组`)
}
</script>
