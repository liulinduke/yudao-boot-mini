<template>
  <Dialog :title="dialogTitle" v-model="dialogVisible" width="800px">
    <el-form
      ref="formRef"
      :model="formData"
      :rules="formRules"
      label-width="120px"
      v-loading="formLoading"
    >
      <el-form-item
        label="执行账号"
        :prop="formData.accountSelectionMode === 'MANUAL' ? 'accountIds' : undefined"
      >
        <FbAccountSelector
          v-model="formData.accountIds"
          v-model:selection-mode="formData.accountSelectionMode"
          v-model:auto-account-count="formData.autoAccountCount"
          :show-auto-count="true"
          class="w-full"
        />
      </el-form-item>
      
      <el-form-item label="帖子内容" prop="postContent">
        <el-input
          v-model="formData.postContent"
          type="textarea"
          :rows="5"
          placeholder="请输入帖子内容"
          maxlength="5000"
          show-word-limit
        />
      </el-form-item>
      
      <el-form-item label="图片/视频" prop="mediaUrls">
        <div>
          <el-button type="primary" @click="handleSelectFiles">
            <el-icon><Plus /></el-icon>
            选择图片/视频
          </el-button>
          <el-button @click="clearFiles" v-if="formData.mediaUrls.length > 0">
            清空
          </el-button>
        </div>
        <div v-if="formData.mediaUrls.length > 0" class="mt-2">
          <el-tag
            v-for="(path, index) in formData.mediaUrls"
            :key="index"
            closable
            @close="removeFile(index)"
            class="mr-2 mb-2"
          >
            {{ getFileName(path) }}
          </el-tag>
        </div>
        <div class="text-sm text-gray-500 mt-1 ml-2">最多选择10个图片或视频（本地路径）</div>
      </el-form-item>
      
      <el-form-item label="隐私设置" prop="privacySetting">
        <el-radio-group v-model="formData.privacySetting">
          <el-radio :label="1" @mousedown.prevent>公开</el-radio>
          <el-radio :label="2" @mousedown.prevent>好友可见</el-radio>
          <el-radio :label="3" @mousedown.prevent>仅自己</el-radio>
        </el-radio-group>
      </el-form-item>
      
      <el-form-item label="发帖间隔" prop="intervalRange">
        <el-select v-model="formData.intervalRange" placeholder="请选择间隔范围" class="!w-200px">
          <el-option label="2-4秒" value="2-4" />
          <el-option label="4-10秒" value="4-10" />
          <el-option label="10-16秒" value="10-16" />
        </el-select>
        <span class="ml-10px text-gray-500">每个账号发帖的随机间隔时间</span>
      </el-form-item>
    </el-form>
    
    <template #footer>
      <el-button @click="dialogVisible = false">取 消</el-button>
      <el-button type="primary" @click="submitForm" :loading="formLoading">确 定</el-button>
    </template>
  </Dialog>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
import { Plus } from '@element-plus/icons-vue'
import { Dialog } from '@/components/Dialog'
import * as OperationApi from '@/api/facebook/operation'
import { FbAccountApi, filterSelectableFbAccounts } from '@/api/facebook/account'
import { AccountGroupApi } from '@/api/facebook/accountgroup'
import FbAccountSelector from '../components/FbAccountSelector.vue'

defineOptions({ name: 'PublishPostForm' })

const message = useMessage()
const { t } = useI18n()

const dialogVisible = ref(false)
const dialogTitle = ref('发个人帖')
const formLoading = ref(false)
const accountGroups = ref<any[]>([])
const accounts = ref<any[]>([])
const formData = ref({
  accountIds: [] as string[],
  accountSelectionMode: 'AUTO' as 'AUTO' | 'MANUAL',
  autoAccountCount: undefined as number | undefined,
  postContent: '',
  mediaUrls: [] as string[],  // 存储本地文件路径
  privacySetting: 1,
  intervalRange: '4-10'  // 间隔范围
})

const formRules = reactive({
  accountIds: [{ required: true, message: '请选择执行账号', trigger: 'change' }],
  postContent: [{ required: true, message: '请输入帖子内容', trigger: 'blur' }]
})

const formRef = ref()

/** 打开弹窗 */
const open = async () => {
  dialogVisible.value = true
  resetForm()
  await loadAccountGroups()
  await loadAccounts()
}
defineExpose({ open })

/** 加载账号分组 */
const loadAccountGroups = async () => {
  try {
    accountGroups.value = await AccountGroupApi.getAllEnabledGroups()
  } catch (error) {
    console.error('加载账号分组失败:', error)
  }
}

/** 加载账号列表 */
const loadAccounts = async () => {
  try {
    const data = await FbAccountApi.getFbAccountPage({ pageNo: 1, pageSize: 200 })
    accounts.value = filterSelectableFbAccounts(data.list || [])
  } catch (error) {
    console.error('加载账号失败:', error)
  }
}

/** 选择文件 - 调用 WPF 文件选择对话框 */
const handleSelectFiles = async () => {
  try {
    // 检查是否在 WPF 环境中
    if (!window.chrome?.webview?.hostObjects?.sync?.wpfBridge) {
      message.warning('请在 WPF 应用中使用此功能')
      return
    }
    
    // 调用 WPF 文件选择对话框
    const result = window.chrome.webview.hostObjects.sync.wpfBridge.SelectMediaFiles()
    const selectedFiles = JSON.parse(result) as string[]
    
    if (selectedFiles.length === 0) {
      return
    }
    
    // 限制最多10个文件
    if (selectedFiles.length > 10) {
      message.warning('最多只能选择10个文件')
      formData.value.mediaUrls = selectedFiles.slice(0, 10)
    } else {
      formData.value.mediaUrls = selectedFiles
    }
    
    message.success(`已选择 ${formData.value.mediaUrls.length} 个文件`)
    console.log('✅ 选择的文件:', formData.value.mediaUrls)
  } catch (error) {
    console.error('❌ 文件选择失败:', error)
    message.error('文件选择失败')
  }
}

