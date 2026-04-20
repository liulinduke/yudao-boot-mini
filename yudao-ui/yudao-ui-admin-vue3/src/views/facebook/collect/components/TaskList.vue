<template>
  <ContentWrap>
    <div class="task-header">
      <h3 class="section-title">采集任务列表</h3>
      <el-button type="primary" link @click="refresh">
        <Icon icon="ep:refresh" class="mr-5px" /> 刷新
      </el-button>
    </div>

    <el-table
      :data="tasks"
      v-loading="loading"
      height="400"
      stripe
      style="width: 100%"
    >
      <el-table-column prop="id" label="任务ID" width="280">
        <template #default="scope">
          <span class="task-id">{{ scope.row.id }}</span>
        </template>
      </el-table-column>
      
      <el-table-column prop="taskType" label="类型" width="100">
        <template #default="scope">
          <dict-tag :type="DICT_TYPE.FB_SEARCH_TYPE" :value="scope.row.taskType" />
        </template>
      </el-table-column>
      
      <el-table-column prop="status" label="状态" width="100">
        <template #default="scope">
          <dict-tag :type="DICT_TYPE.SYS_COLLECT_STATUS" :value="scope.row.status" />
        </template>
      </el-table-column>
      
      <el-table-column label="进度" width="150">
        <template #default="scope">
          <el-progress
            :percentage="getProgress(scope.row)"
            :status="scope.row.status === 2 ? 'success' : undefined"
          />
        </template>
      </el-table-column>
      
      <el-table-column prop="collectedCount" label="已采数量" width="100" />
      <el-table-column prop="expectedCount" label="期望数量" width="100" />
      
      <el-table-column prop="searchUrl" label="采集链接" min-width="200" show-overflow-tooltip />
      
      <el-table-column prop="createTime" label="创建时间" width="160">
        <template #default="scope">
          {{ formatTime(scope.row.createTime) }}
        </template>
      </el-table-column>
      
      <el-table-column label="操作" width="120" fixed="right">
        <template #default="scope">
          <el-button link type="primary" size="small" @click="viewDetail(scope.row)">
            详情
          </el-button>
          <el-button 
            link 
            type="danger" 
            size="small" 
            @click="handleDelete(scope.row.id)"
            v-hasPermi="['facebook:fb-collect:delete']"
          >
            删除
          </el-button>
        </template>
      </el-table-column>
    </el-table>

    <!-- 任务详情对话框 -->
    <Dialog title="采集结果详情" v-model="detailVisible" width="80%">
      <el-tabs v-model="activeTab">
        <el-tab-pane label="采集结果" name="results">
          <el-table
            :data="collectResults"
            v-loading="resultLoading"
            max-height="500"
            stripe
          >
            <el-table-column prop="fbUserId" label="FB用户ID" width="150" />
            <el-table-column prop="userName" label="用户名称" width="150" />
            <el-table-column prop="url" label="主页链接" min-width="200" show-overflow-tooltip />
            <el-table-column prop="followers" label="粉丝数" width="100" />
            <el-table-column prop="city" label="所在地" width="120" />
            <el-table-column prop="phonenumber" label="手机" width="120" />
            <el-table-column prop="email" label="邮箱" width="150" />
            <el-table-column prop="syncTime" label="采集时间" width="160">
              <template #default="scope">
                {{ formatTime(scope.row.syncTime) }}
              </template>
            </el-table-column>
          </el-table>
        </el-tab-pane>
      </el-tabs>
    </Dialog>
  </ContentWrap>
</template>

<script setup lang="ts">
import { DICT_TYPE } from '@/utils/dict'
import { FbCollectApi, type FbCollect } from '@/api/facebook/collect'
import { FbCollectUserApi, type FbCollectUser } from '@/api/facebook/collectuser'
import { dateFormatter } from '@/utils/formatTime'

defineOptions({ name: 'TaskList' })

const message = useMessage()
const { t } = useI18n()

const tasks = ref<FbCollect[]>([])
const loading = ref(false)
const detailVisible = ref(false)
const resultLoading = ref(false)
const currentTask = ref<FbCollect | null>(null)
const collectResults = ref<FbCollectUser[]>([])
const activeTab = ref('results')

/** 刷新任务列表 */
const refresh = async () => {
  loading.value = true
  try {
    const data = await FbCollectApi.getFbCollectPage({
      pageNo: 1,
      pageSize: 100
    })
    tasks.value = data.list || []
  } catch (error) {
    console.error('获取任务列表失败:', error)
    message.error('获取任务列表失败')
  } finally {
    loading.value = false
  }
}

/** 查看详情 */
const viewDetail = async (task: FbCollect) => {
  currentTask.value = task
  detailVisible.value = true
  await loadCollectResults(task.id!)
}

/** 加载采集结果 */
const loadCollectResults = async (taskId: number) => {
  resultLoading.value = true
  try {
    const data = await FbCollectUserApi.getFbCollectUserPage({
      pageNo: 1,
      pageSize: 100,
      taskId: taskId
    })
    collectResults.value = data.list || []
  } catch (error) {
    console.error('获取采集结果失败:', error)
    message.error('获取采集结果失败')
  } finally {
    resultLoading.value = false
  }
}

/** 删除任务 */
const handleDelete = async (id: number) => {
  try {
    await message.delConfirm()
    await FbCollectApi.deleteFbCollect(id)
    message.success(t('common.delSuccess'))
    await refresh()
  } catch {}
}

/** 计算进度 */
const getProgress = (task: FbCollect) => {
  if (!task.expectedCount || task.expectedCount === 0) {
    return 0
  }
  return Math.min(100, Math.round((task.collectedCount / task.expectedCount) * 100))
}

/** 格式化时间 */
const formatTime = (time: any) => {
  if (!time) return '-'
  const date = new Date(time)
  return date.toLocaleString('zh-CN', {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit'
  })
}

/** 初始化 */
onMounted(() => {
  refresh()
})

// 暴露refresh方法供父组件调用
defineExpose({
  refresh
})
</script>

<style scoped lang="scss">
.task-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 16px;
}

.section-title {
  margin: 0;
  color: #303133;
  font-size: 16px;
  font-weight: 600;
  position: relative;
  padding-left: 12px;

  &::before {
    content: '';
    position: absolute;
    left: 0;
    top: 50%;
    transform: translateY(-50%);
    width: 4px;
    height: 16px;
    background-color: #409eff;
    border-radius: 2px;
  }
}

.task-id {
  font-family: 'Courier New', monospace;
  color: #409eff;
  font-size: 12px;
}
</style>
