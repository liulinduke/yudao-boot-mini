<template>
  <Dialog :title="dialogTitle" v-model="dialogVisible" width="700px">
    <div v-if="!tableView">
      <el-alert
        title="新账号需进行3天以上的养号才更稳定些，仅进行浏览等基础操作，避免修改资料和执行任务"
        type="warning"
        :closable="false"
        show-icon
        class="mb-4"
      />
      <el-form :model="formData" label-width="100px">
        <el-form-item label="账号数据" prop="data">
          <el-input
            type="textarea"
            v-model="formData.data"
            :rows="12"
            placeholder="请粘贴账号数据"
            class="font-mono text-sm"
          />
        </el-form-item>
        <el-form-item class="format-hint">
          <el-text type="info" size="small">
            <template #default> 格式一：<code>账号----密码</code>（一行一个账号）</template>
          </el-text>
        </el-form-item>
        <el-form-item class="format-hint">
          <el-text type="info" size="small">
            <template #default>
              格式二：<code>账号----密码----双重验证安全码</code>（一行一个账号）</template
            >
          </el-text>
        </el-form-item>
        <el-form-item class="format-hint">
          <el-text type="info" size="small">
            <template #default>
              格式三：<code>账号----密码----Cookie JSON</code>（一行一个账号）</template
            >
          </el-text>
        </el-form-item>
        <el-form-item class="format-hint">
          <el-text type="info" size="small">
            <template #default>
              格式四：<code>账号|密码|2FA|Cookie|Token|邮箱</code>（多个账号可连续粘贴在同一行）</template
            >
          </el-text>
        </el-form-item>
        <el-form-item class="format-hint">
          <el-text type="info" size="small">
            <template #default>
              格式五：<code>Cookie JSON</code>（仅Cookie时一行一个，自动提取账号ID）</template
            >
          </el-text>
        </el-form-item>
      </el-form>
    </div>
    <div v-else>
      <el-table :data="previewList" :max-height="300" border :show-overflow-tooltip="true">
        <el-table-column label="序号" prop="no" width="60" />
        <el-table-column label="Facebook账号" prop="userName" width="120" />
        <el-table-column label="密码" prop="password" />
        <el-table-column label="双重验证安全码" prop="securityKey" width="140" />
        <el-table-column label="Cookie" width="100">
          <template #default="scope">
            <el-tag v-if="scope.row.cookie" type="success" size="small">已提供</el-tag>
            <span v-else class="text-gray-400">未提供</span>
          </template>
        </el-table-column>
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
          <el-select v-model="importSettings.groupId" placeholder="请选择分组" class="w-200px">
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
          <el-select v-model="importSettings.proxyId" placeholder="请选择代理" class="w-200px">
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
import {
  FbAccountApi,
  FbAccountImportReqVO,
  FbAccountImportPreviewVO
} from '@/api/facebook/account'
import { AccountGroupApi } from '@/api/facebook/accountgroup'
import { SysProxyApi, SysProxyRespVO } from '@/api/system/proxy'
import { useMessage } from '@/hooks/web/useMessage'

const emit = defineEmits(['success'])
const message = useMessage()

const dialogVisible = ref(false)
const dialogTitle = ref('导入账号')
const tableView = ref(false)
const formData = reactive({
  data: ''
})
const importSettings = reactive({
  groupId: null as number | null,
  proxyId: null as number | null
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

const normalizePipeRecords = (data: string) => {
  // 供应商可能把多条记录连续放在一行，下一条记录通常以数字账号开头。
  return data.replace(/\|\s*(?=\d{10,}\s*\|)/g, '\n')
}

const parseImportRecords = (data: string) => {
  const lines = normalizePipeRecords(data).split(/\r?\n/)
  return lines
    .map((line) => line.trim())
    .filter(Boolean)
    .map((line) => {
      const trimmedLine = line.trim()
      if (trimmedLine.startsWith('[') || trimmedLine.startsWith('{')) {
        try {
          const json = JSON.parse(trimmedLine)
          const cookies = Array.isArray(json) ? json : [json]
          const cUser = cookies.find((item: any) => item?.name === 'c_user')
          return {
            userName: cUser?.value ? String(cUser.value) : '',
            password: '',
            securityKey: '',
            cookie: trimmedLine
          }
        } catch {
          // 交给预览校验显示缺少账号，避免导入无效 JSON。
        }
      }
      const parts = line.includes('----') ? line.split('----') : line.split('|')
      const third = parts[2]?.trim() || ''
      const thirdIsCookie =
        line.includes('----') && (third.startsWith('[') || third.startsWith('{'))
      return {
        userName: parts[0]?.trim() || '',
        password: parts[1]?.trim() || '',
        securityKey: thirdIsCookie ? '' : third,
        cookie: thirdIsCookie ? third : parts[3]?.trim() || ''
      }
    })
}

const handleNext = () => {
  if (!formData.data.trim()) {
    message.warning('请输入账号数据')
    return
  }

  previewList.value = []
  let lineNum = 1

  for (const record of parseImportRecords(formData.data)) {
    const userName = record.userName
    const password = record.password
    const securityKey = record.securityKey

    let error = ''
    if (!userName) {
      error = '缺少用户名'
    } else if (!password && !record.cookie) {
      error = '缺少密码'
    }

    previewList.value.push({
      no: lineNum,
      userName,
      password,
      securityKey,
      cookie: record.cookie,
      error
    })
    lineNum++
  }

  tableView.value = true
}

const handleImport = async () => {
  const validItems = previewList.value.filter((item) => !item.error)
  if (validItems.length === 0) {
    message.warning('没有有效的账号数据')
    return
  }

  try {
    const data: FbAccountImportReqVO = {
      // 连续粘贴的 | 格式先按账号边界拆成多行，避免后端只收到第一条记录。
      data: normalizePipeRecords(formData.data),
      groupId: importSettings.groupId,
      proxyId: importSettings.proxyId
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

<style scoped>
.format-hint {
  margin-bottom: 4px;
}

.format-hint :deep(.el-form-item__content) {
  min-height: 20px;
  line-height: 20px;
}
</style>
