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
      <el-form-item label="分组描述" prop="description">
        <el-input v-model="formData.description" type="textarea" placeholder="请输入分组描述" />
      </el-form-item>
      <el-form-item label="排序" prop="sort">
        <el-input-number v-model="formData.sort" :min="0" class="!w-100%" />
      </el-form-item>
      <el-form-item label="状态" prop="status">
        <el-radio-group v-model="formData.status">
          <el-radio :label="1">启用</el-radio>
          <el-radio :label="0">禁用</el-radio>
        </el-radio-group>
      </el-form-item>
    </el-form>
    <template #footer>
      <el-button @click="submitForm" type="primary" :disabled="formLoading">确 定</el-button>
      <el-button @click="dialogVisible = false">取 消</el-button>
    </template>
  </Dialog>
</template>

<script setup lang="ts">
import { ref, reactive } from 'vue'
import { AccountGroupApi } from '@/api/facebook/accountgroup'
import { useMessage } from '@/hooks/web/useMessage'

const message = useMessage()
const dialogVisible = ref(false)
const dialogTitle = ref('')
const formLoading = ref(false)
const formType = ref('')
const formRef = ref()

const formData = ref({
  id: undefined,
  groupName: '',
  description: '',
  sort: 0,
  status: 1
})

const formRules = reactive({
  groupName: [{ required: true, message: '分组名称不能为空', trigger: 'blur' }]
})

/** 打开弹窗 */
const open = async (type: string, id?: number) => {
  dialogVisible.value = true
  dialogTitle.value = type === 'create' ? '新增账号分组' : '修改账号分组'
  formType.value = type
  resetForm()
  
  if (id) {
    formLoading.value = true
    try {
      const data = await AccountGroupApi.getAccountGroup(id)
      formData.value = data
    } finally {
      formLoading.value = false
    }
  }
}

/** 提交表单 */
const submitForm = async () => {
  await formRef.value.validate()
  formLoading.value = true
  try {
    const data = formData.value as any
    if (formType.value === 'create') {
      await AccountGroupApi.createAccountGroup(data)
      message.success('新增成功')
    } else {
      await AccountGroupApi.updateAccountGroup(data)
      message.success('修改成功')
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
    description: '',
    sort: 0,
    status: 1
  }
  formRef.value?.resetFields()
}

defineExpose({ open })

const emit = defineEmits(['success'])
</script>
