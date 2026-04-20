<template>
  <Dialog :title="dialogTitle" v-model="dialogVisible">
    <el-form
      ref="formRef"
      :model="formData"
      :rules="formRules"
      label-width="100px"
      v-loading="formLoading"
    >
      <el-form-item label="FB账号" prop="fbAccount">
        <el-input v-model="formData.fbAccount" placeholder="请输入FB账号" />
      </el-form-item>
      <el-form-item label="密码" prop="password">
        <el-input v-model="formData.password" placeholder="请输入密码" />
      </el-form-item>
      <el-form-item label="地区" prop="area">
        <el-input v-model="formData.area" placeholder="请输入地区" />
      </el-form-item>
      <el-form-item label="好友数" prop="friends">
        <el-input v-model="formData.friends" placeholder="请输入好友数" />
      </el-form-item>
      <el-form-item label="账户分组" prop="groupId">
        <el-input v-model="formData.groupId" placeholder="请输入账户分组" />
      </el-form-item>
      <el-form-item label="账户状态" prop="status">
        <el-radio-group v-model="formData.status">
          <el-radio value="1">请选择字典生成</el-radio>
        </el-radio-group>
      </el-form-item>
      <el-form-item label="备注" prop="remark">
        <el-input v-model="formData.remark" placeholder="请输入备注" />
      </el-form-item>
      <el-form-item label="cookie" prop="cookie">
        <el-input v-model="formData.cookie" placeholder="请输入cookie" />
      </el-form-item>
      <el-form-item label="用户代理" prop="userAgent">
        <el-input v-model="formData.userAgent" placeholder="请输入用户代理" />
      </el-form-item>
      <el-form-item label="2FA" prop="tfa">
        <el-input v-model="formData.tfa" placeholder="请输入2FA" />
      </el-form-item>
      <el-form-item label="邮件信息" prop="email">
        <el-input v-model="formData.email" placeholder="请输入邮件信息" />
      </el-form-item>
      <el-form-item label="邮箱密码" prop="emailPassword">
        <el-input v-model="formData.emailPassword" placeholder="请输入邮箱密码" />
      </el-form-item>
      <el-form-item label="设备ID" prop="deviceId">
        <el-input v-model="formData.deviceId" placeholder="请输入设备ID" />
      </el-form-item>
      <el-form-item label="设备名称" prop="deviceName">
        <el-input v-model="formData.deviceName" placeholder="请输入设备名称" />
      </el-form-item>
      <el-form-item label="异常原因" prop="reason">
        <el-input v-model="formData.reason" placeholder="请输入异常原因" />
      </el-form-item>
      <el-form-item label="代理" prop="proxy">
        <el-input v-model="formData.proxy" placeholder="请输入代理" />
      </el-form-item>
      <el-form-item label="代理ID" prop="proxyId">
        <el-input v-model="formData.proxyId" placeholder="请输入代理ID" />
      </el-form-item>
      <el-form-item label="注册日期" prop="creationDate">
        <el-date-picker
          v-model="formData.creationDate"
          type="date"
          value-format="x"
          placeholder="选择注册日期"
        />
      </el-form-item>
    </el-form>
    <template #footer>
      <el-button @click="submitForm" type="primary" :disabled="formLoading">确 定</el-button>
      <el-button @click="dialogVisible = false">取 消</el-button>
    </template>
  </Dialog>
</template>
<script setup lang="ts">
import { FbAccountApi, FbAccount } from '@/api/facebook/account'

/** FB账号 表单 */
defineOptions({ name: 'FbAccountForm' })

const { t } = useI18n() // 国际化
const message = useMessage() // 消息弹窗

const dialogVisible = ref(false) // 弹窗的是否展示
const dialogTitle = ref('') // 弹窗的标题
const formLoading = ref(false) // 表单的加载中：1）修改时的数据加载；2）提交的按钮禁用
const formType = ref('') // 表单的类型：create - 新增；update - 修改
const formData = ref({
  id: undefined,
  fbAccount: undefined,
  password: undefined,
  area: undefined,
  friends: undefined,
  groupId: undefined,
  status: undefined,
  remark: undefined,
  cookie: undefined,
  userAgent: undefined,
  tfa: undefined,
  email: undefined,
  emailPassword: undefined,
  deviceId: undefined,
  deviceName: undefined,
  reason: undefined,
  proxy: undefined,
  proxyId: undefined,
  creationDate: undefined,
})
const formRules = reactive({
  fbAccount: [{ required: true, message: 'FB账号不能为空', trigger: 'blur' }],
})
const formRef = ref() // 表单 Ref

/** 打开弹窗 */
const open = async (type: string, id?: number) => {
  dialogVisible.value = true
  dialogTitle.value = t('action.' + type)
  formType.value = type
  resetForm()
  // 修改时，设置数据
  if (id) {
    formLoading.value = true
    try {
      formData.value = await FbAccountApi.getFbAccount(id)
    } finally {
      formLoading.value = false
    }
  }
}
defineExpose({ open }) // 提供 open 方法，用于打开弹窗

/** 提交表单 */
const emit = defineEmits(['success']) // 定义 success 事件，用于操作成功后的回调
const submitForm = async () => {
  // 校验表单
  await formRef.value.validate()
  // 提交请求
  formLoading.value = true
  try {
    const data = formData.value as unknown as FbAccount
    if (formType.value === 'create') {
      await FbAccountApi.createFbAccount(data)
      message.success(t('common.createSuccess'))
    } else {
      await FbAccountApi.updateFbAccount(data)
      message.success(t('common.updateSuccess'))
    }
    dialogVisible.value = false
    // 发送操作成功的事件
    emit('success')
  } finally {
    formLoading.value = false
  }
}

/** 重置表单 */
const resetForm = () => {
  formData.value = {
    id: undefined,
    fbAccount: undefined,
    password: undefined,
    area: undefined,
    friends: undefined,
    groupId: undefined,
    status: undefined,
    remark: undefined,
    cookie: undefined,
    userAgent: undefined,
    tfa: undefined,
    email: undefined,
    emailPassword: undefined,
    deviceId: undefined,
    deviceName: undefined,
    reason: undefined,
    proxy: undefined,
    proxyId: undefined,
    creationDate: undefined,
  }
  formRef.value?.resetFields()
}
</script>