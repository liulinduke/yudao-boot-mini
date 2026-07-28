<template>
  <ContentWrap>
    <div class="flex gap-4" style="height: calc(100vh - 200px);">
      <!-- 左侧：账号分组 -->
      <div class="w-250px flex-shrink-0">
        <el-card class="h-full" body-style="padding: 10px;">
          <template #header>
            <div class="flex justify-between items-center">
              <span class="font-bold">账号分组</span>
              <el-button type="primary" size="small" @click="openGroupForm('create')">
                <Icon icon="ep:plus" />
              </el-button>
            </div>
          </template>
          
          <el-scrollbar height="calc(100% - 50px)">
            <div
              v-for="group in groupList"
              :key="group.id"
              class="group-item p-2 rounded cursor-pointer mb-2 transition-all"
              :class="{ 'bg-blue-50 border-blue-400': selectedGroupId === group.id, 'hover:bg-gray-50 border-transparent': selectedGroupId !== group.id }"
              @click="handleSelectGroup(group.id)"
            >
              <div class="flex justify-between items-center">
                <span class="text-sm">{{ group.groupName }}</span>
                <div class="flex gap-1">
                  <el-button
                    link
                    type="primary"
                    size="small"
                    @click.stop="openGroupForm('update', group.id)"
                  >
                    <Icon icon="ep:edit" />
                  </el-button>
                  <el-button
                    link
                    type="danger"
                    size="small"
                    @click.stop="handleDeleteGroup(group.id)"
                  >
                    <Icon icon="ep:delete" />
                  </el-button>
                </div>
              </div>
              <div class="text-xs text-gray-400 mt-1">
                {{ group.description || '暂无描述' }}
              </div>
            </div>
            
            <div
              v-if="!selectedGroupId"
              class="group-item p-2 rounded cursor-pointer mb-2 bg-blue-50 border-blue-400"
              @click="handleSelectGroup(null)"
            >
              <div class="flex justify-between items-center">
                <span class="text-sm font-bold">全部账号</span>
              </div>
            </div>
          </el-scrollbar>
        </el-card>
      </div>

      <!-- 右侧：账号列表 -->
      <div class="flex-1 flex flex-col">
        <!-- 搜索工作栏 -->
        <el-form
          class="-mb-15px"
          :model="queryParams"
          ref="queryFormRef"
          :inline="true"
          label-width="68px"
        >
          <el-form-item label="FB账号" prop="fbAccount">
            <el-input
              v-model="queryParams.fbAccount"
              placeholder="请输入FB账号"
              clearable
              @keyup.enter="handleQuery"
              class="!w-240px"
            />
          </el-form-item>
          <el-form-item label="代理" prop="proxyId">
            <el-select
              v-model="queryParams.proxyId"
              placeholder="请选择代理"
              clearable
              class="!w-200px"
            >
              <el-option :value="null" label="全部代理" />
              <el-option
                v-for="proxy in proxyList"
                :key="proxy.id"
                :value="proxy.id"
                :label="proxy.proxyName"
              />
            </el-select>
          </el-form-item>
          <el-form-item>
            <el-button @click="handleQuery"><Icon icon="ep:search" class="mr-5px" /> 搜索</el-button>
            <el-button @click="resetQuery"><Icon icon="ep:refresh" class="mr-5px" /> 重置</el-button>
          </el-form-item>
        </el-form>
        
        <!-- 操作按钮栏 -->
        <div class="mt-2 mb-2 flex gap-2 flex-wrap">
          <el-button
            type="primary"
            plain
            @click="openForm('create')"
            v-hasPermi="['facebook:fb-account:create']"
          >
            <Icon icon="ep:plus" class="mr-5px" /> 新增
          </el-button>
          
          <el-dropdown trigger="click" @command="handleImportCommand">
            <el-button type="primary" plain>
              <Icon icon="ep:download" class="mr-5px" /> 导入
              <Icon icon="ep:arrow-down" />
            </el-button>
            <template #dropdown>
              <el-dropdown-menu>
                <el-dropdown-item command="import">
                  <Icon icon="ep:user" class="mr-5px" /> 导入账号
                </el-dropdown-item>
                <el-dropdown-item command="cookie">
                  <Icon icon="ep:key" class="mr-5px" /> 导入Cookie
                </el-dropdown-item>
              </el-dropdown-menu>
            </template>
          </el-dropdown>
          
          <el-button
            type="success"
            plain
            @click="handleExport"
            :loading="exportLoading"
            v-hasPermi="['facebook:fb-account:export']"
          >
            <Icon icon="ep:download" class="mr-5px" /> 导出
          </el-button>
          <el-button
            type="danger"
            plain
            :disabled="isEmpty(checkedIds)"
            @click="handleDeleteBatch"
            v-hasPermi="['facebook:fb-account:delete']"
          >
            <Icon icon="ep:delete" class="mr-5px" /> 批量删除
          </el-button>
          <el-button
            type="warning"
            plain
            :disabled="isEmpty(checkedIds)"
            @click="openBatchUpdateProxyDialog"
          >
            <Icon icon="ep:setting" class="mr-5px" /> 批量修改代理
          </el-button>
          <el-button
            type="primary"
            plain
            :disabled="isEmpty(checkedIds) || loginRunning"
            @click="handleBatchLogin"
          >
            <Icon icon="ep:promotion" class="mr-5px" /> 登录
          </el-button>
        </div>

        <!-- 列表 -->
        <div class="flex-1 mt-4 overflow-auto">
          <el-table
            row-key="id"
            v-loading="loading"
            :data="list"
            :stripe="true"
            :show-overflow-tooltip="true"
            @selection-change="handleRowCheckboxChange"
            style="width: 100%; min-width: 1000px;"
          >
            <el-table-column type="selection" width="55" />
            <el-table-column label="FB账号" align="center" prop="fbAccount" width="180" />
            <el-table-column label="密码" align="center" prop="password" width="150" />
            <el-table-column label="地区" align="center" prop="area" width="100" />
            <el-table-column label="账户分组" align="center" prop="groupName" width="120" />
            <el-table-column label="代理" align="center" prop="proxyName" width="150">
              <template #default="scope">
                <el-tag v-if="scope.row.proxyName" type="info" size="small">
                  {{ scope.row.proxyName }}
                </el-tag>
                <span v-else class="text-gray-400">-</span>
              </template>
            </el-table-column>
            <el-table-column label="账户状态" align="center" prop="status" width="100" />
            <el-table-column label="备注" align="center" prop="remark" />
            <el-table-column
              label="创建时间"
              align="center"
              prop="createTime"
              :formatter="dateFormatter"
              width="180px"
            />
            <el-table-column label="操作" align="center" width="120">
              <template #default="scope">
                <el-button
                  link
                  type="primary"
                  @click="openForm('update', scope.row.id)"
                  v-hasPermi="['facebook:fb-account:update']"
                >
                  编辑
                </el-button>
                <el-button
                  link
                  type="danger"
                  @click="handleDelete(scope.row.id)"
                  v-hasPermi="['facebook:fb-account:delete']"
                >
                  删除
                </el-button>
              </template>
            </el-table-column>
          </el-table>
          <!-- 分页 -->
          <Pagination
            :total="total"
            v-model:page="queryParams.pageNo"
            v-model:limit="queryParams.pageSize"
            @pagination="getList"
          />
        </div>
      </div>
    </div>
  </ContentWrap>

  <!-- 表单弹窗：添加/修改 -->
  <FbAccountForm ref="formRef" @success="getList" />
  
  <!-- 分组表单弹窗 -->
  <AccountGroupForm ref="groupFormRef" @success="loadGroups" />
  
  <!-- 导入账号弹窗 -->
  <FbAccountImportDialog ref="importDialogRef" @success="getList" />
  
  <!-- 导入Cookie弹窗 -->
  <FbAccountCookieImportDialog ref="cookieImportDialogRef" @success="getList" />
  
  <!-- 批量修改代理弹窗 -->
  <FbAccountBatchUpdateProxyDialog ref="batchUpdateProxyDialogRef" @success="getList" />
