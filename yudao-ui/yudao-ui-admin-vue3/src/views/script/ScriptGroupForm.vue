<template>
  <Dialog :title="dialogTitle" v-model="dialogVisible">
    <el-form
      ref="formRef"
      :model="formData"
      :rules="formRules"
      label-width="100px"
      v-loading="formLoading"
    >
      <el-form-item label="分组名称" prop="groupName">
        <el-input v-model="formData.groupName" placeholder="请输入分组名称" />
      </el-form-item>
      <el-form-item label="分组类型" prop="groupType">
        <el-radio-group v-model="formData.groupType">
          <el-radio :label="1">公共</el-radio>
          <el-radio :label="2">私有</el-radio>
        </el-radio-group>
      </el-form-item>
      <el-form-item label="排序" prop="sort">
        <el-input-number v-model="formData.sort" :min="0" :max="9999" />
      </el-form-item>
      <el-form-item label="备注" prop="remark">
        <el-input v-model="formData.remark" type="textarea" placeholder="请输入备注" />
      </el-form-item>
    </el-form>
    <template #footer>
      <el-button @click="submitForm" type="primary" :disabled="formLoading">确 定</el-button>
      <el-button @click="dialogVisible = false">取 消</el-button>
    </template>
  </Dialog>
</template>

<script setup lang="ts">
import { createScriptGroup, updateScriptGroup, FbScriptGroupVO } from '@/api/facebook/scriptgroup'

const message = useMessage()
const { t } = useI18n()

const dialogVisible = ref(false)
const dialogTitle = ref('')
const formLoading = ref(false)
const formType = ref('')
const formData = ref({
  id: undefined,
  groupName: '',
  groupType: 1,
  sort: 0,
  remark: ''
})
const formRules = reactive({
  groupName: [{ required: true, message: '分组名称不能为空', trigger: 'blur' }]
})
const formRef = ref()

/** 打开弹窗 */
const open = async (type: string, id?: number) => {
  dialogVisible.value = true
  dialogTitle.value = type === 'create' ? '新增话术分组' : '修改话术分组'
  formType.value = type
  resetForm()
  if (id) {
    formLoading.value = true
    try {
      formData.value = await getScriptGroup(id)
    } finally {
      formLoading.value = false
    }
  }
}
defineExpose({ open })

import { getScriptGroup } from '@/api/facebook/scriptgroup'

/** 提交表单 */
const emit = defineEmits(['success'])
const submitForm = async () => {
  if (!formRef.value) return
  await formRef.value.validate()
  formLoading.value = true
  try {
    const data = formData.value as unknown as FbScriptGroupVO
    if (formType.value === 'create') {
      await createScriptGroup(data)
      message.success(t('common.createSuccess'))
    } else {
      await updateScriptGroup(data)
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
    groupName: '',
    groupType: 1,
    sort: 0,
    remark: ''
  }
  formRef.value?.resetFields()
}
</script>
