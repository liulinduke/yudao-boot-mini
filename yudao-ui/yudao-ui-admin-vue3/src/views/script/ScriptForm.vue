<template>
  <Dialog :title="dialogTitle" v-model="dialogVisible" width="800px">
    <el-form
      ref="formRef"
      :model="formData"
      :rules="formRules"
      label-width="100px"
      v-loading="formLoading"
    >
      <el-form-item label="话术标题" prop="scriptTitle">
        <el-input v-model="formData.scriptTitle" placeholder="请输入话术标题" maxlength="100" show-word-limit />
      </el-form-item>
      <el-form-item label="话术内容" prop="scriptContent">
        <el-input
          v-model="formData.scriptContent"
          type="textarea"
          :rows="6"
          placeholder="请输入话术内容"
          maxlength="5000"
          show-word-limit
        />
      </el-form-item>
      <el-form-item label="内容类型" prop="contentType">
        <el-radio-group v-model="formData.contentType">
          <el-radio :label="1">文本</el-radio>
        </el-radio-group>
      </el-form-item>
      <el-form-item label="顺序发送" prop="sendSequence">
        <el-switch v-model="formData.sendSequence" />
        <span class="ml-2 text-xs text-gray-400">启用后将按添加的内容顺序依次发送</span>
      </el-form-item>
      <el-form-item label="排序" prop="sort">
        <el-input-number v-model="formData.sort" :min="0" :max="9999" />
      </el-form-item>
      <el-form-item label="备注" prop="remark">
        <el-input v-model="formData.remark" type="textarea" placeholder="请输入备注" />
      </el-form-item>
    </el-form>
    <template #footer>
      <div class="flex justify-center">
        <el-button @click="submitForm" type="primary" :disabled="formLoading">确 定</el-button>
        <el-button @click="dialogVisible = false">取 消</el-button>
      </div>
    </template>
  </Dialog>
</template>

<script setup lang="ts">
import { createScript, updateScript, getScript, FbScriptVO } from '@/api/facebook/script'

const message = useMessage()
const { t } = useI18n()

const dialogVisible = ref(false)
const dialogTitle = ref('')
const formLoading = ref(false)
const formType = ref('')
const formData = ref({
  id: undefined,
  groupId: undefined,
  scriptTitle: '',
  scriptContent: '',
  contentType: 1,
  attachments: '',
  sendSequence: false,
  sort: 0,
  remark: ''
})
const formRules = reactive({
  scriptContent: [{ required: true, message: '话术内容不能为空', trigger: 'blur' }]
})
const formRef = ref()

/** 打开弹窗 */
const open = async (type: string, id?: number, groupId?: number) => {
  dialogVisible.value = true
  dialogTitle.value = type === 'create' ? '新增话术' : '修改话术'
  formType.value = type
  resetForm()
  if (groupId) {
    formData.value.groupId = groupId
  }
  if (id) {
    formLoading.value = true
    try {
      const data = await getScript(id)
      formData.value = data
    } finally {
      formLoading.value = false
    }
  }
}
defineExpose({ open })

/** 提交表单 */
const emit = defineEmits(['success'])
const submitForm = async () => {
  if (!formRef.value) return
  await formRef.value.validate()
  formLoading.value = true
  try {
    const data = formData.value as unknown as FbScriptVO
    if (formType.value === 'create') {
      await createScript(data)
      message.success(t('common.createSuccess'))
    } else {
      await updateScript(data)
      message.success(t('common.updateSuccess'))
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
    groupId: undefined,
    scriptTitle: '',
    scriptContent: '',
    contentType: 1,
    attachments: '',
    sendSequence: false,
    sort: 0,
    remark: ''
  }
  formRef.value?.resetFields()
}
</script>
