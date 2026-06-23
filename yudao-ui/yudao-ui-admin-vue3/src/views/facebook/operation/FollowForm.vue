<template>
  <Dialog :title="dialogTitle" v-model="dialogVisible" width="780px">
    <el-form
      ref="formRef"
      :model="formData"
      :rules="formRules"
      label-width="120px"
      v-loading="formLoading"
    >
      <el-form-item label="目标链接" prop="targetUrl">
        <el-input
          v-model="formData.targetUrl"
          type="textarea"
          :rows="3"
          placeholder="填写需要进行刷粉的主页链接"
        />
        <div class="mt-2 text-orange-500 text-sm">
          提示：需先在 FB 上开启允许粉丝关注。
        </div>
      </el-form-item>

      <el-form-item label="执行命令" required>
        <el-radio-group v-model="followCommand">
          <el-radio label="follow">关注</el-radio>
        </el-radio-group>
      </el-form-item>

      <el-form-item label="加粉间隔" prop="intervalRisk">
        <el-select v-model="formData.intervalRisk" style="width: 280px">
          <el-option label="较高风险（30-60秒）" value="high" />
          <el-option label="建议（1-3分钟）" value="normal" />
          <el-option label="稳妥（3-5分钟）" value="safe" />
        </el-select>
      </el-form-item>

      <el-form-item label="账号配置" prop="accountIds">
        <el-select
          v-model="formData.accountIds"
          multiple
          filterable
          style="width: 100%"
          placeholder="请选择执行账号"
        >
          <el-option
            v-for="account in accounts"
            :key="account.id"
            :label="account.fbAccount + (account.remark ? '(' + account.remark + ')' : '')"
            :value="String(account.id)"
          />
        </el-select>
      </el-form-item>

      <el-form-item label="备注" prop="remark">
        <el-input v-model="formData.remark" type="textarea" :rows="3" placeholder="可选" />
      </el-form-item>
    </el-form>

    <template #footer>
      <el-button type="primary" :loading="formLoading" @click="submitForm">确定</el-button>
      <el-button @click="dialogVisible = false">取消</el-button>
    </template>
  </Dialog>
</template>

<script setup lang="ts">
import { reactive, ref } from 'vue'
import { Dialog } from '@/components/Dialog'
import { FbAccountApi } from '@/api/facebook/account'
import {
  createFbOperationTask,
  getFbOperationTask,
  type FbOperationTaskSaveReqVO
} from '@/api/facebook/operation'
import { startBrowserCollect } from '@/utils/wpfBridge'

defineOptions({ name: 'FollowForm' })

const TASK_TYPE = 16
const FOLLOW_ACTION_TYPE = 7
const emit = defineEmits(['success'])
const message = useMessage()

const dialogVisible = ref(false)
const dialogTitle = ref('创建刷粉任务')
const formLoading = ref(false)
const formRef = ref()
const accounts = ref<any[]>([])
const followCommand = ref('follow')

const formData = ref({
  targetUrl: '',
  accountIds: [] as string[],
  intervalRisk: 'high',
  remark: ''
})

const intervalMap: Record<string, [number, number]> = {
  high: [30, 60],
  normal: [60, 180],
  safe: [180, 300]
}

const normalizeTargetUrl = (raw: string) => String(raw || '').trim()
const hasMultipleTargetUrls = (raw: string) => normalizeTargetUrl(raw).split(/\r?\n/).filter(Boolean).length > 1

const formRules = reactive({
  targetUrl: [
    { required: true, message: '请输入目标主页', trigger: 'blur' },
    {
      validator: (_rule: any, value: string, callback: any) => {
        const url = normalizeTargetUrl(value)
        if (hasMultipleTargetUrls(value)) {
          callback(new Error('刷粉第一版仅支持单个主页链接'))
          return
        }
        if (!/^https?:\/\/(www\.)?facebook\.com\//i.test(url)) {
          callback(new Error('请输入有效的 Facebook 主页链接'))
          return
        }
        callback()
      },
      trigger: 'blur'
    }
  ],
  intervalRisk: [{ required: true, message: '请选择加粉间隔', trigger: 'change' }],
  accountIds: [{ required: true, message: '请选择执行账号', trigger: 'change' }]
})

const loadAccounts = async () => {
  const data = await FbAccountApi.getFbAccountPage({ pageNo: 1, pageSize: 200 })
  accounts.value = data?.list || []
}

const open = async () => {
  dialogVisible.value = true
  resetForm()
  await loadAccounts()
}

defineExpose({ open })

const submitForm = async () => {
  if (!formRef.value) return
  await formRef.value.validate()

  const targetUrl = normalizeTargetUrl(formData.value.targetUrl)
  const accountIds = formData.value.accountIds || []
  if (accountIds.length === 0) {
    message.warning('请选择执行账号')
    return
  }

  formLoading.value = true
  try {
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

    const intervalRangeSeconds = intervalMap[formData.value.intervalRisk] || intervalMap.high
    const configData = {
      actions: [FOLLOW_ACTION_TYPE],
      targetUrl,
      postUrl: targetUrl,
      intervalRisk: formData.value.intervalRisk,
      intervalRangeSeconds
    }

    const submitData: FbOperationTaskSaveReqVO = {
      taskType: TASK_TYPE,
      taskName: `刷粉_${timestamp}`,
      accountIds,
      targetUrls: targetUrl,
      postUrl: targetUrl,
      actionConfig: JSON.stringify(configData),
      expectedCount: accountIds.length,
      remark: formData.value.remark
    }

    const result = await createFbOperationTask(submitData)
    const respData = result?.data || result
    const taskId = respData?.id || respData
    message.success('任务创建成功')

    if (taskId) {
      const createdTaskDetail = await getFbOperationTask(String(taskId))
      const createdDetails = createdTaskDetail?.details || []
      const startedAccounts = new Set<string>()

      for (const detail of createdDetails) {
        const accountId = String(detail.accountId || '')
        if (!accountId || startedAccounts.has(accountId)) continue

        const accountInfo = accounts.value.find((acc) => String(acc.id) === accountId)
        if (!accountInfo) continue

        const followConfig = JSON.stringify({
          taskId: String(taskId),
          detailId: String(detail.id),
          targetUrl,
          actionConfig: configData
        })

        try {
          startBrowserCollect(
            String(detail.id),
            accountId,
            accountInfo.cookie || null,
            targetUrl,
            1,
            TASK_TYPE,
            followConfig,
            true
          )
          startedAccounts.add(accountId)
          console.log(`🚀 启动刷粉: 任务=${taskId}, 明细=${detail.id}, 账号=${accountId}`)
        } catch (error) {
          console.error(`启动账号 ${accountId} 的刷粉任务失败:`, error)
        }
      }

      if (startedAccounts.size > 0) {
        message.success(`已启动 ${startedAccounts.size} 个账号执行刷粉任务`)
      } else {
        message.warning('任务已创建，但 WPF 未连接、账号无效或关注额度不足')
      }
    }

    dialogVisible.value = false
    emit('success')
  } finally {
    formLoading.value = false
  }
}

const resetForm = () => {
  formData.value = {
    targetUrl: '',
    accountIds: [],
    intervalRisk: 'high',
    remark: ''
  }
  followCommand.value = 'follow'
  formRef.value?.resetFields()
}
</script>
