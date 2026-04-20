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
      <el-form-item label="任务类型" prop="taskType">
        <el-select
          v-model="queryParams.taskType"
          placeholder="请选择任务类型"
          clearable
          class="!w-140px"
        >
          <el-option
            v-for="dict in getIntDictOptions(DICT_TYPE.FB_SEARCH_TYPE)"
            :key="dict.value"
            :label="dict.label"
            :value="dict.value"
          />
        </el-select>
      </el-form-item>
      <el-form-item label="状态" prop="status">
        <el-select v-model="queryParams.status" placeholder="请选择状态" clearable class="!w-140px">
          <el-option
            v-for="dict in getIntDictOptions(DICT_TYPE.SYS_COLLECT_STATUS)"
            :key="dict.value"
            :label="dict.label"
            :value="dict.value"
          />
        </el-select>
      </el-form-item>
      <el-form-item label="备注" prop="remark">
        <el-input
          v-model="queryParams.remark"
          placeholder="请输入备注"
          clearable
          @keyup.enter="handleQuery"
          class="!w-140px"
        />
      </el-form-item>
      <el-form-item label="创建时间" prop="createTime">
        <el-date-picker
          v-model="queryParams.createTime"
          value-format="YYYY-MM-DD HH:mm:ss"
          type="daterange"
          start-placeholder="开始日期"
          end-placeholder="结束日期"
          :default-time="[new Date('1 00:00:00'), new Date('1 23:59:59')]"
          class="!w-220px"
        />
      </el-form-item>
      <el-form-item>
        <el-button @click="handleQuery"> <Icon icon="ep:search" class="mr-5px" /> 搜索 </el-button>
        <el-button @click="resetQuery"><Icon icon="ep:refresh" class="mr-5px" /> 重置</el-button>
        <el-button
          type="primary"
          plain
          @click="openForm('create')"
          v-hasPermi="['facebook:fb-collect:create']"
        >
          <Icon icon="ep:plus" class="mr-5px" /> 新增
        </el-button>
        <el-button
          type="danger"
          plain
          :disabled="isEmpty(checkedIds)"
          @click="handleDeleteBatch"
          v-hasPermi="['facebook:fb-collect:delete']"
        >
          <Icon icon="ep:delete" class="mr-5px" /> 批量删除
        </el-button>
      </el-form-item>
    </el-form>
  </ContentWrap>

  <!-- 列表 -->
  <ContentWrap>
    <el-table
      row-key="id"
      v-loading="loading"
      :data="list"
      :stripe="true"
      :show-overflow-tooltip="true"
      @selection-change="handleRowCheckboxChange"
    >
      <el-table-column type="selection" width="55" />
      <el-table-column label="任务类型" align="center" prop="taskType" width="100">
        <template #default="scope">
          <dict-tag :type="DICT_TYPE.FB_SEARCH_TYPE" :value="scope.row.taskType" />
        </template>
      </el-table-column>
      <el-table-column
        label="采集链接"
        align="center"
        prop="searchUrl"
        min-width="200"
        show-overflow-tooltip
      />
      <el-table-column label="期望/已采" align="center" width="120">
        <template #default="scope">
          {{ scope.row.expectedCount }}/{{ scope.row.collectedCount }}
        </template>
      </el-table-column>
      <el-table-column label="进度" align="center" width="120">
        <template #default="scope">
          <el-progress
            :percentage="getProgress(scope.row)"
            :status="scope.row.status === 2 ? 'success' : undefined"
            :stroke-width="15"
          />
        </template>
      </el-table-column>
      <el-table-column label="状态" align="center" prop="status" width="100">
        <template #default="scope">
          <dict-tag :type="DICT_TYPE.SYS_COLLECT_STATUS" :value="scope.row.status" />
        </template>
      </el-table-column>
      <el-table-column
        label="创建时间"
        align="center"
        prop="createTime"
        :formatter="dateFormatter"
        width="160"
      />
      <el-table-column label="操作" align="center" width="150" fixed="right">
        <template #default="scope">
          <el-button
            link
            type="primary"
            @click="openForm('update', scope.row.id)"
            v-hasPermi="['facebook:fb-collect:update']"
          >
            编辑
          </el-button>
          <el-button
            link
            type="danger"
            @click="handleDelete(scope.row.id)"
            v-hasPermi="['facebook:fb-collect:delete']"
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
  </ContentWrap>

  <!-- 表单弹窗：添加/修改 -->
  <FbCollectForm ref="formRef" @success="getList" />
</template>

<script setup lang="ts">
import { getIntDictOptions, DICT_TYPE } from '@/utils/dict'
import { isEmpty } from '@/utils/is'
import { dateFormatter } from '@/utils/formatTime'
import download from '@/utils/download'
import { FbCollectApi, FbCollect } from '@/api/facebook/collect'
import FbCollectForm from './FbCollectForm.vue'

/** FB采集任务 列表 */
defineOptions({ name: 'FbCollect' })

const message = useMessage() // 消息弹窗
const { t } = useI18n() // 国际化

const loading = ref(true) // 列表的加载中
const list = ref<FbCollect[]>([]) // 列表的数据
const total = ref(0) // 列表的总页数
const queryParams = reactive({
  pageNo: 1,
  pageSize: 10,
  fbAccount: undefined,
  taskType: undefined,
  searchUrl: undefined,
  searchType: undefined,
  expectedCount: undefined,
  intervalSeconds: undefined,
  status: undefined,
  collectedCount: undefined,
  errorMessage: undefined,
  startTime: [],
  endTime: [],
  remark: undefined,
  createTime: []
})
const queryFormRef = ref() // 搜索的表单
const exportLoading = ref(false) // 导出的加载中
const checkedIds = ref<number[]>([])
const handleRowCheckboxChange = (records: FbCollect[]) => {
  checkedIds.value = records.map((item) => item.id!)
}

/** 计算进度 */
const getProgress = (task: FbCollect) => {
  if (!task.expectedCount || task.expectedCount === 0) {
    return 0
  }
  return Math.min(100, Math.round((task.collectedCount / task.expectedCount) * 100))
}

/** 查询列表 */
const getList = async () => {
  loading.value = true
  try {
    const data = await FbCollectApi.getFbCollectPage(queryParams)
    console.log('=== API返回数据 ===', data)
    console.log('data.list:', data.list)
    console.log('data.total:', data.total)
    list.value = data.list || []
    total.value = data.total || 0
  } catch (error) {
    console.error('获取列表失败:', error)
    list.value = []
    total.value = 0
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
    await FbCollectApi.deleteFbCollect(id)
    message.success(t('common.delSuccess'))
    // 刷新列表
    await getList()
  } catch {}
}

/** 批量删除FB采集任务 */
const handleDeleteBatch = async () => {
  try {
    // 删除的二次确认
    await message.delConfirm()
    await FbCollectApi.deleteFbCollectList(checkedIds.value)
    checkedIds.value = []
    message.success(t('common.delSuccess'))
    await getList()
  } catch {}
}

/** 导出按钮操作 */
const handleExport = async () => {
  try {
    // 导出的二次确认
    await message.exportConfirm()
    // 发起导出
    exportLoading.value = true
    const data = await FbCollectApi.exportFbCollect(queryParams)
    download.excel(data, 'FB采集任务.xls')
  } catch {
  } finally {
    exportLoading.value = false
  }
}

/** 初始化 **/
onMounted(() => {
  getList()
})
</script>
