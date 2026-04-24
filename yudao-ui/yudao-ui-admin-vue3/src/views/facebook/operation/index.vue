<template>
  <div>
    <el-row :gutter="20">
      <!-- 左侧：运营工具 -->
      <el-col :span="8">
        <ContentWrap>
          <div class="operation-section">
            <h3 class="section-title">
              <el-icon :size="20"><Operation /></el-icon>
              <span class="ml-6px">运营工具</span>
            </h3>
            <div class="operation-grid">
              <OperationCard
                v-for="(tool, index) in operationTools"
                :key="index"
                :title="tool.title"
                :icon="tool.icon"
                :active="activeTool === tool.type"
                :disabled="tool.disabled"
                @click="selectTool(tool.type)"
              />
            </div>
          </div>
        </ContentWrap>
      </el-col>

      <!-- 右侧：任务列表 -->
      <el-col :span="16">
        <ContentWrap>
          <div class="task-section">
            <h3 class="section-title">
              <el-icon :size="20"><List /></el-icon>
              <span class="ml-6px">任务列表</span>
            </h3>
          </div>
          <!-- 搜索工作栏 -->
          <el-form
            class="search-form"
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
                <el-option label="链接加组" :value="1" />
                <el-option label="转贴" :value="2" />
                <el-option label="群发私信" :value="3" />
              </el-select>
            </el-form-item>
            <el-form-item label="状态" prop="status">
              <el-select
                v-model="queryParams.status"
                placeholder="请选择状态"
                clearable
                class="!w-140px"
              >
                <el-option label="待执行" :value="0" />
                <el-option label="执行中" :value="1" />
                <el-option label="已完成" :value="2" />
                <el-option label="已停止" :value="3" />
                <el-option label="失败" :value="4" />
              </el-select>
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
              <el-button @click="handleQuery">
                <Icon icon="ep:search" class="mr-5px" /> 搜索
              </el-button>
            </el-form-item>
          </el-form>

          <!-- 列表 -->
          <el-table
            row-key="id"
            v-loading="loading"
            :data="list"
            :stripe="true"
            :show-overflow-tooltip="true"
          >
            <el-table-column label="任务ID" align="center" prop="id" width="80" />
            <el-table-column label="任务类型" align="center" prop="taskType" width="100">
              <template #default="scope">
                <el-tag v-if="scope.row.taskType === 1" type="primary">链接加组</el-tag>
                <el-tag v-else-if="scope.row.taskType === 2" type="success">转贴</el-tag>
                <el-tag v-else-if="scope.row.taskType === 3" type="warning">群发私信</el-tag>
              </template>
            </el-table-column>
            <el-table-column label="任务名称" align="center" prop="taskName" min-width="120" show-overflow-tooltip />
            <el-table-column label="期望/实际" align="center" width="120">
              <template #default="scope">
                {{ scope.row.expectedCount }}/{{ scope.row.actualCount || 0 }}
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
                <el-tag v-if="scope.row.status === 0" type="info">待执行</el-tag>
                <el-tag v-else-if="scope.row.status === 1" type="primary">执行中</el-tag>
                <el-tag v-else-if="scope.row.status === 2" type="success">已完成</el-tag>
                <el-tag v-else-if="scope.row.status === 3" type="warning">已停止</el-tag>
                <el-tag v-else-if="scope.row.status === 4" type="danger">失败</el-tag>
              </template>
            </el-table-column>
            <el-table-column label="开始时间" align="center" prop="startTime" width="160">
              <template #default="scope">
                {{ formatDate(scope.row.startTime) }}
              </template>
            </el-table-column>
            <el-table-column label="结束时间" align="center" prop="endTime" width="160">
              <template #default="scope">
                {{ formatDate(scope.row.endTime) }}
              </template>
            </el-table-column>
            <el-table-column label="操作" align="center" width="120" fixed="right">
              <template #default="scope">
                <el-button link type="primary" @click="openForm('view', scope.row.id)">
                  详情
                </el-button>
                <el-button link type="danger" @click="handleDelete(scope.row.id)">
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
      </el-col>
    </el-row>

    <!-- 表单弹窗：添加/修改 -->
    <FbOperationForm ref="formRef" @success="getList" />
    
    <!-- 转帖表单弹窗 -->
    <RepostForm ref="repostFormRef" @success="getList" />
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
import { Operation, List } from '@element-plus/icons-vue'
import ContentWrap from '@/components/ContentWrap/src/ContentWrap.vue'
import OperationCard from './components/OperationCard.vue'
import FbOperationForm from './FbOperationForm.vue'
import RepostForm from './RepostForm.vue'
import {
  getFbOperationTaskPage,
  deleteFbOperationTask,
  FbOperationTask
} from '@/api/facebook/operation'

