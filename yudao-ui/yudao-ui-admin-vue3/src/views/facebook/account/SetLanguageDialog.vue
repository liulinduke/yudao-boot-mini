<template>
  <Dialog title="切换语言" v-model="dialogVisible" width="520px">
    <el-form label-width="90px">
      <el-alert
        title="不同账号可并行切换，受系统最大浏览器槽位限制；同一账号不会并行执行任务。"
        type="info"
        :closable="false"
        class="mb-4"
      />
      <el-form-item label="目标语言">
        <el-select v-model="languageCode" filterable class="w-full" placeholder="请选择 Facebook 语言">
          <el-option
            v-for="item in facebookLanguages"
            :key="`${item.code}-${item.nativeName}`"
            :label="`${item.nativeName} / ${item.englishName}`"
            :value="item.code"
          />
        </el-select>
      </el-form-item>
      <el-form-item label="已选账号">
        <el-tag type="primary">{{ selectedAccounts.length }} 个账号</el-tag>
      </el-form-item>
    </el-form>

    <template #footer>
      <el-button type="primary" :loading="formLoading" @click="submitForm">开始切换</el-button>
      <el-button :disabled="formLoading" @click="dialogVisible = false">取消</el-button>
    </template>
  </Dialog>
</template>

<script setup lang="ts">
import { FbAccountApi } from '@/api/facebook/account'
import { useMessage } from '@/hooks/web/useMessage'
import { facebookLanguages } from './facebookLanguages'

defineOptions({ name: 'SetLanguageDialog' })

const message = useMessage()
const dialogVisible = ref(false)
const formLoading = ref(false)
const languageCode = ref('en_US')
const selectedAccounts = ref<any[]>([])
const emit = defineEmits(['success'])

const open = (accounts: any[]) => {
  selectedAccounts.value = accounts
  languageCode.value = 'en_US'
  dialogVisible.value = true
}

const submitForm = async () => {
  if (!selectedAccounts.value.length) {
    message.warning('请选择要切换语言的账号')
    return
  }

  const target = facebookLanguages.find(item => item.code === languageCode.value)
  if (!target) {
    message.warning('请选择目标语言')
    return
  }

  formLoading.value = true
  try {
    await Promise.all(selectedAccounts.value.map(account =>
      FbAccountApi.updateFbAccountLanguage(account.id, target.code)
    ))

    const bridge = (window as any).chrome?.webview?.hostObjects?.sync?.wpfBridge
    if (!bridge?.SetAccountLanguage) {
      throw new Error('WPF语言切换服务未就绪')
    }

    const payload = selectedAccounts.value.map(account => ({
      accountId: account.fbAccount || '',
      cookie: account.cookie || ''
    }))
    bridge.SetAccountLanguage(JSON.stringify(payload), JSON.stringify(target))
    message.success(`已提交 ${payload.length} 个账号，正在并行切换为${target.nativeName}`)
    dialogVisible.value = false
    emit('success')
  } catch (error: any) {
    message.error(error?.message || '切换语言失败')
  } finally {
    formLoading.value = false
  }
}

defineExpose({ open })
</script>
