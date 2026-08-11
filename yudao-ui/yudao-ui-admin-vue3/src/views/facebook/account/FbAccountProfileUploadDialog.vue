<template>
  <Dialog v-model="dialogVisible" title="Facebook资料上传" width="760px">
    <el-form label-width="110px" v-loading="loading">
      <el-alert
        title="任务提交后在后台执行：不同账号并行，同一账号串行；关闭此弹框不影响任务。"
        type="warning"
        :closable="false"
        show-icon
        class="mb-4"
      />
      <el-form-item label="执行账号">
        <el-tag type="primary">已选择 {{ selectedAccounts.length }} 个账号</el-tag>
      </el-form-item>
      <el-form-item label="主页封面">
        <UploadImgs v-model="form.coverUrls" :limit="10" :file-size="10" @update:model-value="validateCoverImages" />
        <div class="text-xs text-gray-400 mt-1">至少 720 像素宽，可上传多张并随机分配。</div>
      </el-form-item>
      <el-form-item label="头像">
        <UploadImgs v-model="form.avatarUrls" :limit="10" :file-size="5" @update:model-value="validateAvatarImages" />
        <div class="text-xs text-gray-400 mt-1">宽高至少 320 像素，可上传多张并随机分配。</div>
      </el-form-item>
      <el-form-item label="昵称">
        <el-input
          v-model="form.nicknames"
          type="textarea"
          :rows="3"
          placeholder="每行一个昵称，将随机分配给账号"
        />
      </el-form-item>
      <el-form-item label="个人签名">
        <el-input
          v-model="form.signatures"
          type="textarea"
          :rows="3"
          placeholder="每行一条签名，单条不超过100个字符，将随机分配给账号"
        />
      </el-form-item>
      <el-form-item v-if="runningItems.length" label="执行进度">
        <div class="w-full">
          <div class="mb-2 text-sm text-gray-500">
            已完成 {{ completedCount }} / {{ runningItems.length }}
            · 成功 {{ successCount }}
            · 失败 {{ failedCount }}
            · 等待或执行中 {{ runningItems.length - completedCount }}
          </div>
          <div v-for="item in runningItems" :key="item.accountId" class="flex items-center mb-1 text-sm">
            <span class="w-180px truncate">{{ item.name }}</span>
            <el-tag :type="item.status === 'SUCCESS' ? 'success' : item.status === 'FAILED' ? 'danger' : 'info'" size="small">
              {{ item.status === 'SUCCESS' ? '完成' : item.status === 'FAILED' ? '失败' : item.status === 'RUNNING' ? '执行中' : '等待中' }}
            </el-tag>
            <span v-if="item.error" class="ml-2 text-red-500 truncate">{{ item.error }}</span>
          </div>
        </div>
      </el-form-item>
    </el-form>
    <template #footer>
      <el-button @click="dialogVisible = false" :disabled="loading">关闭</el-button>
      <el-button type="primary" @click="submit" :disabled="submitted || loading" :loading="loading">
        {{ submitted ? '已提交' : '立即上传' }}
      </el-button>
    </template>
  </Dialog>
</template>

<script setup lang="ts">
import { computed, reactive, ref } from 'vue'
import { Dialog } from '@/components/Dialog'
import { UploadImgs } from '@/components/UploadFile'
import { FbAccountApi, type FbAccount } from '@/api/facebook/account'
import { onProfileUpdateComplete } from '@/utils/wpfBridge'
import { getFbAccountProxyJson } from '@/utils/fbAccountProxy'

const message = useMessage()
const emit = defineEmits(['success'])
const dialogVisible = ref(false)
const loading = ref(false)
const selectedAccounts = ref<FbAccount[]>([])
const form = reactive({ avatarUrls: [] as string[], coverUrls: [] as string[], nicknames: '', signatures: '' })
type ProfileWorkItem = {
  accountId: string
  name: string
  cookie: string
  deviceId: string
  config: string
  status: 'QUEUED' | 'RUNNING' | 'SUCCESS' | 'FAILED'
  error?: string
}