</template>

<script setup lang="ts">
import { isEmpty } from '@/utils/is'
import { dateFormatter } from '@/utils/formatTime'
import download from '@/utils/download'
import { FbAccountApi, FbAccount } from '@/api/facebook/account'
import { AccountGroupApi } from '@/api/facebook/accountgroup'
import { SysProxyApi, SysProxyRespVO } from '@/api/system/proxy'
import FbAccountForm from './FbAccountForm.vue'
import AccountGroupForm from '../accountgroup/AccountGroupForm.vue'
import FbAccountImportDialog from './FbAccountImportDialog.vue'
import FbAccountCookieImportDialog from './FbAccountCookieImportDialog.vue'
import FbAccountBatchUpdateProxyDialog from './FbAccountBatchUpdateProxyDialog.vue'
import { useMessage } from '@/hooks/web/useMessage'
import { useI18n } from '@/hooks/web/useI18n'
import {
  startAccountLoginBatch,
  onAccountLoginProgress,
  onAccountLoginComplete,
  type FbAccountLoginBridgePayload,
  type FbAccountLoginBridgeResult
} from '@/utils/wpfBridge'

/** FB账号 列表 */
defineOptions({ name: 'FbAccount' })

const message = useMessage() // 消息弹窗
const { t } = useI18n() // 国际化

