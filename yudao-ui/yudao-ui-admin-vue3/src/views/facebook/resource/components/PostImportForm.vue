<template>
  <Dialog v-model="dialogVisible" title="帖子数据导入" width="400">
    <el-upload
      ref="uploadRef"
      v-model:file-list="fileList"
      :action="importUrl"
      :auto-upload="false"
      :disabled="formLoading"
      :headers="uploadHeaders"
      :limit="1"
      :on-error="submitFormError"
      :on-exceed="handleExceed"
      :on-success="submitFormSuccess"
      accept=".xlsx, .xls"
      drag
    >
      <Icon icon="ep:upload" />
      <div class="el-upload__text">将文件拖到此处，或<em>点击上传</em></div>
      <template #tip>
        <div class="el-upload__tip text-center">
          <div class="text-left text-xs text-gray-500 mt-2 mb-2">
            <p>📝 导入说明：</p>
            <p>1. Excel只需填写一列：<strong>帖子URL</strong></p>
            <p>2. 系统会自动从URL抓取帖子详细信息</p>
            <p>3. 支持批量导入，建议单次不超过1000条</p>
          </div>
          <span>仅允许导入 xls、xlsx 格式文件。</span>
          <el-link
            :underline="false"
            style="font-size: 12px; vertical-align: baseline; margin-left: 8px"
            type="primary"
            @click="importTemplate"
          >
            下载模板
          </el-link>
        </div>
      </template>
    </el-upload>
    <template #footer>
      <el-button :disabled="formLoading" type="primary" @click="submitForm">确 定</el-button>
      <el-button @click="dialogVisible = false">取 消</el-button>
    </template>
  </Dialog>
</template>

<script lang="ts" setup>
import { FbCollectPostApi } from '@/api/facebook/fbcollectpost'
import { getAccessToken, getTenantId } from '@/utils/auth'
import download from '@/utils/download'
import type { UploadUserFile } from 'element-plus'

defineOptions({ name: 'FbCollectPostImportForm' })

const message = useMessage() // 消息弹窗

const dialogVisible = ref(false) // 弹窗的是否展示
const formLoading = ref(false) // 表单的加载中
const uploadRef = ref()
const importUrl =
  import.meta.env.VITE_BASE_URL + import.meta.env.VITE_API_URL + '/facebook/fb-collect-post/import'
const uploadHeaders = ref() // 上传 Header 头
const fileList = ref<UploadUserFile[]>([]) // 文件列表

/** 打开弹窗 */
const open = () => {
  dialogVisible.value = true
  fileList.value = []
  resetForm()
}
defineExpose({ open }) // 提供 open 方法，用于打开弹窗

/** 提交表单 */
const submitForm = async () => {
  if (fileList.value.length === 0) {
    message.error('请上传文件')
    return
  }
  // 设置请求头
  uploadHeaders.value = {
    Authorization: 'Bearer ' + getAccessToken(),
    'tenant-id': getTenantId()
  }
  formLoading.value = true
  uploadRef.value!.submit()
}

/** 文件上传成功 */
const emits = defineEmits(['success'])
const submitFormSuccess = (response: any) => {
  if (response.code !== 0) {
    message.error(response.msg)
    resetForm()
    return
  }
  
  // 显示导入结果
  const data = response.data
  let text = `导入成功！\n`
  text += `成功数量：${data.successCount || 0}\n`
  if (data.failureCount && data.failureCount > 0) {
    text += `失败数量：${data.failureCount}\n`
    if (data.failureMessages && data.failureMessages.length > 0) {
      text += `\n失败详情：\n${data.failureMessages.join('\n')}`
    }
  }
  
  message.alert(text)
  formLoading.value = false
  dialogVisible.value = false
  // 发送操作成功的事件
  emits('success')
}

/** 上传错误提示 */
const submitFormError = (): void => {
  message.error('上传失败，请您重新上传！')
  formLoading.value = false
}

/** 重置表单 */
const resetForm = async (): Promise<void> => {
  // 重置上传状态和文件
  formLoading.value = false
  await nextTick()
  uploadRef.value?.clearFiles()
}

/** 文件数超出提示 */
const handleExceed = (): void => {
  message.error('最多只能上传一个文件！')
}

/** 下载模板操作 */
const importTemplate = async () => {
  try {
    const res = await FbCollectPostApi.importFbCollectPostTemplate()
    download.excel(res, 'FB帖子导入模版.xls')
  } catch (error) {
    message.error('下载模板失败')
  }
}
</script>

<style scoped lang="scss">
.el-upload__tip {
  color: #909399;
  font-size: 12px;
}
</style>
