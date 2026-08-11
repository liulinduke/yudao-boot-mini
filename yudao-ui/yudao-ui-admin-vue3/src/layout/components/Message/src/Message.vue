<script lang="ts" setup>
import { formatDate } from '@/utils/formatTime'
import * as NotifyMessageApi from '@/api/system/notify/message'
import { FbMessageApi } from '@/api/facebook/message'
import { useUserStoreWithOut } from '@/store/modules/user'
import { propTypes } from '@/utils/propTypes'
import { useWebSocket } from '@vueuse/core'
import { getRefreshToken } from '@/utils/auth'
import { claimAndStartPendingAiAgentDetails } from '@/utils/wpfAiAgentTaskPoller'
import { getFbAccountProxyJson } from '@/utils/fbAccountProxy'

defineOptions({ name: 'Message' })

const props = defineProps({
  color: propTypes.string.def(''),
  placement: propTypes.string.def('bottom')
})

const { push } = useRouter()
const userStore = useUserStoreWithOut()
const systemUnread = ref(0) // 站内消息未读数量
const list = ref<any[]>([]) // 消息列表
const facebookUnread = reactive({ messenger: 0, notification: 0 })
const facebookUnreadByAccount = reactive<Record<string, { messenger: number; notification: number }>>({})
const unreadCount = computed(
  () => systemUnread.value + facebookUnread.messenger + facebookUnread.notification
)

let claimingMessageMonitors = false
const activeMessageMonitorTimers = new Map<string, number>()
const finishedMessageMonitors = new Set<string>()

// 主界面常驻 WebSocket：等待登录令牌就绪后再连接，避免组件初始化早于登录完成。
// Refresh Token 变化时重建连接，覆盖登录、续期和重新登录场景。
const websocketServer = ref<string | undefined>()
const websocketToken = ref('')
const buildWebsocketServer = (token: string) => {
  if (!token) return undefined
  return `${(import.meta.env.VITE_BASE_URL + '/infra/ws').replace(/^http/, 'ws')}?token=${encodeURIComponent(token)}`
}

const {
  data: websocketData,
  status: websocketStatus,
  close: closeWebsocket,
  open: openWebsocket
} = useWebSocket(websocketServer, {
  immediate: false,
  autoReconnect: true,
  heartbeat: true,
  onConnected: () => console.info('[WebSocket] 主界面连接成功'),
  onDisconnected: () => console.warn('[WebSocket] 主界面连接已断开'),
  onError: () => console.warn('[WebSocket] 主界面连接失败')
})

const syncWebsocket = () => {
  const token = String(getRefreshToken() || '')
  if (!token) {
    if (websocketToken.value) {
      websocketToken.value = ''
      websocketServer.value = undefined
      closeWebsocket()
    }
    return
  }
  if (token === websocketToken.value && websocketStatus.value !== 'CLOSED') return

  const changed = token !== websocketToken.value
  websocketToken.value = token
  websocketServer.value = buildWebsocketServer(token)
  if (changed && websocketStatus.value !== 'CLOSED') closeWebsocket()
  openWebsocket()
}

let websocketTokenTimer: number | undefined
onMounted(() => {
  syncWebsocket()
  websocketTokenTimer = window.setInterval(syncWebsocket, 500)
})

watch(websocketData, (raw) => {
  if (!raw || raw === 'pong') return
  try {
    const message = JSON.parse(raw)
    if (message.type === 'fb-ai-agent-task-ready') {
      void claimAndStartPendingAiAgentDetails()
    }
    if (message.type === 'fb-message-monitor-task-ready') {
      void claimAndStartMessageMonitors()
    }
  } catch (error) {
    console.warn('处理 AI 获客任务 WebSocket 通知失败', error)
  }
})

const claimAndStartMessageMonitors = async () => {
  const bridge = window.chrome?.webview?.hostObjects?.sync?.wpfBridge
  if (claimingMessageMonitors || !bridge?.StartMessageMonitor) return

  const availableSlots = Math.max(Number(bridge.GetAvailableBrowserSlots?.() || 0), 0)
  if (availableSlots <= 0) return

  claimingMessageMonitors = true
  try {
    const monitors = await FbMessageApi.claimMonitor(Math.min(availableSlots, 10))
    for (const item of monitors || []) {
      const monitorId = String(item.monitorId)
      const accountId = String(item.accountId)
      finishedMessageMonitors.delete(monitorId)
      const proxyConfigJson = await getFbAccountProxyJson(accountId)
      bridge.StartMessageMonitor(
        monitorId,
        accountId,
        item.cookie || '',
        String(item.deviceId || ''),
        item.url || 'https://www.facebook.com/',
        item.mode || 'scheduled',
        proxyConfigJson
      )

      const timeout = window.setTimeout(() => {
        if (finishedMessageMonitors.has(monitorId)) return
        finishedMessageMonitors.add(monitorId)
        void FbMessageApi.reportMonitor(item.monitorId, false, '消息检查超过3分钟未回传')
        bridge.CloseMessageBrowserAccount?.(accountId)
        activeMessageMonitorTimers.delete(monitorId)
      }, 180000)
      activeMessageMonitorTimers.set(monitorId, timeout)
    }
  } catch (error) {
    console.warn('领取消息监控任务失败', error)
  } finally {
    claimingMessageMonitors = false
  }
}

