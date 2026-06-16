
<template>
  <ContentWrap>
    <!-- 搜索工作栏 -->
    <el-form
      class="-mb-15px"
      :model="queryParams"
      ref="queryFormRef"
      :inline="true"
      label-width="68px"
    >
      <el-form-item label="代理名称" prop="proxyName">
        <el-input
          v-model="queryParams.proxyName"
          placeholder="请输入代理名称"
          clearable
          @keyup.enter="handleQuery"
          class="!w-240px"
        />
      </el-form-item>
      <el-form-item label="代理类型" prop="proxyType">
        <el-select
          v-model="queryParams.proxyType"
          placeholder="请选择代理类型"
          clearable
          class="!w-160px"
        >
          <el-option :value="1" label="HTTP" />
          <el-option :value="2" label="HTTPS" />
          <el-option :value="3" label="SOCKS5" />
        </el-select>
      </el-form-item>
      <el-form-item label="状态" prop="status">
        <el-select
          v-model="queryParams.status"
          placeholder="请选择状态"
          clearable
          class="!w-120px"
        >
          <el-option :value="1" label="启用" />
          <el-option :value="0" label="禁用" />
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
        v-hasPermi="['system:proxy:create']"
      >
        <Icon icon="ep:plus" class="mr-5px" /> 新增
      </el-button>
      <el-button
        type="danger"
        plain
        :disabled="isEmpty(checkedIds)"
        @click="handleDeleteBatch"
        v-hasPermi="['system:proxy:delete']"
      >
        <Icon icon="ep:delete" class="mr-5px" /> 批量删除
      </el-button>
    </div>

    <!-- 列表 -->
    <div class="mt-4">
      <el-table
        row-key="id"
        v-loading="loading"
        :data="list"
        :stripe="true"
        :show-overflow-tooltip="true"
        @selection-change="handleRowCheckboxChange"
        style="width: 100%;"
      >
        <el-table-column type="selection" width="55" />
        <el-table-column label="代理名称" align="center" prop="proxyName" />
        <el-table-column label="代理类型" align="center" prop="proxyTypeName" />
        <el-table-column label="服务器地址" align="center" prop="host" />
        <el-table-column label="端口" align="center" prop="port" />
        <el-table-column label="用户名" align="center" prop="username" />
        <el-table-column label="国家/地区" align="center" prop="country" />
        <el-table-column label="状态" align="center" prop="status">
          <template #default="scope">
            <el-tag :type="scope.row.status === 1 ? 'success' : 'danger'" size="small">
              {{ scope.row.statusName }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column label="备注" align="center" prop="remark" />
        <el-table-column
          label="创建时间"
          align="center"
          prop="createTime"
          width="180px"
        />
        <el-table-column label="操作" align="center" min-width="120px">
          <template #default="scope">
            <el-button
              link
              type="primary"
              @click="openForm('update', scope.row.id)"
              v-hasPermi="['system:proxy:update']"
            >
              编辑
            </el-button>
            <el-button
              link
              type="danger"
              @click="handleDelete(scope.row.id)"
              v-hasPermi="['system:proxy:delete']"
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
  </ContentWrap>

  <!-- 表单弹窗：添加/修改 -->
  <SysProxyForm ref="formRef" @success="getList" />
</template>

<script setup lang="ts">
import { isEmpty } from '@/utils/is'
import { SysProxyApi, SysProxyRespVO } from '@/api/system/proxy'
import SysProxyForm from './SysProxyForm.vue'
import { useMessage } from '@/hooks/web/useMessage'
import { useI18n } from '@/hooks/web/useI18n'

/** 代理管理 */
defineOptions({ name: 'SysProxy' })

const message = useMessage()
const { t } = useI18n()

const loading = ref(true)
const list = ref<SysProxyRespVO[]>([])
const total = ref(0)
const queryParams = reactive({
  pageNo: 1,
  pageSize: 10,
  proxyName: undefined,
  proxyType: undefined,
  host: undefined,
  status: undefined,
  country: undefined,
})
const queryFormRef = ref()
const checkedIds = ref<number[]>([])

/** 查询列表 */
const getList = async () => {
  loading.value = true
  try {
    const data = await SysProxyApi.getProxyPage(queryParams)
    list.value = data.list
    total.value = data.total
  } finally {
    loading.value = false
  }
}

/** 搜索按钮操作 */
const handleQuery = () => {
  queryParams.pageNo = 1
  getList()
}

/** 重置按钮操作 */
const resetQuery = () => {
  queryFormRef.value?.resetFields()
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
    await SysProxyApi.deleteProxy(id)
    message.success(t('common.delSuccess'))
    await getList()
  } catch {}
}

/** 批量删除 */
const handleDeleteBatch = async () => {
  try {
    await message.delConfirm()
    for (const id of checkedIds.value) {
      await SysProxyApi.deleteProxy(id)
    }
    checkedIds.value = []
    message.success(t('common.delSuccess'))
    await getList()
  } catch {}
}

const handleRowCheckboxChange = (records: SysProxyRespVO[]) => {
  checkedIds.value = records.map((item) => item.id)
}

/** 初始化 */
onMounted(() => {
  getList()
})
</script>
