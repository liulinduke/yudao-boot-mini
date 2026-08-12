<template>
  <el-dialog v-model="visible" title="养号" width="680px" append-to-body>
    <el-form label-width="110px">
      <el-form-item label="养号动作" required>
        <el-checkbox-group v-model="form.actions">
          <el-checkbox label="feed_scroll">主页随机滚动</el-checkbox>
          <el-checkbox label="safe_click">随机点击页面</el-checkbox>
          <el-checkbox label="friend_profile">浏览好友主页</el-checkbox>
          <el-checkbox label="reels">浏览 Reels</el-checkbox>
        </el-checkbox-group>
      </el-form-item>

      <el-form-item label="运行时长">
        <el-input-number v-model="form.durationMinutes" :min="1" :max="1440" />
        <span class="ml-2 text-gray-500">分钟</span>
      </el-form-item>

      <el-form-item label="页面停留">
        <el-input-number v-model="form.minStaySeconds" :min="3" :max="3600" />
        <span class="mx-2">至</span>
        <el-input-number v-model="form.maxStaySeconds" :min="3" :max="3600" />
        <span class="ml-2 text-gray-500">秒</span>
      </el-form-item>

      <el-form-item v-if="form.actions.includes('friend_profile')" label="好友主页数">
        <el-input-number v-model="form.maxFriendProfiles" :min="1" :max="100" />
      </el-form-item>

      <el-form-item v-if="form.actions.includes('reels')" label="Reels数量">
        <el-input-number v-model="form.maxReels" :min="1" :max="500" />
      </el-form-item>

      <el-form-item v-if="form.actions.includes('reels')" label="随机点赞">
        <el-switch v-model="form.enableLike" />
        <el-input-number
          v-if="form.enableLike"
          v-model="form.likeProbability"
          class="ml-3"
          :min="1"
          :max="100"
        />
        <span v-if="form.enableLike" class="ml-2">%</span>
      </el-form-item>

      <el-form-item label="执行账号">
        <el-tag v-for="account in accounts" :key="String(account.id)" class="mr-2 mb-1">
          {{ account.fbAccount || account.id }}
        </el-tag>
      </el-form-item>

      <el-divider content-position="left">定时养号</el-divider>
      <el-form-item label="执行时间">
        <el-date-picker
          v-model="scheduleTime"
          type="datetime"
          value-format="x"
          placeholder="选择执行时间"
          :disabled-date="disabledScheduleDate"
        />
      </el-form-item>
      <el-form-item label="任务名称">
        <el-input v-model="taskName" maxlength="64" placeholder="可选" />
      </el-form-item>

      <el-divider content-position="left">已创建任务</el-divider>
      <el-empty v-if="!tasks.length" description="暂无定时任务" :image-size="48" />
      <div v-else class="warmup-task-list">
        <div v-for="task in tasks" :key="String(task.id)" class="warmup-task-item">
          <div class="warmup-task-main">
            <div class="warmup-task-title">{{ task.taskName || '养号任务' }}</div>
            <div class="warmup-task-meta">
              {{ formatScheduleTime(task.scheduleTime) }} · {{ task.accountCount }} 个账号 · {{ warmupActionText(task.warmupConfig) }}
            </div>
          </div>
          <el-tag size="small" :type="taskStatusType(task.status)">{{ taskStatusText(task.status) }}</el-tag>
          <el-button
            v-if="task.status === 0 || task.status === 1"
            link
            type="danger"
            @click="handleDeleteTask(task)"
          >删除</el-button>
        </div>
      </div>
    </el-form>

    <template #footer>
      <el-button @click="visible = false">取消</el-button>
      <el-button type="primary" :loading="starting" @click="handleStart">立即养号</el-button>
      <el-button type="success" :loading="scheduling" @click="handleSchedule">创建定时任务</el-button>
    </template>
  </el-dialog>
</template>

<script setup lang="ts">
import { ElMessage } from 'element-plus'
import { startBrowserCollect } from '@/utils/wpfBridge'
import { FbWarmupApi } from '@/api/facebook/warmup'
import dayjs from 'dayjs'

interface AccountItem {
  id: string | number
  fbAccount?: string
  cookie?: string
  deviceId?: string | number
}

const visible = ref(false)
const starting = ref(false)
const scheduling = ref(false)
const accounts = ref<AccountItem[]>([])
const tasks = ref<any[]>([])
// 后端 LocalDateTime 的 Jackson 配置接收毫秒时间戳。
const scheduleTime = ref('')
const taskName = ref('')
const form = reactive({
  actions: ['feed_scroll', 'safe_click'] as string[],
  durationMinutes: 20,
  minStaySeconds: 15,
  maxStaySeconds: 45,
  maxFriendProfiles: 5,
  maxReels: 20,
  enableLike: false,
  likeProbability: 0
})

const open = (selectedAccounts: AccountItem[]) => {
  accounts.value = selectedAccounts
  visible.value = true
  void loadTasks()
}

