
<template>
  <Dialog :title="dialogTitle" v-model="dialogVisible">
    <el-form
      ref="formRef"
      :model="formData"
      :rules="formRules"
      label-width="100px"
      v-loading="formLoading"
    >
      <el-form-item label="代理名称" prop="proxyName">
        <el-input v-model="formData.proxyName" placeholder="请输入代理名称" />
      </el-form-item>
      <el-form-item label="代理类型" prop="proxyType">
        <el-select v-model="formData.proxyType" placeholder="请选择代理类型">
          <el-option :value="1" label="HTTP" />
          <el-option :value="2" label="HTTPS" />
          <el-option :value="3" label="SOCKS5" />
        </el-select>
      </el-form-item>
      <el-form-item label="服务器地址" prop="host">
        <el-input v-model="formData.host" placeholder="请输入服务器地址" />
      </el-form-item>
      <el-form-item label="端口" prop="port">
        <el-input-number
          v-model="formData.port"
          :min="1"
          :max="65535"
          placeholder="请输入端口"
        />
      </el-form-item>
      <el-form-item label="用户名" prop="username">
        <el-input v-model="formData.username" placeholder="请输入用户名（可选）" />
      </el-form-item>
      <el-form-item label="密码" prop="password">
        <el-input v-model="formData.password" type="password" placeholder="请输入密码（可选）" />
      </el-form-item>
      <el-form-item label="国家/地区" prop="country">
        <el-input v-model="formData.country" placeholder="请输入国家/地区" />
      </el-form-item>
      <el-form-item label="状态" prop="status">
        <el-radio-group v-model="formData.status">
          <el-radio :value="1">启用</el-radio>
          <el-radio :value="0">禁用</el-radio>
        </el-radio-group>
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
import { SysProxyApi, SysProxyCreateReqVO, SysProxyUpdateReqVO } from '@/api/system/proxy'
import { useMessage } from '@/hooks/web/useMessage'
import { useI18n } from '@/hooks/web/useI18n'

/** 代理表单 */
defineOptions({ name: 'SysProxyForm' })

const { t } = useI18n()
const message = useMessage()

const dialogVisible = ref(false)
const dialogTitle = ref('')
const formLoading = ref(false)
const formType = ref('')
const formData = ref({
  id: undefined,
  proxyName: undefined,
  proxyType: undefined,
  host: undefined,
  port: undefined,
  username: undefined,
  password: undefined,
  country: undefined,
  status: 1,
  remark: undefined,
})
const formRules = reactive({
  proxyName: [{ required: true, message: '代理名称不能为空', trigger: 'blur' }],
  proxyType: [{ required: true, message: '代理类型不能为空', trigger: 'change' }],
  host: [{ required: true, message: '服务器地址不能为空', trigger: 'blur' }],
  port: [{ required: true, message: '端口不能为空', trigger: 'blur' }],
})
const formRef = ref()

/** 打开弹窗 */
const open = async (type: string, id?: number) => {
  dialogVisible.value = true
  dialogTitle.value = t('action.' + type)
  formType.value = type
  resetForm()
  if (id) {
    formLoading.value = true
    try {
      const data = await SysProxyApi.getProxy(id)
      formData.value = {
        id: data.id,
        proxyName: data.proxyName,
        proxyType: data.proxyType,
        host: data.host,
        port: data.port,
        username: data.username,
        password: '',
        country: data.country,
        status: data.status,
        remark: data.remark,
      }
    } finally {
      formLoading.value = false
    }
  }
}
defineExpose({ open })

/** 提交表单 */
const emit = defineEmits(['success'])
const submitForm = async () => {
  await formRef.value.validate()
  formLoading.value = true
  try {
    if (formType.value === 'create') {
      const data: SysProxyCreateReqVO = {
        proxyName: formData.value.proxyName,
        proxyType: formData.value.proxyType,
        host: formData.value.host,
        port: formData.value.port,
        username: formData.value.username,
        password: formData.value.password,
        country: formData.value.country,
        status: formData.value.status,
        remark: formData.value.remark,
      }
      await SysProxyApi.createProxy(data)
      message.success(t('common.createSuccess'))
    } else {
      const data: SysProxyUpdateReqVO = {
        id: formData.value.id,
        proxyName: formData.value.proxyName,
        proxyType: formData.value.proxyType,
        host: formData.value.host,
        port: formData.value.port,
        username: formData.value.username,
        password: formData.value.password,
        country: formData.value.country,
        status: formData.value.status,
        remark: formData.value.remark,
      }
      await SysProxyApi.updateProxy(data)
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
    proxyName: undefined,
    proxyType: undefined,
    host: undefined,
    port: undefined,
    username: undefined,
    password: undefined,
    country: undefined,
    status: 1,
    remark: undefined,
  }
  formRef.value?.resetFields()
}
</script>
