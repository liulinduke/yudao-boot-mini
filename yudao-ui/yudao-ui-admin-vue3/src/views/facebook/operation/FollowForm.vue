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

      <el-form-item
        label="账号配置"
        :prop="formData.accountSelectionMode === 'MANUAL' ? 'accountIds' : undefined"
      >
        <FbAccountSelector
          v-model="formData.accountIds"
          v-model:selection-mode="formData.accountSelectionMode"
          v-model:auto-account-count="formData.autoAccountCount"
          :show-auto-count="true"
          :excluded-account-ids="excludedAccountIds"
          :action-types="['follow']"
          class="w-full"
        />
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
import { reactive, ref, watch } from 'vue'
import { Dialog } from '@/components/Dialog'
import { FbAccountApi, filterSelectableFbAccounts } from '@/api/facebook/account'
import FbAccountSelector from '../components/FbAccountSelector.vue'
import {
  createFbOperationTask,
  getFollowedAccountIds,
  type FbOperationTaskSaveReqVO
} from '@/api/facebook/operation'

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
const excludedAccountIds = ref<string[]>([])
const followCommand = ref('follow')

const formData = ref({
  targetUrl: '',
  accountIds: [] as string[],
  accountSelectionMode: 'AUTO' as 'AUTO' | 'MANUAL',
  autoAccountCount: undefined as number | undefined,
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
  accounts.value = filterSelectableFbAccounts(data?.list || [])
}

const loadFollowedAccountIds = async (targetUrl: string) => {
  if (!targetUrl) {
    excludedAccountIds.value = []
    return
  }
  try {
    const response = await getFollowedAccountIds(targetUrl)
    const ids = response?.data ?? response ?? []
    excludedAccountIds.value = Array.isArray(ids) ? ids.map((id) => String(id)) : []
  } catch (error) {
    console.error('查询已刷粉账号失败:', error)
    excludedAccountIds.value = []
  }
}

watch(() => formData.value.targetUrl, (value) => {
  void loadFollowedAccountIds(normalizeTargetUrl(value))
})

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
  if (formData.value.accountSelectionMode === 'MANUAL' && accountIds.length === 0) {
    message.warning('请选择执行账号')
    return
  }
  if (formData.value.accountSelectionMode === 'AUTO' && !formData.value.autoAccountCount) {
    message.warning('请输入自动分配的账号数量')
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
      accountSelectionMode: formData.value.accountSelectionMode,
      autoAccountCount: formData.value.autoAccountCount,
      targetUrls: targetUrl,
      postUrl: targetUrl,
      actionConfig: JSON.stringify(configData),
      expectedCount: formData.value.accountSelectionMode === 'AUTO'
        ? formData.value.autoAccountCount || 0
        : accountIds.length,
      remark: formData.value.remark
    }

    await createFbOperationTask(submitData)
    message.success('刷粉任务已创建，将按间隔到期后进入账号队列')

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
    accountSelectionMode: 'AUTO',
    autoAccountCount: undefined,
    intervalRisk: 'high',
    remark: ''
  }
  followCommand.value = 'follow'
  excludedAccountIds.value = []
  formRef.value?.resetFields()
}
</script>