defineOptions({ name: 'FbOperation' })

const message = useMessage()
const { t } = useI18n()

// 运营工具列表
const operationTools = [
  {
    type: 'add-group',
    title: '链接加组',
    icon: 'ep:user-filled',
    disabled: false
  },
  {
    type: 'repost',
    title: '转贴',
    icon: 'ep:share',
    disabled: false // 启用转帖功能
  },
  {
    type: 'mass-message',
    title: '群发私信',
    icon: 'ep:message',
    disabled: true
  }
]

const activeTool = ref('')
const loading = ref(true)
const list = ref<FbOperationTask[]>([])
const total = ref(0)
const queryParams = reactive({
  pageNo: 1,
  pageSize: 10,
  taskType: undefined as number | undefined,
  status: undefined as number | undefined,
  createTime: [] as string[]
})
const queryFormRef = ref()

/** 查询列表 */
const getList = async () => {
  loading.value = true
  try {
    const data = await getFbOperationTaskPage(queryParams)
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

/** 选择工具 - 打开新建任务弹窗 */
const selectTool = (type: string) => {
  if (operationTools.find((f) => f.type === type)?.disabled) {
    message.warning('该功能开发中')
    return
  }
  activeTool.value = type
  
  // 如果是转帖，打开转帖表单
  if (type === 'repost') {
    repostFormRef.value.open()
  } else {
    formRef.value.open('create', undefined, getTaskTypeByTool(type))
  }
}

/** 根据工具类型获取任务类型 */
const getTaskTypeByTool = (toolType: string): number => {
  const typeMap: Record<string, number> = {
    'add-group': 1,
    repost: 2,
    'mass-message': 3
  }
  return typeMap[toolType] || 1
}

/** 计算进度 */
const getProgress = (task: FbOperationTask) => {
  if (!task.expectedCount || task.expectedCount === 0) {
    return 0
  }
  return Math.min(100, Math.round(((task.actualCount || 0) / task.expectedCount) * 100))
}

/** 格式化日期 */
const formatDate = (date: string | Date | null | undefined) => {
  if (!date) return '-'
  const d = new Date(date)
  return d.toLocaleString('zh-CN', {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit'
  })
}

/** 搜索按钮操作 */
const handleQuery = () => {
  queryParams.pageNo = 1
  getList()
}

/** 添加/修改操作 */
const formRef = ref()
const repostFormRef = ref()
const openForm = (type: string, id?: number) => {
  formRef.value.open(type, id)
}

/** 删除按钮操作 */
const handleDelete = async (id: number) => {
  try {
    await message.delConfirm()
    await deleteFbOperationTask(id)
    message.success(t('common.delSuccess'))
    await getList()
  } catch {}
}

/** 初始化 */
onMounted(() => {
  getList()
})
</script>

<style scoped lang="scss">
.operation-section {
  .section-title {
    margin: 0 0 16px 0;
    color: var(--el-text-color-primary);
    font-size: 18px;
    font-weight: 600;
    display: flex;
    align-items: center;
    line-height: 1;

    .el-icon {
      display: flex;
      align-items: center;
    }

    span {
      display: flex;
      align-items: center;
    }
  }

  .operation-grid {
    display: grid;
    grid-template-columns: repeat(2, 1fr);
    gap: 12px;
  }
}

.task-section {
  .section-title {
    margin: 0 0 16px 0;
    color: var(--el-text-color-primary);
    font-size: 18px;
    font-weight: 600;
    display: flex;
    align-items: center;
    line-height: 1;

    .el-icon {
      display: flex;
      align-items: center;
    }

    span {
      display: flex;
      align-items: center;
    }
  }
}

// 右侧搜索区域和表格之间的分隔线
:deep(.el-col:last-child) {
  .el-form {
    padding-bottom: 16px;
    border-bottom: 2px solid var(--el-border-color);
  }
}
</style>