const loading = ref(true) // 列表的加载中
const list = ref<FbAccount[]>([]) // 列表的数据
const total = ref(0) // 列表的总页数
const queryParams = reactive({
  pageNo: 1,
  pageSize: 10,
  fbAccount: undefined,
  proxyId: undefined as number | null | undefined,
})
const queryFormRef = ref() // 搜索的表单
const exportLoading = ref(false) // 导出的加载中

// 分组相关
const groupList = ref<any[]>([])
const selectedGroupId = ref<number | null>(null)
const groupFormRef = ref()

// 代理列表
const proxyList = ref<SysProxyRespVO[]>([])

// 导入相关
const importDialogRef = ref()
const cookieImportDialogRef = ref()

// 批量修改代理相关
const batchUpdateProxyDialogRef = ref()
const loginRunning = ref(false)

/** 查询列表 */
const getList = async () => {
  loading.value = true
  try {
    const params = {
      ...queryParams,
      groupId: selectedGroupId.value,
    }
    const data = await FbAccountApi.getFbAccountPage(params)
    list.value = data.list
    total.value = data.total
  } finally {
    loading.value = false
  }
}

/** 加载分组列表 */
const loadGroups = async () => {
  try {
    const data = await AccountGroupApi.getAllEnabledGroups()
    groupList.value = data || []
  } catch (error) {
    console.error('加载分组失败:', error)
  }
}

/** 加载代理列表 */
const loadProxies = async () => {
  try {
    const data = await SysProxyApi.getAllEnabledProxyList()
    proxyList.value = data || []
  } catch (error) {
    console.error('加载代理失败:', error)
  }
}

/** 选择分组 */
const handleSelectGroup = (groupId: number | null) => {
  selectedGroupId.value = groupId
  handleQuery()
}

/** 搜索按钮操作 */
const handleQuery = () => {
  queryParams.pageNo = 1
  getList()
}

/** 重置按钮操作 */
const resetQuery = () => {
  queryFormRef.value?.resetFields()
  queryParams.proxyId = undefined
  handleQuery()
}

/** 添加/修改操作 */
const formRef = ref()
const openForm = (type: string, id?: number) => {
  formRef.value.open(type, id)
}

/** 删除按钮操作 */
const handleDelete = async (id: number) => {
  try {
    await message.delConfirm()
    await FbAccountApi.deleteFbAccount(id)
    message.success(t('common.delSuccess'))
    await getList()
  } catch {}
}

