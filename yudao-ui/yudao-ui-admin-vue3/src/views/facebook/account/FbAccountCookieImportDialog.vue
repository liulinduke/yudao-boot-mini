<template>
  <Dialog :title="dialogTitle" v-model="dialogVisible" width="700px">
    <div v-if="!tableView">
      <el-alert
        title="新账号需进行24–48小时养号，仅进行浏览等基础操作，避免修改资料和执行任务"
        type="warning"
        :closable="false"
        show-icon
        class="mb-4"
      />
      <el-form :model="formData" label-width="100px">
        <el-form-item label="Cookie数据" prop="data">
          <el-input
            type="textarea"
            v-model="formData.data"
            :rows="12"
            placeholder="请输入Cookie数据，每行一个Cookie"
            class="font-mono text-sm"
          />
        </el-form-item>
        <el-form-item>
          <el-text type="info" size="small">
            <template #default> 导入Cookie，多条Cookie请换行，支持字符串格式、Json格式 </template>
          </el-text>
        </el-form-item>
      </el-form>
    </div>
    <div v-else>
      <el-table :data="previewList" :max-height="250" border :show-overflow-tooltip="true">
        <el-table-column label="序号" prop="no" width="60" />
        <el-table-column label="用户ID" prop="id" />
        <el-table-column label="Cookie" prop="cookie" show-overflow-tooltip />
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
        <!-- <el-form-item>
          <el-checkbox v-model="importSettings.useSessionCookie">
            固定使用此Cookie（永不会刷新）
          </el-checkbox>
        </el-form-item> -->
      </el-form>
    </div>

    <template #footer>
      <template v-if="!tableView">
        <el-button @click="dialogVisible = false">取 消</el-button>
        <el-button type="primary" @click="handleNext">下一步</el-button>
      </template>
      <template v-else>
        <el-button @click="tableView = false">返回编辑</el-button>
        <el-button type="primary" @click="handleImport">导 入</el-button>
      </template>
    </template>
  </Dialog>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
import { Dialog } from '@/components/Dialog'
import {
  FbAccountApi,
  FbAccountCookieImportReqVO,
  FbAccountCookieImportPreviewVO
} from '@/api/facebook/account'
import { AccountGroupApi } from '@/api/facebook/accountgroup'
import { SysProxyApi, SysProxyRespVO } from '@/api/system/proxy'
import { useMessage } from '@/hooks/web/useMessage'

const emit = defineEmits(['success'])
const message = useMessage()

const dialogVisible = ref(false)
const dialogTitle = ref('导入Cookie')
const tableView = ref(false)
const formData = reactive({
  data: ''
})
const importSettings = reactive({
  groupId: null as number | null,
  proxyId: null as number | null,
  useSessionCookie: false
})

const groupList = ref<any[]>([])
const proxyList = ref<SysProxyRespVO[]>([])
const previewList = ref<FbAccountCookieImportPreviewVO[]>([])

const open = () => {
  dialogVisible.value = true
  dialogTitle.value = '导入Cookie'
  tableView.value = false
  formData.data = ''
  importSettings.groupId = null
  importSettings.proxyId = null
  importSettings.useSessionCookie = false
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

const extractUserIdFromCookie = (cookie: string): string | null => {
  try {
    const cUserMatch = cookie.match(/c_user=(\d+)/)
    if (cUserMatch && cUserMatch[1]) {
      return cUserMatch[1]
    }

    try {
      const jsonObj = JSON.parse(cookie)
      if (jsonObj && typeof jsonObj === 'object') {
        if (Array.isArray(jsonObj)) {
          const cUserCookie = jsonObj.find((item: any) => item.name === 'c_user')
          if (cUserCookie) return cUserCookie.value
        } else if (jsonObj.c_user) {
          return String(jsonObj.c_user)
        }
      }
    } catch {}

    return null
  } catch {
    return null
  }
}

const splitCookieEntries = (data: string): string[] => {
  const trimmedData = data.trim()
  if (!trimmedData) return []

  // 浏览器导出的 Cookie JSON 通常是一个完整数组，不能按换行拆开。
  try {
    const json = JSON.parse(trimmedData)
    if (json && typeof json === 'object') {
      return [trimmedData]
    }
  } catch {}

  return data.split(/\r?\n/).map((line) => line.trim()).filter(Boolean)
}

const handleNext = () => {
  if (!formData.data.trim()) {
    message.warning('请输入Cookie数据')
    return
  }

  previewList.value = []
  const lines = splitCookieEntries(formData.data)
  let lineNum = 1

  for (const trimmedLine of lines) {
    if (!trimmedLine) continue

    let error = ''
    let userId = ''

    if (!trimmedLine.includes('=') && !trimmedLine.trimStart().startsWith('[') && !trimmedLine.trimStart().startsWith('{')) {
      error = '无效的Cookie格式'
    } else {
      userId = extractUserIdFromCookie(trimmedLine) || ''
      if (!userId) {
        error = '无法提取用户ID'
      }
    }

    previewList.value.push({
      no: lineNum,
      id: userId,
      cookie: trimmedLine.substring(0, 100) + (trimmedLine.length > 100 ? '...' : ''),
      error
    })
    lineNum++
  }

  tableView.value = true
}

const handleImport = async () => {
  const validItems = previewList.value.filter((item) => !item.error)
  if (validItems.length === 0) {
    message.warning('没有有效的Cookie数据')
    return
  }

  try {
    const data: FbAccountCookieImportReqVO = {
      data: formData.data,
      groupId: importSettings.groupId,
      proxyId: importSettings.proxyId,
      useSessionCookie: importSettings.useSessionCookie
    }
    await FbAccountApi.importFbAccountCookie(data)
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