/** 获取文件名 */
const getFileName = (path: string) => {
  return path.split('\\').pop() || path.split('/').pop() || path
}

/** 移除文件 */
const removeFile = (index: number) => {
  formData.value.mediaUrls.splice(index, 1)
}

/** 清空文件 */
const clearFiles = () => {
  formData.value.mediaUrls = []
}

/** 重置表单 */
const resetForm = () => {
  formData.value = {
    accountIds: [],
    accountSelectionMode: 'AUTO',
    autoAccountCount: undefined,
    postContent: '',
    mediaUrls: [],
    privacySetting: 1,
    intervalRange: '4-10'
  }
  formRef.value?.resetFields()
}

/** 提交表单 */
const emit = defineEmits(['success'])
const submitForm = async () => {
  if (formLoading.value) return
  formLoading.value = true
  const valid = await formRef.value?.validate()
  if (!valid) {
    formLoading.value = false
    return
  }
  
  try {
    // 解析间隔范围
    const [minSec, maxSec] = formData.value.intervalRange.split('-').map(Number)
    
    // 构建任务数据
    if (formData.value.accountSelectionMode === 'AUTO' && !formData.value.autoAccountCount) {
      message.warning('请输入自动分配的账号数量')
      return
    }

    const data = {
      taskType: 12, // 发个人帖
      taskName: `发个人帖-${new Date().getTime()}`, // 自动生成任务名称
      accountIds: formData.value.accountIds,
      accountSelectionMode: formData.value.accountSelectionMode,
      autoAccountCount: formData.value.autoAccountCount,
      expectedCount: formData.value.accountSelectionMode === 'AUTO'
        ? formData.value.autoAccountCount || 0
        : formData.value.accountIds.length,
      actionConfig: JSON.stringify({
        postContent: formData.value.postContent,
        mediaUrls: formData.value.mediaUrls,
        privacySetting: formData.value.privacySetting,
        minIntervalSeconds: minSec,
        maxIntervalSeconds: maxSec
      })
    }
    
    // 1. 创建任务
    const taskId = await OperationApi.createFbOperationTask(data)
    console.log('✅ 发个人帖任务创建成功, TaskId:', taskId)

    // 从后端明细取得最终账号和明细 ID，WPF 成功后必须用真实明细 ID 回传状态。
    const taskDetail = await OperationApi.getFbOperationTask(String(taskId))
    const detailData = (taskDetail as any)?.data || taskDetail
    const taskDetails = Array.isArray(detailData?.details) ? detailData.details : []
    const detailByAccountId = new Map<string, any>(
      taskDetails.map((detail: any) => [String(detail.accountId || '').trim(), detail])
    )

    // 自动模式下账号由后端最终分配，不能继续使用表单里的空 accountIds。
    let executionAccountIds = [...formData.value.accountIds]
    if (formData.value.accountSelectionMode === 'AUTO') {
      executionAccountIds = taskDetails
        .map((detail: any) => String(detail.accountId || '').trim())
        .filter(Boolean)
    }
    if (executionAccountIds.length === 0) {
      throw new Error('任务已创建，但没有获取到后端分配的执行账号')
    }
    
    // 2. 调用 WPF 执行任务（为每个账号启动）
    // @ts-ignore
    if (window.chrome?.webview?.hostObjects?.sync?.wpfBridge) {
      console.log('🚀 开始调用 WPF 执行发个人帖任务...')
      
      // 获取账号信息（需要从后端获取cookie）
      for (const accountId of executionAccountIds) {
        try {
          const accountInfo = await FbAccountApi.getFbAccount(accountId)
          const cookie = accountInfo.cookie || ''
          
          console.log(`📝 启动账号 ${accountId} 的发个人帖任务`)
          const detail = detailByAccountId.get(String(accountId))
          if (!detail?.id) {
            throw new Error(`未找到账号 ${accountId} 对应的任务明细`)
          }
          
          // @ts-ignore
          const proxyConfigJson = accountInfo.runtimeProxy
            ? JSON.stringify(accountInfo.runtimeProxy)
            : undefined
          window.chrome.webview.hostObjects.sync.wpfBridge.StartPublishPostTask(
            String(taskId),
            String(accountId),
            cookie,
            data.actionConfig,
            accountInfo.password,
            accountInfo.tfa,
            String(detail.id),
            proxyConfigJson
          )
          
          // 等待间隔时间（防风控）
          const intervalSeconds = (minSec + maxSec) / 2
          await new Promise(resolve => setTimeout(resolve, intervalSeconds * 1000))
        } catch (error) {
          console.error(`❌ 启动账号 ${accountId} 的任务失败:`, error)
        }
      }
      
      message.success('任务已创建并发送到 WPF 队列')
    } else {
      console.warn('⚠️ WPF 桥接对象不存在，任务已创建但未执行')
      message.warning('任务已创建，但 WPF 未连接')
    }
    
    dialogVisible.value = false
    emit('success')
  } catch (error) {
    console.error('创建任务失败:', error)
    message.error('创建失败')
  } finally {
    formLoading.value = false
  }
}
</script>

<style scoped lang="scss">
.text-sm {
  font-size: 12px;
}
.text-gray-500 {
  color: #9ca3af;
}
.mt-1 {
  margin-top: 4px;
}
</style>