/** 批量删除FB账号 */
const handleDeleteBatch = async () => {
  try {
    await message.delConfirm()
    await FbAccountApi.deleteFbAccountList(checkedIds.value)
    checkedIds.value = []
    message.success(t('common.delSuccess'))
    await getList()
  } catch {}
}

const checkedIds = ref<number[]>([])
const handleRowCheckboxChange = (records: FbAccount[]) => {
  checkedIds.value = records.map((item) => item.id!)
}

const updateAccountLoginState = (result: FbAccountLoginBridgeResult) => {
  const target = list.value.find((item) => item.id === result.accountDbId)
  if (!target) return

  if (result.status === 'running') {
    target.loginStatus = 'RUNNING'
    target.loginErrorReason = ''
  } else if (result.status === 'success') {
    target.loginStatus = 'SUCCESS'
    target.loginErrorReason = ''
  } else if (result.status === 'failed') {
    target.loginStatus = 'FAILED'
    target.loginErrorReason = result.errorReason || '登录失败'
  } else if (result.status === 'skipped') {
    target.loginStatus = 'FAILED'
    target.loginErrorReason = result.errorReason || '缺少登录凭据'
  } else {
    target.loginStatus = 'PENDING'
    target.loginErrorReason = ''
  }
}

const handleBatchLogin = () => {
  const selectedAccounts = list.value.filter((item) => checkedIds.value.includes(item.id!))
  if (!selectedAccounts.length) {
    message.warning('请先选择账号')
    return
  }

  const payload: FbAccountLoginBridgePayload[] = selectedAccounts.map((item) => ({
    id: item.id!,
    accountId: item.fbAccount || '',
    password: item.password,
    tfa: item.tfa,
    cookie: item.cookie || null
  }))

  payload.forEach((item) => {
    const target = list.value.find((account) => account.id === item.id)
    if (target) {
      target.loginStatus = 'PENDING'
      target.loginErrorReason = ''
    }
  })

  loginRunning.value = true
  startAccountLoginBatch(payload)
  message.notifySuccess(`已提交 ${payload.length} 个账号登录`)
}

/** 导出按钮操作 */
const handleExport = async () => {
  try {
    await message.exportConfirm()
    exportLoading.value = true
    const params = {
      ...queryParams,
      groupId: selectedGroupId.value,
    }
    const data = await FbAccountApi.exportFbAccount(params)
    download.excel(data, 'FB账号.xls')
  } catch {
  } finally {
    exportLoading.value = false
  }
}

/** 打开分组表单 */
const openGroupForm = (type: string, id?: number) => {
  groupFormRef.value.open(type, id)
}

/** 删除分组 */
const handleDeleteGroup = async (id: number) => {
  try {
    await message.delConfirm()
    await AccountGroupApi.deleteAccountGroup(id)
    message.success('删除成功')
    await loadGroups()
    if (selectedGroupId.value === id) {
      selectedGroupId.value = null
      await getList()
    }
  } catch {}
}

/** 处理导入命令 */
const handleImportCommand = (command: string) => {
  if (command === 'import') {
    openImportDialog()
  } else if (command === 'cookie') {
    openCookieImportDialog()
  }
}

/** 打开导入账号对话框 */
const openImportDialog = () => {
  importDialogRef.value.open()
}

/** 打开导入Cookie对话框 */
const openCookieImportDialog = () => {
  cookieImportDialogRef.value.open()
}

/** 打开批量修改代理对话框 */
const openBatchUpdateProxyDialog = () => {
  batchUpdateProxyDialogRef.value.open(checkedIds.value)
}

/** 初始化 **/
onMounted(() => {
  loadGroups()
  loadProxies()
  getList()

  onAccountLoginProgress((result) => {
    updateAccountLoginState(result)
  })

  onAccountLoginComplete(async ({ summary, results }) => {
    results.forEach((item) => updateAccountLoginState(item))
    loginRunning.value = false
    await getList()
    message.notifySuccess(`批量登录完成，成功 ${summary.success}，失败 ${summary.failed}，跳过 ${summary.skipped}`)
  })
})
</script>