const runningItems = ref<ProfileWorkItem[]>([])
const pendingItems = ref<ProfileWorkItem[]>([])
const activeProfileAccounts = new Set<string>()
const submitted = ref(false)
let retryTimer: number | undefined
const completedCount = computed(() => runningItems.value.filter((item) => item.status === 'SUCCESS' || item.status === 'FAILED').length)
const successCount = computed(() => runningItems.value.filter((item) => item.status === 'SUCCESS').length)
const failedCount = computed(() => runningItems.value.filter((item) => item.status === 'FAILED').length)

const lines = (value: string) => value.split(/\r?\n/).map((item) => item.trim()).filter(Boolean)
const shuffle = <T,>(items: T[]) => {
  const result = [...items]
  for (let i = result.length - 1; i > 0; i--) {
    const j = Math.floor(Math.random() * (i + 1))
    ;[result[i], result[j]] = [result[j], result[i]]
  }
  return result
}

const loadImage = (url: string) => new Promise<{ width: number; height: number }>((resolve, reject) => {
  const image = new Image()
  image.onload = () => resolve({ width: image.naturalWidth, height: image.naturalHeight })
  image.onerror = reject
  image.src = url
})

const validateCoverImages = async (urls: string[]) => {
  const valid: string[] = []
  for (const url of urls) {
    try {
      const size = await loadImage(url)
      if (size.width < 720) message.warning('封面宽度必须至少720像素，已移除不符合的图片')
      else valid.push(url)
    } catch {
      message.warning('封面图片无法读取，已移除')
    }
  }
  form.coverUrls = valid
}

const validateAvatarImages = async (urls: string[]) => {
  const valid: string[] = []
  for (const url of urls) {
    try {
      const size = await loadImage(url)
      if (size.width < 320 || size.height < 320) message.warning('头像宽高必须至少320像素，已移除不符合的图片')
      else valid.push(url)
    } catch {
      message.warning('头像图片无法读取，已移除')
    }
  }
  form.avatarUrls = valid
}

const validateNickname = (value: string) => {
  if (!/^[\p{L}\s]+$/u.test(value) || /(.)\1{2,}/u.test(value)) return false
  const upper = (value.match(/[A-Z]/g) || []).length
  return upper < Math.max(2, value.replace(/\s/g, '').length / 2)
}

const open = (accounts: FbAccount[]) => {
  if (pendingItems.value.length || activeProfileAccounts.size) {
    message.warning('上一批资料上传仍在后台执行，请先等待完成')
    return
  }
  selectedAccounts.value = accounts
  form.avatarUrls = []
  form.coverUrls = []
  form.nicknames = ''
  form.signatures = ''
  runningItems.value = []
  pendingItems.value = []
  submitted.value = false
  dialogVisible.value = true
}
defineExpose({ open })

const reportProfileResult = async (item: ProfileWorkItem, success: boolean, errorMessage?: string) => {
  try {
    await FbAccountApi.reportFbAccountProfile({
      accountId: item.accountId,
      status: success ? 'SUCCESS' : 'FAILED',
      errorMessage,
      avatarUrl: success ? JSON.parse(item.config).avatarUrl : undefined,
      coverUrl: success ? JSON.parse(item.config).coverUrl : undefined,
      nickname: success ? JSON.parse(item.config).nickname : undefined,
      signature: success ? JSON.parse(item.config).signature : undefined
    })
    if (success) emit('success')
  } catch (error) {
    console.error('保存资料上传结果失败:', error)
  }
}

const scheduleQueueRetry = () => {
  if (retryTimer !== undefined || !pendingItems.value.length) return
  retryTimer = window.setTimeout(() => {
    retryTimer = undefined
    void dispatchProfileQueue()
  }, 1000)
}

