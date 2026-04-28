<template>
  <Dialog title="设置账号语言" v-model="dialogVisible">
    <el-form
      ref="formRef"
      :model="formData"
      label-width="100px"
      v-loading="formLoading"
    >
      <el-alert
        title="提示：修改语言后，将调用指纹浏览器自动切换Facebook语言设置"
        type="info"
        :closable="false"
        class="mb-4"
      />
      
      <el-form-item label="选择语言" prop="language">
        <el-radio-group v-model="formData.language">
          <el-radio :label="1">英文 (English)</el-radio>
          <el-radio :label="2">中文 (简体中文)</el-radio>
        </el-radio-group>
      </el-form-item>
      
      <el-form-item label="已选账号">
        <el-tag type="primary">{{ selectedAccounts.length }} 个账号</el-tag>
      </el-form-item>
    </el-form>
    
    <template #footer>
      <el-button @click="submitForm" type="primary" :disabled="formLoading">确 定</el-button>
      <el-button @click="dialogVisible = false">取 消</el-button>
    </template>
  </Dialog>
</template>

<script setup lang="ts">
import { FbAccountApi } from '@/api/facebook/account'
import { useMessage } from '@/hooks/web/useMessage'

defineOptions({ name: 'SetLanguageDialog' })

const message = useMessage()

const dialogVisible = ref(false)
const formLoading = ref(false)
const formData = ref({
  language: 1 // 默认英文
})
const selectedAccounts = ref<any[]>([])
const formRef = ref()

/** 打开弹窗 */
const open = async (accounts: any[]) => {
  dialogVisible.value = true
  selectedAccounts.value = accounts
  formData.value.language = 1 // 重置为默认值
}

defineExpose({ open })

/** 提交表单 */
const submitForm = async () => {
  if (selectedAccounts.value.length === 0) {
    message.warning('请选择要设置语言的账号')
    return
  }

  formLoading.value = true
  try {
    // 1. 先更新数据库中的语言字段
    const promises = selectedAccounts.value.map(account => 
      FbAccountApi.updateFbAccountLanguage(account.id, formData.value.language)
    )
    
    await Promise.all(promises)
    
    const langText = formData.value.language === 1 ? '英文' : '中文'
    message.success(`成功更新 ${selectedAccounts.value.length} 个账号的语言设置为${langText}`)
    
    // 2. 调用WPF指纹浏览器API切换语言
    const wpfBridge = (window as any).chrome?.webview?.hostObjects?.sync?.wpfBridge
    
    if (wpfBridge) {
      // WebView2环境：使用Host Object调用
      const accountIds = JSON.stringify(selectedAccounts.value.map(a => a.fbAccount))
      wpfBridge.SetAccountLanguage(accountIds, formData.value.language)
      message.info('正在调用指纹浏览器切换语言...')
    } else {
      console.warn('WPF桥接服务未就绪')
      console.log('当前环境:', {
        hasChrome: !!window.chrome,
        hasWebview: !!(window as any).chrome?.webview,
        hasHostObjects: !!(window as any).chrome?.webview?.hostObjects
      })
      message.warning('请在WPF应用中打开此页面')
    }
    
    dialogVisible.value = false
    emit('success')
  } catch (error) {
    console.error('设置语言失败:', error)
    message.error('设置语言失败，请重试')
  } finally {
    formLoading.value = false
  }
}

const emit = defineEmits(['success'])
</script>
