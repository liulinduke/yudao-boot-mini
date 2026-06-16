<template>
  <Dialog :title="dialogTitle" v-model="dialogVisible" width="700px">
    <div v-if="!tableView">
      <el-form :model="formData" label-width="100px">
        <el-form-item label="账号数据" prop="data">
          <el-input
            type="textarea"
            v-model="formData.data"
            :rows="12"
            placeholder="请输入账号数据，每行一个账号"
            class="font-mono text-sm"
          />
        </el-form-item>
        <el-form-item>
          <el-text type="info" size="small">
            <template #default>
              账号格式：<code>Facebook用户名----Facebook密码</code>
            </template>
          </el-text>
        </el-form-item>
        <el-form-item>
          <el-text type="info" size="small">
            <template #default>
              或：<code>Facebook用户名----Facebook密码----双重验证安全码</code>
            </template>
          </el-text>
        </el-form-item>
      </el-form>
    </div>
    <div v-else>
      <el-table
        :data="previewList"
        :max-height="300"
        border
        :show-overflow-tooltip="true"
      >
        <el-table-column label="序号" prop="no" width="60" />
        <el-table-column label="Facebook账号" prop="userName" />
        <el-table-column label="密码" prop="password" />
        <el-table-column label="双重验证安全码" prop="securityKey" />
        <el-table-column label="错误" prop="error" width="150">
          <template #default="scope">
            <el-tag v-if="scope.row.error" type="danger" size="small">
              {{ scope.row.error }}
            </el-tag>
          </template>
        </el-table-column>
      </el-table>

      <el-form :model="importSettings" label-width="100px" class="mt-4">
        <el-form-item label="设置分组">
          <el-select
            v-model="importSettings.groupId"
            placeholder="请选择分组"
            class="w-200px"
          >
            <el-option :value="null" label="不分组" />
            <el-option
              v-for="group in groupList"
              :key="group.id"
              :value="group.id"
              :label="group.groupName"
            />
          </el-select>
        </el-form-item>
        <el-form-item label="设置代理">
          <el-select
            v-model="importSettings.proxyId"
            placeholder="请选择代理"
            class="w-200px"
          >
            <el-option :value="null" label="不设置代理" />
            <el-option
              v-for="proxy in proxyList"
              :key="proxy.id"
              :value="proxy.id"
              :label="proxy.proxyName"
            />
          </el-select>
        </el-form-item>
      </el-form>
    </div>

    <template #footer>
      <template v-if="!tableView">
        <el-button @click="dialogVisible = false">取 消</el-button>
        <el-button type="primary" @click="handleNext">下一步</el-button>
      </template>
      <template v-else>
        <el-button @click="tableView = false">返回编辑</el-button>
        <el-button type="primary" @click="handleImport">确 定</el-button>
      </template>
    </template>
  </Dialog>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
import { Dialog } from '@/components/Dialog'
import { FbAccountApi, FbAccountImportReqVO, FbAccountImportPreviewVO } from '@/api/facebook/account'
import { AccountGroupApi } from '@/api/facebook/accountgroup'
import { SysProxyApi, SysProxyRespVO } from '@/api/system/proxy'
import { useMessage } from '@/hooks/web/useMessage'

const emit = defineEmits(['success'])
const message = useMessage()

const dialogVisible = ref(false)
const dialogTitle = ref('导入账号')
const tableView = ref(false)
const formData = reactive({
  data: '',
})
const importSettings = reactive({
  groupId: null as number | null,
  proxyId: null as number | null,
})

const groupList = ref<any[]>([])
const proxyList = ref<SysProxyRespVO[]>([])
const previewList = ref<FbAccountImportPreviewVO[]>([])

const open = () => {
  dialogVisible.value = true
  dialogTitle.value = '导入账号'
  tableView.value = false
  formData.data = ''
  importSettings.groupId = null
  importSettings.proxyId = null
  previewList.value = []
}
defineExpose({ open })

const loadGroups = async () => {
  try {
    const data = await AccountGroupApi.getAllEnabledGroups()
    groupList.value = data || []
  } catch (error) {
    console.error('加载分组失败:', error)
  }
}

const loadProxies = async () => {
  try {
    const data = await SysProxyApi.getAllEnabledProxyList()
    proxyList.value = data || []
  } catch (error) {
    console.error('加载代理失败:', error)
  }
}

const handleNext = () => {
  if (!formData.data.trim()) {
    message.warning('请输入账号数据')
    return
  }

  previewList.value = []
  const lines = formData.data.split('\n')
  let lineNum = 1

  for (const line of lines) {
    const trimmedLine = line.trim()
    if (!trimmedLine) continue

    const parts = trimmedLine.split('----')
    const userName = parts.length > 0 ? parts[0].trim() : ''
    const password = parts.length > 1 ? parts[1].trim() : ''
    const securityKey = parts.length > 2 ? parts[2].trim() : ''

    let error = ''
    if (!userName) {
      error = '缺少用户名'
    } else if (!password) {
      error = '缺少密码'
    }

    previewList.value.push({
      no: lineNum,
      userName,
      password,
      securityKey,
      error,
    })
    lineNum++
  }

  tableView.value = true
}

const handleImport = async () => {
  const validItems = previewList.value.filter(item => !item.error)
  if (validItems.length === 0) {
    message.warning('没有有效的账号数据')
    return
  }

  try {
    const data: FbAccountImportReqVO = {
      data: formData.data,
      groupId: importSettings.groupId,
      proxyId: importSettings.proxyId,
    }
    await FbAccountApi.importFbAccount(data)
    message.success('导入成功')
    dialogVisible.value = false
    emit('success')
  } catch (error) {
    message.error('导入失败')
  }
}

onMounted(() => {
  loadGroups()
  loadProxies()
})
</script>