const handleMessageMonitorComplete = async (event: Event) => {
  const detail = (event as CustomEvent).detail || {}
  if (!detail.monitorId) return
  const monitorId = String(detail.monitorId)
  if (finishedMessageMonitors.has(monitorId)) return
  finishedMessageMonitors.add(monitorId)
  detail.__reportedByMessage = true

  const timeout = activeMessageMonitorTimers.get(monitorId)
  if (timeout) window.clearTimeout(timeout)
  activeMessageMonitorTimers.delete(monitorId)

  let result: any = {}
  try {
    result = typeof detail.data === 'string' ? JSON.parse(detail.data || '{}') : (detail.data || {})
  } catch {
    result = { success: false, errorMessage: '消息监控结果格式错误' }
  }

  const success = Boolean(result.success)
  try {
    await FbMessageApi.reportMonitor(detail.monitorId, success, result.errorMessage)
    if (success && detail.accountId) {
      await FbMessageApi.reportUnreadBadges({
        accountId: detail.accountId,
        messengerUnreadCount: Number(result.messengerUnreadCount || 0),
        notificationUnreadCount: Number(result.notificationUnreadCount || 0),
        loggedIn: result.loggedIn !== false
      })
    }
    window.dispatchEvent(new CustomEvent('fb:message:monitor-saved', { detail }))
  } catch (error) {
    console.warn('保存消息监控结果失败', error)
  } finally {
    window.chrome?.webview?.hostObjects?.sync?.wpfBridge?.CloseMessageBrowserAccount?.(String(detail.accountId || ''))
    await refreshMessages()
  }
}

const displayUnreadCount = computed(() => unreadCount.value)

const recalculateFacebookUnread = () => {
  facebookUnread.messenger = Object.values(facebookUnreadByAccount).reduce(
    (total, item) => total + item.messenger,
    0
  )
  facebookUnread.notification = Object.values(facebookUnreadByAccount).reduce(
    (total, item) => total + item.notification,
    0
  )
}

const setFacebookUnreadSummaries = (summaries: any[]) => {
  Object.keys(facebookUnreadByAccount).forEach((key) => delete facebookUnreadByAccount[key])
  summaries.forEach((item) => {
    facebookUnreadByAccount[String(item.accountId)] = {
      messenger: Number(item.messengerUnreadCount || 0),
      notification: Number(item.commentUnreadCount || 0)
    }
  })
  recalculateFacebookUnread()
}

const handleFacebookBadgeChanged = (event: Event) => {
  const data = (event as CustomEvent).detail || {}
  if (!data.accountId) return
  facebookUnreadByAccount[String(data.accountId)] = {
    messenger: Number(data.messengerUnreadCount || 0),
    notification: Number(data.notificationUnreadCount || 0)
  }
  recalculateFacebookUnread()
}

// 刷新三类未读消息。打开铃铛只刷新，不自动标记已读。
const refreshMessages = async () => {
  const [systemCount, summaries, unreadList] = await Promise.all([
    NotifyMessageApi.getUnreadNotifyMessageCount(),
    FbMessageApi.getUnreadSummary().catch(() => []),
    NotifyMessageApi.getUnreadNotifyMessageList()
  ])
  systemUnread.value = Number(systemCount || 0)
  list.value = unreadList || []
  setFacebookUnreadSummaries(summaries)
}

const markNotifyRead = async (item: any) => {
  if (item.readStatus) return
  await NotifyMessageApi.updateNotifyMessageRead([item.id])
  list.value = list.value.filter((message) => message.id !== item.id)
  systemUnread.value = Math.max(0, systemUnread.value - 1)
}

// 跳转我的站内信
const goMyList = () => {
  push({
    name: 'MyNotifyMessage'
  })
}

const openFacebookMessageManager = () => {
  const bridge = window.chrome?.webview?.hostObjects?.sync?.wpfBridge
  if (bridge?.OpenMessageManagerWindow) {
    bridge.OpenMessageManagerWindow()
    return
  }
  push({ name: 'FacebookMessage' })
}