const dispatchProfileQueue = async () => {
  const bridge = window.chrome?.webview?.hostObjects?.sync?.wpfBridge
  if (!bridge?.StartProfileUpdateTask || !bridge?.GetAvailableBrowserSlots) {
    message.error('WPF资料上传桥接未就绪')
    return
  }

  while (pendingItems.value.length) {
    const slots = Number(bridge.GetAvailableBrowserSlots()) || 0
    if (slots <= 0) {
      scheduleQueueRetry()
      return
    }

    const item = pendingItems.value.shift()!
    item.status = 'RUNNING'
    activeProfileAccounts.add(item.accountId)
    try {
      const proxyConfigJson = await getFbAccountProxyJson(item.accountId)
      bridge.StartProfileUpdateTask(
        `profile_${Date.now()}_${item.accountId}`,
        item.accountId,
        item.cookie,
        item.deviceId,
        item.config,
        proxyConfigJson
      )
    } catch (error: any) {
      activeProfileAccounts.delete(item.accountId)
      item.status = 'FAILED'
      item.error = error?.message || '启动资料上传失败'
      await reportProfileResult(item, false, item.error)
    }
  }
}

const submit = async () => {
  const nicknames = lines(form.nicknames)
  const signatures = lines(form.signatures)
  if (!form.avatarUrls.length && !form.coverUrls.length && !nicknames.length && !signatures.length) {
    message.warning('请至少上传头像、封面或填写昵称、签名')
    return
  }
  if (signatures.some((value) => value.length > 100)) {
    message.warning('个人签名不能超过100个字符')
    return
  }
  if (nicknames.some((value) => !validateNickname(value))) {
    message.warning('昵称只能使用中英文和空格，不能包含数字、符号或重复字符')
    return
  }
  const accounts = selectedAccounts.value
  const avatarUrls = shuffle(form.avatarUrls)
  const coverUrls = shuffle(form.coverUrls)
  const nicknameList = shuffle(nicknames)
  const signatureList = shuffle(signatures)
  const items = accounts.map((account, index) => ({
    accountId: String(account.id),
    avatarUrl: avatarUrls.length ? avatarUrls[index % avatarUrls.length] : undefined,
    coverUrl: coverUrls.length ? coverUrls[index % coverUrls.length] : undefined,
    nickname: nicknameList.length ? nicknameList[index % nicknameList.length] : undefined,
    signature: signatureList.length ? signatureList[index % signatureList.length] : undefined
  }))
  loading.value = true
  submitted.value = false
  const workItems: ProfileWorkItem[] = items.map((item, index) => ({
    accountId: item.accountId,
    name: accounts[index].fbAccount || item.accountId,
    cookie: accounts[index].cookie || '',
    deviceId: String(accounts[index].deviceId || ''),
    config: JSON.stringify({ avatarUrl: item.avatarUrl || '', coverUrl: item.coverUrl || '', nickname: item.nickname || '', signature: item.signature || '' }),
    status: 'QUEUED'
  }))
  runningItems.value = workItems
  pendingItems.value = [...workItems]
  try {
    await FbAccountApi.uploadFbAccountProfile({ items })
    submitted.value = true
    loading.value = false
    message.success(`已提交${items.length}个账号的资料上传任务`)
    emit('success')
    void dispatchProfileQueue()
  } catch (error: any) {
    message.error(error?.message || '资料上传任务启动失败')
    loading.value = false
    pendingItems.value = []
    submitted.value = false
    runningItems.value = []
  }
}

onProfileUpdateComplete(async (result) => {
  // WPF 历史版本把资料结果放在 data 内，新版本直接放在事件顶层，统一展开处理。
  const payload = result?.data && typeof result.data === 'object'
    ? { ...result.data, accountId: result.accountId || result.data.accountId, detailId: result.detailId }
    : result
  const accountId = String(payload.accountId || '')
  const item = runningItems.value.find((entry) => entry.accountId === accountId)
  if (!item) return
  item.status = payload.success ? 'SUCCESS' : 'FAILED'
  item.error = payload.errorMessage
  activeProfileAccounts.delete(accountId)
  await reportProfileResult(item, !!payload.success, payload.errorMessage)
  if (pendingItems.value.length) void dispatchProfileQueue()
  if (runningItems.value.every((entry) => entry.status === 'SUCCESS' || entry.status === 'FAILED')) {
    message.success(`资料上传完成：成功 ${successCount.value} 个，失败 ${failedCount.value} 个`)
  }
})
</script>
