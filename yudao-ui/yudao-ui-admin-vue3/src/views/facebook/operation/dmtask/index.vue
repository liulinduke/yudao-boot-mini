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
      <el-form-item label="任务名称" prop="taskName">
        <el-input
          v-model="queryParams.taskName"
          placeholder="请输入任务名称"
          clearable
          @keyup.enter="handleQuery"
          class="!w-240px"
        />
      </el-form-item>
      <el-form-item label="状态" prop="status">
        <el-select v-model="queryParams.status" placeholder="请选择状态" clearable class="!w-140px">
          <el-option label="待执行" :value="0" />
          <el-option label="执行中" :value="1" />
          <el-option label="已完成" :value="2" />
          <el-option label="失败" :value="3" />
          <el-option label="已取消" :value="4" />
        </el-select>
      </el-form-item>
      <el-form-item>
        <el-button @click="handleQuery"><Icon icon="ep:search" class="mr-5px" /> 搜索</el-button>
        <el-button @click="resetQuery"><Icon icon="ep:refresh" class="mr-5px" /> 重置</el-button>
        <el-button type="primary" plain @click="openForm('create')">
          <Icon icon="ep:plus" class="mr-5px" /> 新建任务
        </el-button>
      </el-form-item>
    </el-form>

    <!-- 列表 -->
    <el-table v-loading="loading" :data="list" :stripe="true" :show-overflow-tooltip="true">
      <el-table-column label="ID" align="center" prop="id" width="80" />
      <el-table-column label="任务名称" align="center" prop="taskName" min-width="150" />
      <el-table-column label="总任务数" align="center" prop="totalCount" width="100" />
      <el-table-column label="已完成" align="center" prop="completedCount" width="100" />
      <el-table-column label="失败数" align="center" prop="failedCount" width="100" />
      <el-table-column label="状态" align="center" prop="status" width="100">
        <template #default="scope">
          <el-tag :type="getStatusType(scope.row.status)">
            {{ getStatusText(scope.row.status) }}
          </el-tag>
        </template>
      </el-table-column>
      <el-table-column label="创建时间" align="center" prop="createTime" width="180" />
      <el-table-column label="操作" align="center" width="200" fixed="right">
        <template #default="scope">
          <el-button 
            v-if="scope.row.status === 0" 
            link 
            type="success" 
            @click="handleStart(scope.row.id)"
          >
            启动
          </el-button>
          <el-button 
            v-if="scope.row.status === 1" 
            link 
            type="warning" 
            @click="handleCancel(scope.row.id)"
          >
            取消
          </el-button>
          <el-button link type="danger" @click="handleDelete(scope.row.id)">删除</el-button>
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

  <!-- 表单弹窗 -->
  <DmTaskForm ref="formRef" @success="getList" />
</template>

<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
import { DmTaskApi } from '@/api/facebook/dmtask'
import { getFbAccountProxyJson } from '@/utils/fbAccountProxy'
import DmTaskForm from './DmTaskForm.vue'
import { useMessage } from '@/hooks/web/useMessage'

const message = useMessage()
const loading = ref(true)
const list = ref([])
const total = ref(0)
const queryFormRef = ref()
const formRef = ref()

const queryParams = reactive({
  pageNo: 1,
  pageSize: 10,
  taskName: '',
  status: undefined
})

/** 查询列表 */
const getList = async () => {
  loading.value = true
  try {
    const data = await DmTaskApi.getDmTaskPage(queryParams)
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
  queryFormRef.value.resetFields()
  handleQuery()
}

/** 添加/修改操作 */
const openForm = (type: string, id?: number) => {
  formRef.value.open(type, id)
}

/** 删除按钮操作 */
const handleDelete = async (id: number) => {
  try {
    await message.delConfirm()
    await DmTaskApi.deleteDmTask(id)
    message.success('删除成功')
    await getList()
  } catch {}
}

/** 启动任务 */
const handleStart = async (id: number) => {
  try {
    await message.confirm('确认启动该任务吗？')
    
    // 1. 调用后端 API，更新任务状态
    await DmTaskApi.startTask(id)
    message.success('任务已启动，正在调用 WPF 执行...')
    await getList()
    
    // 2. 获取任务详情
    const taskDetail = await DmTaskApi.getDmTask(id)
    if (!taskDetail || !taskDetail.details || taskDetail.details.length === 0) {
      message.warning('任务没有明细数据')
      return
    }
    
    console.log(`📨 开始执行私信任务，共 ${taskDetail.details.length} 条消息`)
    
    // 3. 遍历每个明细，调用 WPF 发送私信
    for (let i = 0; i < taskDetail.details.length; i++) {
      const detail = taskDetail.details[i]
      
      // @ts-ignore
      if (window.chrome?.webview?.hostObjects?.sync?.wpfBridge) {
        console.log(`📨 发送第 ${i + 1}/${taskDetail.details.length} 条私信:`, {
          taskId: id,
          detailId: detail.id,
          accountId: detail.accountId,
          fbUserId: detail.targetUserId,
          messageText: detail.scriptContent
        })
        
        // @ts-ignore
        const proxyConfigJson = await getFbAccountProxyJson(detail.accountId)
        window.chrome.webview.hostObjects.sync.wpfBridge.StartDmTask(
          String(id),
          String(detail.id),
          String(detail.accountId),
          detail.cookie || '',
          detail.targetUserId,
          detail.scriptContent,
          detail.password,
          detail.tfa,
          proxyConfigJson
        )
        
        if (i < taskDetail.details.length - 1) {
          const min = taskDetail.minIntervalSeconds || 4
          const max = taskDetail.maxIntervalSeconds || 10
          const waitMs = (Math.min(min, max) + Math.random() * Math.abs(max - min)) * 1000
          await new Promise((resolve) => setTimeout(resolve, waitMs))
        }
      } else {
        console.warn('⚠️ WPF 桥接对象不存在，无法发送私信')
        break
      }
    }
    
    message.success('所有私信已发送到 WPF 队列')
  } catch (error) {
    console.error('启动任务失败:', error)
  }
}

/** 取消任务 */
const handleCancel = async (id: number) => {
  try {
    await message.confirm('确认取消该任务吗？')
    await DmTaskApi.cancelTask(id)
    message.success('任务已取消')
    await getList()
  } catch {}
}

/** 获取状态类型 */
const getStatusType = (status: number) => {
  const types: Record<number, string> = {
    0: 'info',
    1: 'warning',
    2: 'success',
    3: 'danger',
    4: 'info'
  }
  return types[status] || 'info'
}

/** 获取状态文本 */
const getStatusText = (status: number) => {
  const texts: Record<number, string> = {
    0: '待执行',
    1: '执行中',
    2: '已完成',
    3: '失败',
    4: '已取消'
  }
  return texts[status] || '未知'
}

onMounted(() => {
  getList()
})
</script>
