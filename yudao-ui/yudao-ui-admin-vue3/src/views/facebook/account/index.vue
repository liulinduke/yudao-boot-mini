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
      <el-form-item label="密码" prop="password">
        <el-input
          v-model="queryParams.password"
          placeholder="请输入密码"
          clearable
          @keyup.enter="handleQuery"
          class="!w-240px"
        />
      </el-form-item>
      <el-form-item label="地区" prop="area">
        <el-input
          v-model="queryParams.area"
          placeholder="请输入地区"
          clearable
          @keyup.enter="handleQuery"
          class="!w-240px"
        />
      </el-form-item>
      <el-form-item label="好友数" prop="friends">
        <el-input
          v-model="queryParams.friends"
          placeholder="请输入好友数"
          clearable
          @keyup.enter="handleQuery"
          class="!w-240px"
        />
      </el-form-item>
          <el-form-item>
            <el-button @click="handleQuery"><Icon icon="ep:search" class="mr-5px" /> 搜索</el-button>
            <el-button @click="resetQuery"><Icon icon="ep:refresh" class="mr-5px" /> 重置</el-button>
            <el-button
              type="primary"
              plain
              @click="openForm('create')"
              v-hasPermi="['facebook:fb-account:create']"
            >
              <Icon icon="ep:plus" class="mr-5px" /> 新增
            </el-button>
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
          </el-form-item>
        </el-form>

        <!-- 列表 -->
        <div class="flex-1 mt-4">
          <el-table
        row-key="id"
        v-loading="loading"
        :data="list"
        :stripe="true"
        :show-overflow-tooltip="true"
        @selection-change="handleRowCheckboxChange"
    >
    <el-table-column type="selection" width="55" />
      <el-table-column label="id" align="center" prop="id" />
      <el-table-column label="FB账号" align="center" prop="fbAccount" />
      <el-table-column label="密码" align="center" prop="password" />
      <el-table-column label="地区" align="center" prop="area" />
      <el-table-column label="好友数" align="center" prop="friends" />
          <el-table-column label="账户分组" align="center" prop="groupName" />
          <el-table-column label="账户状态" align="center" prop="status" />
      <el-table-column label="备注" align="center" prop="remark" />
      <el-table-column label="cookie" align="center" prop="cookie" />
      <el-table-column label="用户代理" align="center" prop="userAgent" />
      <el-table-column label="2FA" align="center" prop="tfa" />
      <el-table-column label="邮件信息" align="center" prop="email" />
      <el-table-column label="邮箱密码" align="center" prop="emailPassword" />
      <el-table-column label="设备ID" align="center" prop="deviceId" />
      <el-table-column label="设备名称" align="center" prop="deviceName" />
      <el-table-column label="异常原因" align="center" prop="reason" />
      <el-table-column label="代理" align="center" prop="proxy" />
      <el-table-column label="代理ID" align="center" prop="proxyId" />
      <el-table-column
        label="注册日期"
        align="center"
        prop="creationDate"
        :formatter="dateFormatter"
        width="180px"
      />
      <el-table-column
        label="创建时间"
        align="center"
        prop="createTime"
        :formatter="dateFormatter"
        width="180px"
      />
      <el-table-column label="操作" align="center" min-width="120px">
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
</template>

<script setup lang="ts">
import { isEmpty } from '@/utils/is'
import { dateFormatter } from '@/utils/formatTime'
import download from '@/utils/download'
import { FbAccountApi, FbAccount } from '@/api/facebook/account'
import { AccountGroupApi } from '@/api/facebook/accountgroup'
import FbAccountForm from './FbAccountForm.vue'
import AccountGroupForm from '../accountgroup/AccountGroupForm.vue'
import { useMessage } from '@/hooks/web/useMessage'
import { useI18n } from '@/hooks/web/useI18n'

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
  password: undefined,
  area: undefined,
  friends: undefined,
  groupId: undefined as number | null | undefined,
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
  creationDate: [],
  createTime: [],
})
const queryFormRef = ref() // 搜索的表单
const exportLoading = ref(false) // 导出的加载中

// 分组相关
const groupList = ref<any[]>([])
const selectedGroupId = ref<number | null>(null)
const groupFormRef = ref()

/** 查询列表 */
const getList = async () => {
  loading.value = true
  try {
    const data = await FbAccountApi.getFbAccountPage(queryParams)
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

/** 选择分组 */
const handleSelectGroup = (groupId: number | null) => {
  selectedGroupId.value = groupId
  queryParams.groupId = groupId
  handleQuery()
}

/** 搜索按钮操作 */
const handleQuery = () => {
  queryParams.pageNo = 1
  getList()
}

/** 重置按钮操作 */
const resetQuery = () => {
  queryFormRef.value.resetFields()
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
    // 删除的二次确认
    await message.delConfirm()
    // 发起删除
    await FbAccountApi.deleteFbAccount(id)
    message.success(t('common.delSuccess'))
    // 刷新列表
    await getList()
  } catch {}
}

/** 批量删除FB账号 */
const handleDeleteBatch = async () => {
  try {
    // 删除的二次确认
    await message.delConfirm()
    await FbAccountApi.deleteFbAccountList(checkedIds.value);
    checkedIds.value = [];
    message.success(t('common.delSuccess'))
    await getList();
  } catch {}
}

const checkedIds = ref<number[]>([])
const handleRowCheckboxChange = (records: FbAccount[]) => {
  checkedIds.value = records.map((item) => item.id!);
}

/** 导出按钮操作 */
const handleExport = async () => {
  try {
    // 导出的二次确认
    await message.exportConfirm()
    // 发起导出
    exportLoading.value = true
    const data = await FbAccountApi.exportFbAccount(queryParams)
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
    // 如果删除的是当前选中的分组，切换到全部
    if (selectedGroupId.value === id) {
      selectedGroupId.value = null
      queryParams.groupId = null
      await getList()
    }
  } catch {}
}

/** 初始化 **/
onMounted(() => {
  loadGroups()
  getList()
})
</script>