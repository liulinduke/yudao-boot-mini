<template>
  <Dialog v-model="dialogVisible" title="Facebook资料上传" width="760px">
    <el-form label-width="110px" v-loading="loading">
      <el-alert
        title="资料会直接修改 Facebook 主页，提交后立即按账号串行执行。"
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
          <div v-for="item in runningItems" :key="item.accountId" class="flex items-center mb-1 text-sm">
            <span class="w-180px truncate">{{ item.name }}</span>
            <el-tag :type="item.status === 'SUCCESS' ? 'success' : item.status === 'FAILED' ? 'danger' : 'info'" size="small">
              {{ item.status === 'SUCCESS' ? '完成' : item.status === 'FAILED' ? '失败' : '执行中' }}
            </el-tag>
            <span v-if="item.error" class="ml-2 text-red-500 truncate">{{ item.error }}</span>
          </div>
        </div>
      </el-form-item>
    </el-form>
    <template #footer>
      <el-button @click="dialogVisible = false" :disabled="loading">关闭</el-button>
      <el-button type="primary" @click="submit" :loading="loading">立即上传</el-button>
    </template>
  </Dialog>
</template>

<script setup lang="ts">
import { reactive, ref } from 'vue'
import { Dialog } from '@/components/Dialog'
import { UploadImgs } from '@/components/UploadFile'
import { FbAccountApi, type FbAccount } from '@/api/facebook/account'
import { onProfileUpdateComplete } from '@/utils/wpfBridge'

const message = useMessage()
const emit = defineEmits(['success'])
const dialogVisible = ref(false)
const loading = ref(false)
const selectedAccounts = ref<FbAccount[]>([])
const form = reactive({ avatarUrls: [] as string[], coverUrls: [] as string[], nicknames: '', signatures: '' })
const runningItems = ref<Array<{ accountId: string; name: string; status: string; error?: string }>>([])

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
  selectedAccounts.value = accounts
  form.avatarUrls = []
  form.coverUrls = []
  form.nicknames = ''
  form.signatures = ''
  runningItems.value = []
  dialogVisible.value = true
}
defineExpose({ open })

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
  runningItems.value = items.map((item, index) => ({ accountId: item.accountId, name: accounts[index].fbAccount || item.accountId, status: 'RUNNING' }))
  try {
    await FbAccountApi.uploadFbAccountProfile({ items })
    const bridge = window.chrome?.webview?.hostObjects?.sync?.wpfBridge
    if (!bridge?.StartProfileUpdateTask) throw new Error('WPF资料上传桥接未就绪')
    items.forEach((item) => {
      const account = accounts.find((candidate) => String(candidate.id) === item.accountId)!
      bridge.StartProfileUpdateTask(
        `profile_${Date.now()}_${item.accountId}`,
        item.accountId,
        account.cookie || '',
        String(account.deviceId || ''),
        JSON.stringify({ avatarUrl: item.avatarUrl || '', coverUrl: item.coverUrl || '', nickname: item.nickname || '', signature: item.signature || '' })
      )
    })
    message.success(`已提交${items.length}个账号的资料上传任务`)
    emit('success')
  } catch (error: any) {
    message.error(error?.message || '资料上传任务启动失败')
    loading.value = false
  }
}

onProfileUpdateComplete(async (result) => {
  const item = runningItems.value.find((entry) => entry.accountId === String(result.accountId))
  if (!item) return
  item.status = result.success ? 'SUCCESS' : 'FAILED'
  item.error = result.errorMessage
  try {
    await FbAccountApi.reportFbAccountProfile({
      accountId: String(result.accountId),
      status: result.success ? 'SUCCESS' : 'FAILED',
      errorMessage: result.errorMessage,
      avatarUrl: result.avatarUrl,
      coverUrl: result.coverUrl,
      nickname: result.nickname,
      signature: result.signature
    })
  } catch (error) {
    console.error('保存资料上传结果失败:', error)
  }
  if (runningItems.value.every((entry) => entry.status === 'SUCCESS' || entry.status === 'FAILED')) loading.value = false
})
</script>
