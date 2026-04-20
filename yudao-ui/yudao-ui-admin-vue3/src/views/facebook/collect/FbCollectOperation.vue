<template>
  <div class="app-container">
    <el-row :gutter="20">
      <!-- 左侧：功能卡片 -->
      <el-col :span="6">
        <ContentWrap>
          <div class="function-section">
            <h3 class="section-title">采集功能</h3>
            <div class="function-grid">
              <FunctionCard
                v-for="(func, index) in functions"
                :key="index"
                :title="func.title"
                :icon="func.icon"
                :description="func.description"
                :active="activeFunction === func.type"
                :disabled="func.disabled"
                @click="selectFunction(func.type)"
              />
            </div>
          </div>
        </ContentWrap>
      </el-col>

      <!-- 右侧：任务列表 -->
      <el-col :span="18">
        <TaskList ref="taskListRef" />
      </el-col>
    </el-row>

    <!-- 新建任务对话框 -->
    <Dialog title="新建采集任务" v-model="dialogVisible" width="600px">
      <el-form :model="taskForm" label-width="100px">
        <el-form-item label="采集账号">
          <el-select
            v-model="taskForm.accountIds"
            multiple
            placeholder="请选择账号"
            style="width: 100%"
          >
            <el-option
              v-for="account in accounts"
              :key="account.id"
              :label="account.fbAccount + (account.remark ? '(' + account.remark + ')' : '')"
              :value="account.id"
            />
          </el-select>
        </el-form-item>

        <el-form-item label="期望数量">
          <el-input-number
            v-model="taskForm.expectedCount"
            :min="1"
            :max="10000"
            style="width: 100%"
          />
        </el-form-item>

        <el-form-item label="采集链接">
          <el-input
            v-model="taskForm.urls"
            type="textarea"
            :rows="4"
            placeholder="每行一个链接"
          />
        </el-form-item>

        <el-form-item label="采集间隔(秒)">
          <el-input-number
            v-model="taskForm.interval"
            :min="1"
            :max="300"
            style="width: 100%"
          />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">取 消</el-button>
        <el-button type="primary" @click="submitTask" :loading="submitting">确 定</el-button>
      </template>
    </Dialog>
  </div>
</template>

<script setup lang="ts">
import { FbCollectApi } from '@/api/facebook/collect'
import { FbAccountApi, type FbAccount } from '@/api/facebook/account'
import FunctionCard from './components/FunctionCard.vue'
import TaskList from './components/TaskList.vue'

defineOptions({ name: 'FbCollectOperation' })

const message = useMessage()

// 功能列表
const functions = [
  {
    type: 'pages',
    title: '主页采集',
    icon: 'ep:user',
    description: '采集Facebook主页信息',
    disabled: false
  },
  {
    type: 'posts',
    title: '帖子采集',
    icon: 'ep:document',
    description: '采集Facebook帖子内容',
    disabled: false
  },
  {
    type: 'users',
    title: '用户采集',
    icon: 'ep:user-filled',
    description: '采集Facebook用户资料',
    disabled: false
  },
  {
    type: 'groups',
    title: '群组采集',
    icon: 'ep:user',
    description: '采集Facebook群组信息',
    disabled: true
  },
  {
    type: 'events',
    title: '活动采集',
    icon: 'ep:calendar',
    description: '采集Facebook活动信息',
    disabled: true
  },
  {
    type: 'comments',
    title: '评论采集',
    icon: 'ep:chat-dot-round',
    description: '采集帖子评论内容',
    disabled: true
  }
]

const activeFunction = ref('pages')
const dialogVisible = ref(false)
const submitting = ref(false)
const taskListRef = ref()

// 账号列表
const accounts = ref<FbAccount[]>([])

// 任务表单
const taskForm = reactive({
  accountIds: [] as number[],
  expectedCount: 100,
  urls: '',
  interval: 5
})

/** 选择功能 */
const selectFunction = (type: string) => {
  if (functions.find((f) => f.type === type)?.disabled) {
    message.warning('该功能开发中')
    return
  }
  activeFunction.value = type
  dialogVisible.value = true
}