const disabledScheduleDate = (date: Date) => date.getTime() < Date.now() - 24 * 60 * 60 * 1000

const buildConfig = () => JSON.stringify({
  actions: form.actions,
  durationMinutes: form.durationMinutes,
  minStaySeconds: form.minStaySeconds,
  maxStaySeconds: form.maxStaySeconds,
  maxFriendProfiles: form.maxFriendProfiles,
  maxReels: form.maxReels,
  enableLike: form.enableLike,
  likeProbability: form.enableLike ? form.likeProbability : 0
})

const loadTasks = async () => {
  try {
    const result: any = await FbWarmupApi.page({ pageNo: 1, pageSize: 50 })
    tasks.value = result?.list || []
  } catch (error) {
    console.warn('加载定时养号任务失败', error)
  }
}

const handleWarmupSaved = () => { void loadTasks() }
onMounted(() => window.addEventListener('fb:warmup:saved', handleWarmupSaved))
onBeforeUnmount(() => window.removeEventListener('fb:warmup:saved', handleWarmupSaved))

const warmupActionText = (raw: string) => {
  const labels: Record<string, string> = {
    feed_scroll: '主页浏览',
    safe_click: '随机浏览',
    friend_profile: '好友主页',
    reels: '短视频浏览'
  }
  try {
    const actions = JSON.parse(raw)?.actions || []
    return actions.map((action: string) => labels[action] || '其他操作').join('、') || '未设置动作'
  } catch { return '未设置动作' }
}
const formatScheduleTime = (value: string | number) => {
  const date = dayjs(Number(value))
  return date.isValid() ? date.format('YYYY-MM-DD HH:mm:ss') : '时间未知'
}
const taskStatusText = (status: number) => ({ 0: '等待执行', 1: '待领取', 2: '执行中', 3: '已完成', 4: '失败', 5: '已取消' }[status] || '未知')
const taskStatusType = (status: number) => ({ 0: 'warning', 1: 'info', 2: '', 3: 'success', 4: 'danger', 5: 'info' }[status] || 'info') as any

const validateForm = () => {
  if (!form.actions.length) { ElMessage.warning('请至少选择一个养号动作'); return false }
  if (form.minStaySeconds > form.maxStaySeconds) { ElMessage.warning('最短停留时间不能大于最长停留时间'); return false }
  if (!accounts.value.length) { ElMessage.warning('请先选择账号'); return false }
  return true
}

const handleStart = async () => {
  if (!validateForm()) return

  starting.value = true
  try {
    const config = buildConfig()

    accounts.value.forEach((account) => {
      startBrowserCollect(
        `warmup-${Date.now()}-${String(account.id)}`,
        account.fbAccount || String(account.id),
        account.cookie || null,
        'https://www.facebook.com',
        0,
        17,
        config,
        true,
        account.deviceId == null ? undefined : String(account.deviceId)
      )
    })
    ElMessage.success(`已提交 ${accounts.value.length} 个账号的养号任务`)
    visible.value = false
  } catch (error) {
    console.error('启动养号失败', error)
    ElMessage.error('启动养号失败，请确认 WPF 桥接已连接')
  } finally {
    starting.value = false
  }
}

const handleSchedule = async () => {
  if (!validateForm()) return
  if (!scheduleTime.value || !dayjs(scheduleTime.value).isAfter(dayjs())) {
    ElMessage.warning('请选择晚于当前时间的执行时间')
    return
  }
  scheduling.value = true
  try {
    await FbWarmupApi.create({
      taskName: taskName.value,
      scheduleTime: Number(scheduleTime.value),
      accountIds: accounts.value.map(item => item.id),
      warmupConfig: buildConfig()
    })
    ElMessage.success('定时养号任务已创建')
    scheduleTime.value = ''
    taskName.value = ''
    await loadTasks()
  } catch (error) {
    console.error('创建定时养号任务失败', error)
    ElMessage.error('创建定时养号任务失败')
  } finally { scheduling.value = false }
}

const handleDeleteTask = async (task: any) => {
  try {
    await ElMessageBox.confirm('确定删除这个定时养号任务吗？', '提示', { type: 'warning' })
    await FbWarmupApi.delete(task.id)
    ElMessage.success('任务已删除')
    await loadTasks()
  } catch (error: any) {
    if (error !== 'cancel' && error !== 'close') console.warn('删除定时养号任务失败', error)
  }
}

defineExpose({ open })
</script>

<style scoped>
.warmup-task-list { max-height: 220px; overflow-y: auto; }
.warmup-task-item { display: flex; align-items: center; gap: 12px; padding: 9px 0; border-bottom: 1px solid var(--el-border-color-lighter); }
.warmup-task-main { flex: 1; min-width: 0; }
.warmup-task-title { font-weight: 600; }
.warmup-task-meta { margin-top: 3px; color: var(--el-text-color-secondary); font-size: 12px; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
</style>