// ========== 初始化 =========
let refreshTimer: number | undefined
onMounted(() => {
  window.addEventListener('fb:message:badge-changed', handleFacebookBadgeChanged)
  window.addEventListener('fb:message:monitor-complete', handleMessageMonitorComplete)
  // 首次加载小红点
  refreshMessages()
  // 轮询刷新小红点
  refreshTimer = window.setInterval(
    () => {
      if (userStore.getIsSetUser) {
        refreshMessages()
      } else {
        systemUnread.value = 0
        facebookUnread.messenger = 0
        facebookUnread.notification = 0
        list.value = []
        Object.keys(facebookUnreadByAccount).forEach((key) => delete facebookUnreadByAccount[key])
      }
    },
    1000 * 60 * 2
  )
})

onBeforeUnmount(() => {
  window.removeEventListener('fb:message:badge-changed', handleFacebookBadgeChanged)
  window.removeEventListener('fb:message:monitor-complete', handleMessageMonitorComplete)
  if (refreshTimer) window.clearInterval(refreshTimer)
  activeMessageMonitorTimers.forEach((timer) => window.clearTimeout(timer))
  activeMessageMonitorTimers.clear()
  finishedMessageMonitors.clear()
  if (websocketTokenTimer) window.clearInterval(websocketTokenTimer)
  closeWebsocket()
})
</script>
<template>
  <div class="message">
    <ElPopover :width="380" :placement="props.placement" trigger="click">
      <template #reference>
        <ElBadge :value="displayUnreadCount" :max="99" :hidden="unreadCount === 0" class="item">
          <Icon
            :size="18"
            class="cursor-pointer"
            icon="ep:bell"
            :color="props.color"
            @click="refreshMessages"
          />
        </ElBadge>
      </template>
      <div class="message-panel">
        <div class="message-panel-title">消息中心</div>
        <div class="message-group" @click="openFacebookMessageManager">
          <Icon icon="ep:chat-dot-round" :size="20" />
          <div class="message-group-content">
            <span class="message-group-name">Facebook 消息</span>
            <span class="message-group-desc">私信 {{ facebookUnread.messenger }} 条未读</span>
          </div>
          <ElBadge :value="facebookUnread.messenger" :max="99" :hidden="facebookUnread.messenger === 0" />
        </div>
        <div class="message-group" @click="openFacebookMessageManager">
          <Icon icon="ep:bell" :size="20" />
          <div class="message-group-content">
            <span class="message-group-name">通知消息</span>
            <span class="message-group-desc">Facebook 通知 {{ facebookUnread.notification }} 条未读</span>
          </div>
          <ElBadge
            :value="facebookUnread.notification"
            :max="99"
            :hidden="facebookUnread.notification === 0"
          />
        </div>
        <div class="message-group" @click="goMyList">
          <Icon icon="ep:message" :size="20" />
          <div class="message-group-content">
            <span class="message-group-name">站内消息</span>
            <span class="message-group-desc">系统消息 {{ systemUnread }} 条未读</span>
          </div>
          <ElBadge :value="systemUnread" :max="99" :hidden="systemUnread === 0" />
        </div>
        <el-scrollbar v-if="list.length" class="message-list">
          <div
            v-for="item in list"
            :key="item.id"
            class="message-item"
            @click="markNotifyRead(item)"
          >
            <div class="message-content">
              <span class="message-title">{{ item.templateNickname }}：{{ item.templateContent }}</span>
              <span class="message-date">{{ formatDate(item.createTime) }}</span>
            </div>
          </div>
        </el-scrollbar>
      </div>
    </ElPopover>
  </div>
</template>
<style lang="scss" scoped>
.message-empty {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  height: 260px;
  line-height: 45px;
}

.message-list {
  display: flex;
  max-height: 220px;
  flex-direction: column;

  .message-item {
    display: flex;
    align-items: center;
    padding: 10px 0;
    border-bottom: 1px solid var(--el-border-color-light);

    &:last-child {
      border: none;
    }

    .message-content {
      display: flex;
      flex-direction: column;

      .message-title {
        margin-bottom: 5px;
      }

      .message-date {
        font-size: 12px;
        color: var(--el-text-color-secondary);
      }
    }
  }
}

.message-panel-title {
  margin-bottom: 8px;
  font-size: 15px;
  font-weight: 600;
}

.message-group {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 12px 8px;
  cursor: pointer;
  color: var(--el-text-color-primary);
  border-bottom: 1px solid var(--el-border-color-lighter);

  &:hover {
    background: var(--el-fill-color-light);
  }
}

.message-group-content {
  display: flex;
  flex: 1;
  min-width: 0;
  flex-direction: column;
  gap: 3px;
}

.message-group-name {
  font-size: 14px;
}

.message-group-desc {
  color: var(--el-text-color-secondary);
  font-size: 12px;
}

</style>