/** 加载账号列表 */
const loadAccounts = async () => {
  try {
    const data = await FbAccountApi.getFbAccountPage({
      pageNo: 1,
      pageSize: 1000,
      status: true
    })
    accounts.value = data.list || []
  } catch (error) {
    console.error('加载账号列表失败:', error)
  }
}

/** 提交任务 */
const submitTask = async () => {
  // 验证
  if (!taskForm.accountIds || taskForm.accountIds.length === 0) {
    message.warning('请选择采集账号')
    return
  }

  if (!taskForm.urls || taskForm.urls.trim() === '') {
    message.warning('请输入采集链接')
    return
  }

  submitting.value = true
  try {
    // 解析URL列表
    const urls = taskForm.urls
      .split('\n')
      .map((url) => url.trim())
      .filter((url) => url.length > 0)

    if (urls.length === 0) {
      message.warning('请输入有效的采集链接')
      return
    }

    // 获取选中的账号信息
    const selectedAccounts = accounts.value.filter((acc) =>
      taskForm.accountIds.includes(acc.id)
    )

    // 为每个账号创建采集任务
    let createdCount = 0
    for (const account of selectedAccounts) {
      for (const url of urls) {
        const taskData: any = {
          fbAccount: account.fbAccount || '',
          taskType: getTaskTypeByFunction(activeFunction.value),
          searchUrl: url,
          searchType: 0, // 0表示链接搜索
          expectedCount: taskForm.expectedCount,
          intervalSeconds: taskForm.interval,
          status: 0, // 待执行
          collectedCount: 0,
          remark: `自动化采集-${activeFunction.value}`
        }

        const result = await FbCollectApi.createFbCollect(taskData)

        // 调用WPF启动浏览器进行采集
        if (result && result.data) {
          const taskId = result.data
          await startBrowserCollection(account, url, taskId)
          createdCount++
        }
      }
    }

    message.success(`已创建 ${createdCount} 个采集任务`)

    // 刷新任务列表
    if (taskListRef.value) {
      taskListRef.value.refresh()
    }

    // 关闭对话框并重置表单
    dialogVisible.value = false
    resetForm()
  } catch (error) {
    console.error('创建任务失败:', error)
    message.error('创建任务失败')
  } finally {
    submitting.value = false
  }
}

/** 根据功能类型获取任务类型 */
const getTaskTypeByFunction = (funcType: string): number => {
  const typeMap: Record<string, number> = {
    pages: 1,
    posts: 2,
    users: 3,
    groups: 4,
    events: 5,
    comments: 6
  }
  return typeMap[funcType] || 1
}

/** 启动浏览器采集 */
const startBrowserCollection = async (
  account: FbAccount,
  url: string,
  taskId: number
) => {
  try {
    message.info(`正在启动指纹浏览器...`)

    // 调用WPF桥接，打开浏览器矩阵窗口并启动自动化采集
    if ((window as any).chrome?.webview?.hostObjects?.sync?.wpfBridge) {
      ;(window as any).chrome.webview.hostObjects.sync.wpfBridge.StartBrowser(
        account.id,
        account.cookie || null,
        url,
        taskForm.expectedCount
      )
      message.info(`已为账号 ${account.fbAccount} 启动自动化采集，请稍后...`)
    } else {
      message.warning('WPF桥接未就绪，请在WPF环境中运行')
    }
  } catch (error) {
    console.error('启动浏览器失败:', error)
    message.error(`启动浏览器失败: ${(error as Error).message}`)
  }
}

/** 重置表单 */
const resetForm = () => {
  taskForm.accountIds = []
  taskForm.urls = ''
  taskForm.expectedCount = 100
  taskForm.interval = 5
}

/** 初始化 */
onMounted(() => {
  loadAccounts()
})
</script>

<style scoped lang="scss">
.app-container {
  padding: 20px;
}

.function-section {
  .section-title {
    margin: 0 0 16px 0;
    color: var(--el-text-color-primary);
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
      background-color: var(--el-color-primary);
      border-radius: 2px;
    }
  }

  .function-grid {
    display: grid;
    grid-template-columns: repeat(2, 1fr);
    gap: 12px;
  }
}
</style>
