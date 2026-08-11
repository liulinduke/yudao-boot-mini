<template>
  <template v-if="isDetached">
  <ContentWrap class="message-manager">
    <div class="message-toolbar">
      <div class="message-title">Facebook消息管理</div>
      <el-switch v-model="bilingual" active-text="双语显示" />
      <el-button :loading="loading" @click="refreshAll"><Icon icon="ep:refresh" class="mr-5px" />刷新</el-button>
    </div>

    <div class="message-layout">
      <aside class="account-panel">
        <div class="panel-heading">账号</div>
        <el-input v-model="accountKeyword" placeholder="搜索账号" clearable class="mb-10px" />
        <div
          v-for="account in filteredAccounts"
          :key="account.id"
          class="account-row"
          :class="{ active: String(selectedAccount?.id) === String(account.id) }"
          @click="selectAccount(account)"
        >
          <div class="account-main">
            <el-checkbox
              :model-value="enabledAccountIds.includes(String(account.id))"
              @click.stop
              @change="(checked) => toggleAccount(account, Boolean(checked))"
            />
            <span class="status-dot" :class="getAccountStatusClass(account)"></span>
            <span class="account-name">{{ account.fbAccount || account.id }}</span>
          </div>
          <div class="account-meta">
            <el-select
              :model-value="getMonitor(account.id)?.mode || 'disabled'"
              size="small"
              @click.stop
              @change="(mode) => saveMode(account, mode)"
            >
              <el-option label="定时检查" value="scheduled" />
              <el-option label="不接收" value="disabled" />
            </el-select>
            <template v-if="getMonitor(account.id)?.mode === 'scheduled'">
              <el-input
                :model-value="getMonitor(account.id)?.scheduleTimes || '06:00'"
                placeholder="06:00,08:00"
                style="width: 145px"
                size="small"
                @click.stop
                @change="(value) => saveScheduleTimes(account, String(value || '06:00'))"
              />
              <span class="schedule-hint">时间</span>
            </template>
            <el-badge :value="getUnreadCount(account.id)" :hidden="getUnreadCount(account.id) === 0" />
          </div>
          <div v-if="getBrowserState(account.id)" class="account-state">
            {{ getBrowserState(account.id) }}
          </div>
        </div>
        <el-empty v-if="filteredAccounts.length === 0" description="暂无账号" :image-size="70" />
      </aside>

      <section ref="browserPanelRef" class="browser-panel">
        <div class="browser-placeholder">
          <Icon icon="ep:chat-dot-round" :size="34" />
          <span>{{ selectedAccount ? `正在显示 ${selectedAccount.fbAccount || selectedAccount.id} 的 Messenger` : '请选择账号' }}</span>
          <small>Facebook Messenger 原始页面由 WPF 嵌入此区域</small>
        </div>
      </section>

      <aside class="conversation-panel">
        <div class="panel-heading conversation-heading">
          <span>会话</span>
          <el-input v-model="conversationKeyword" placeholder="搜索" clearable size="small" />
        </div>
        <div class="conversation-list">
          <div
            v-for="conversation in conversations"
            :key="conversation.id"
            class="conversation-row"
            :class="{ active: String(selectedConversation?.id) === String(conversation.id) }"
            @click="selectConversation(conversation)"
          >
            <div class="conversation-avatar">{{ (conversation.targetName || '?').slice(0, 1).toUpperCase() }}</div>
            <div class="conversation-body">
              <div class="conversation-line">
                <strong>{{ conversation.targetName || conversation.targetUserId || '未知用户' }}</strong>
                <span>{{ formatTime(conversation.lastMessageTime) }}</span>
              </div>
              <div class="conversation-preview">{{ conversation.lastMessagePreview || '暂无消息' }}</div>
            </div>
            <el-badge v-if="conversation.unreadCount" :value="conversation.unreadCount" />
          </div>
          <el-empty v-if="conversations.length === 0" description="暂无会话" :image-size="70" />
        </div>

        <div v-if="selectedConversation" class="reply-panel">
          <div class="message-history" ref="historyRef">
            <div v-for="item in messages" :key="item.id" class="message-item" :class="item.direction">
              <div class="message-label">{{ item.direction === 'inbound' ? '收到消息' : '已发送' }}</div>
              <div class="message-original">{{ item.originalText }}</div>
              <div v-if="bilingual || item.direction === 'inbound'" class="message-translation">
                {{ item.translatedText || '翻译中...' }}
                <el-button
                  v-if="item.direction === 'inbound' && !item.translatedText"
                  link
                  type="primary"
                  size="small"
                  @click.stop="retryTranslation(item)"
                >重试翻译</el-button>
              </div>
              <div v-if="item.detectedLanguage" class="message-language">{{ item.detectedLanguage }}</div>
            </div>
          </div>
          <div class="reply-composer">
            <div class="composer-label">回复（中文）</div>
            <el-input v-model="replyChinese" type="textarea" :rows="3" placeholder="输入中文回复" @blur="autoTranslate && translateReply()" />
            <div class="composer-target-row">
              <span>目标语言：{{ targetLanguageLabel }}</span>
              <el-switch v-model="autoTranslate" inline-prompt active-text="自动翻译" inactive-text="手动翻译" />
              <el-select v-model="replyTargetLanguage" size="small" @change="autoTranslate && translateReply()">
                <el-option v-for="language in languages" :key="language.value" :label="language.label" :value="language.value" />
              </el-select>
            </div>
            <el-input v-model="replyTranslated" type="textarea" :rows="3" placeholder="目标语言回复，可编辑" />
            <div class="composer-actions">
              <el-button @click="scriptSelectorVisible = true"><Icon icon="ep:collection" class="mr-5px" />话术库</el-button>
              <el-button :loading="translating" @click="translateReply">翻译</el-button>
              <el-button type="primary" :loading="sending" :disabled="!replyTranslated.trim()" @click="sendMessage">
                <Icon icon="ep:promotion" class="mr-5px" />发送
              </el-button>
            </div>
          </div>
        </div>
      </aside>
    </div>
  </ContentWrap>

  <ScriptSelector v-if="scriptSelectorVisible" v-model="scriptSelectorVisible" @confirm="applyScript" />
  </template>
</template>

<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useMessage } from '@/hooks/web/useMessage'
import { FbAccountApi, type FbAccount } from '@/api/facebook/account'
import { getFbAccountProxyJson } from '@/utils/fbAccountProxy'
import { FbMessageApi, type FbMessage, type FbMessageConversation, type FbMessageMonitorAccount } from '@/api/facebook/message'
import ScriptSelector from '@/views/facebook/operation/dmtask/ScriptSelector.vue'
import dayjs from 'dayjs'

const message = useMessage()
const loading = ref(false)
const sending = ref(false)
const bilingual = ref(false)
const accountKeyword = ref('')
const conversationKeyword = ref('')
const accounts = ref<FbAccount[]>([])
const monitors = ref<FbMessageMonitorAccount[]>([])
const conversations = ref<FbMessageConversation[]>([])
const messages = ref<FbMessage[]>([])
const selectedAccount = ref<FbAccount>()
const selectedConversation = ref<FbMessageConversation>()
const replyChinese = ref('')
const replyTranslated = ref('')
const autoTranslate = ref(false)
const translating = ref(false)
const replyTargetLanguage = ref('en')
const scriptSelectorVisible = ref(false)
const browserPanelRef = ref<HTMLElement>()
const historyRef = ref<HTMLElement>()
const enabledAccountIds = ref<string[]>([])
const browserStates = ref<Record<string, string>>({})
let monitorTimer: number | undefined
const monitorTimeouts = new Map<string, number>()
const finishedMonitors = new Set<string>()
const activeMonitorIds = new Map<string, string>()

const languages = [
  { value: 'en', label: '英语' }, { value: 'es', label: '西班牙语' },
  { value: 'pt', label: '葡萄牙语' }, { value: 'ar', label: '阿拉伯语' },
  { value: 'fr', label: '法语' }, { value: 'de', label: '德语' },
  { value: 'it', label: '意大利语' }, { value: 'ru', label: '俄语' },
  { value: 'ja', label: '日语' }, { value: 'ko', label: '韩语' },
  { value: 'tr', label: '土耳其语' }, { value: 'id', label: '印度尼西亚语' },
  { value: 'th', label: '泰语' }, { value: 'vi', label: '越南语' },
  { value: 'zh', label: '中文' }
]

const filteredAccounts = computed(() => accounts.value.filter((item) => !accountKeyword.value || String(item.fbAccount || item.id).toLowerCase().includes(accountKeyword.value.toLowerCase())))
const targetLanguageLabel = computed(() => languages.find((item) => item.value === replyTargetLanguage.value)?.label || replyTargetLanguage.value)

const normalizeLanguage = (value?: string) => {
  const text = String(value || '').toLowerCase()
  if (['zh', '中文', 'chinese'].some((item) => text.includes(item))) return 'zh'
  if (['es', '西班牙', 'spanish', 'español'].some((item) => text.includes(item))) return 'es'
  if (['pt', '葡萄牙', 'portuguese', 'português'].some((item) => text.includes(item))) return 'pt'
  if (['ar', '阿拉伯', 'arabic', 'العربية'].some((item) => text.includes(item))) return 'ar'
  if (['fr', '法语', 'french', 'français'].some((item) => text.includes(item))) return 'fr'
  if (['de', '德语', 'german', 'deutsch'].some((item) => text.includes(item))) return 'de'
  return 'en'
}

const getMonitor = (accountId?: string | number) => monitors.value.find((item) => String(item.accountId) === String(accountId))
const getUnreadCount = (accountId?: string | number) => conversations.value.filter((item) => String(item.accountId) === String(accountId)).reduce((sum, item) => sum + Number(item.unreadCount || 0), 0)
const getAccountStatusClass = (account: FbAccount) => getMonitor(account.id)?.mode === 'realtime' ? 'online' : getMonitor(account.id)?.mode === 'scheduled' ? 'scheduled' : 'disabled'
const getBrowserState = (accountId?: string | number) => browserStates.value[String(accountId || '')] || ''
const formatTime = (value?: string) => value ? dayjs(value).format('MM-DD HH:mm') : ''

const getBridge = () => window.chrome?.webview?.hostObjects?.sync?.wpfBridge
const route = useRoute()
const router = useRouter()
const isDetached = computed(() => route.query.detached === '1')

const syncBrowserBounds = () => {
  const rect = browserPanelRef.value?.getBoundingClientRect()
  const bridge = getBridge()
  if (rect && bridge?.SetMessageBrowserBounds) bridge.SetMessageBrowserBounds(rect.left, rect.top, rect.width, rect.height)
}

const selectAccount = async (account: FbAccount) => {
  selectedAccount.value = account
  const bridge = getBridge()
  if (bridge?.OpenMessageBrowser) {
    bridge.OpenMessageBrowser(String(account.id), account.cookie || '', String(account.deviceId || ''), 'https://www.facebook.com/messages/')
    await nextTick(syncBrowserBounds)
  }
  await loadConversations()
}

const selectConversation = async (conversation: FbMessageConversation) => {
  selectedConversation.value = conversation
  replyTargetLanguage.value = normalizeLanguage(conversation.replyTargetLanguage || conversation.detectedLanguage)
  try {
    await FbMessageApi.translateUnread(conversation.id)
  } catch (error) {
    console.warn('未读消息翻译接口不可用，继续显示原文', error)
  }
  await FbMessageApi.markRead(conversation.id)
  await loadMessages()
  const bridge = getBridge()
  if (bridge?.OpenMessageConversation) bridge.OpenMessageConversation(String(conversation.accountId), conversation.targetUserId || '', `https://www.facebook.com/messages/t/${conversation.targetUserId || ''}/`)
}

const loadConversations = async () => {
  const data = await FbMessageApi.getConversationPage({ pageNo: 1, pageSize: 100, accountId: selectedAccount.value?.id, keyword: conversationKeyword.value })
  conversations.value = data.list || []
}

const loadMessages = async () => {
  if (!selectedConversation.value) return
  messages.value = await FbMessageApi.getConversationMessages(selectedConversation.value.id)
  await nextTick(() => { if (historyRef.value) historyRef.value.scrollTop = historyRef.value.scrollHeight })
}

const refreshAll = async () => {
  loading.value = true
  try {
    accounts.value = (await FbAccountApi.getFbAccountPage({ pageNo: 1, pageSize: 500 })).list || []
    monitors.value = await FbMessageApi.getMonitorAccounts()
    await loadConversations()
  } finally { loading.value = false }
}

const saveMode = async (account: FbAccount, mode: string) => {
  const current = getMonitor(account.id)
  await FbMessageApi.saveMonitorAccount({ id: current?.id, accountId: account.id, mode: mode as any, checkIntervalMinutes: current?.checkIntervalMinutes || 30, scheduleTimes: current?.scheduleTimes || '06:00', status: 1 })
  const key = String(account.id)
  enabledAccountIds.value = mode === 'disabled'
    ? enabledAccountIds.value.filter((id) => id !== key)
    : [...new Set([...enabledAccountIds.value, key])]
  monitors.value = await FbMessageApi.getMonitorAccounts()
  if (mode === 'realtime' && selectedAccount.value?.id === account.id) await selectAccount(account)
}

const toggleAccount = async (account: FbAccount, enabled: boolean) => {
  const key = String(account.id)
  enabledAccountIds.value = enabled
    ? [...new Set([...enabledAccountIds.value, key])]
    : enabledAccountIds.value.filter((id) => id !== key)
  await saveMode(account, enabled ? 'scheduled' : 'disabled')
}

const saveScheduleTimes = async (account: FbAccount, scheduleTimes: string) => {
  const current = getMonitor(account.id)
  await FbMessageApi.saveMonitorAccount({ id: current?.id, accountId: account.id, mode: current?.mode || 'scheduled', checkIntervalMinutes: current?.checkIntervalMinutes || 30, scheduleTimes: scheduleTimes.trim(), status: 1 })
  monitors.value = await FbMessageApi.getMonitorAccounts()
}

const translateReply = async () => {
  if (translating.value) return
  const text = replyChinese.value.trim()
  if (!text || !/[\u3400-\u9fff]/.test(text)) {
    if (text) replyTranslated.value = text
    return
  }
  translating.value = true
  try {
    const request = FbMessageApi.translate({ text, sourceLanguage: 'zh', targetLanguage: targetLanguageLabel.value, context: 'facebook_messenger_reply' })
    const result = await Promise.race([
      request,
      new Promise<never>((_, reject) => window.setTimeout(() => reject(new Error('翻译超过10秒未完成')), 10000))
    ])
    replyTranslated.value = result.translation || ''
  } catch (error) {
    replyTranslated.value = ''
    message.error(error instanceof Error ? error.message : '翻译失败，请稍后重试')
  } finally {
    translating.value = false
  }
}

const retryTranslation = async (item: FbMessage) => {
  if (!item.originalText) return
  try {
    const result = await FbMessageApi.retryTranslation({
      text: item.originalText,
      sourceLanguage: item.detectedLanguage || 'auto',
      targetLanguage: 'zh',
      context: item.sourceType === 'comment' ? 'facebook_comment' : 'facebook_messenger'
    })
    item.translatedText = result.translation || ''
    item.detectedLanguage = result.detectedLanguage || item.detectedLanguage
    await FbMessageApi.ingest({ ...item, translationStatus: item.translatedText ? 1 : 0 })
  } catch (error) {
    message.error('翻译失败，请稍后重试')
  }
}

const applyScript = (scripts: any[]) => {
  const script = scripts?.[0]
  if (!script) return
  replyChinese.value = script.content || script.scriptContent || script.title || ''
  void translateReply()
}

const sendMessage = async () => {
  if (!selectedConversation.value || !selectedAccount.value || !replyTranslated.value.trim()) return
  sending.value = true
  try {
    await FbMessageApi.send({
      accountId: selectedAccount.value.id,
      targetUserId: selectedConversation.value.targetUserId,
      targetName: selectedConversation.value.targetName,
      targetUrl: selectedConversation.value.targetUrl,
      conversationKey: selectedConversation.value.conversationKey,
      text: replyTranslated.value.trim(),
      targetLanguage: targetLanguageLabel.value
    })
    replyChinese.value = ''; replyTranslated.value = ''
    await loadMessages()
    message.success('消息已加入账号发送队列')
  } finally { sending.value = false }
}

const handleIncomingMessage = async (event: any) => {
  const data = event.detail || {}
  if (!data.accountId || !data.originalText) return
  data.detectedLanguage = data.detectedLanguage || 'auto'
  data.translatedText = null
  await FbMessageApi.ingest(data)
  await loadConversations()
  if (selectedConversation.value && String(selectedConversation.value.conversationKey) === String(data.conversationKey)) await loadMessages()
}

const handleBrowserState = (event: any) => {
  const data = event.detail || {}
  if (!data.accountId) return
  const labels: Record<string, string> = {
    loading: '浏览器加载中',
    loaded: '浏览器已加载',
    waiting: data.detail || '等待账号队列',
    error: data.detail || '浏览器异常'
  }
  browserStates.value[String(data.accountId)] = labels[data.state] || data.state || ''
}

const handleMonitorComplete = async (event: any) => {
  const data = event.detail || {}
  if (!data.monitorId) return
  if (data.__reportedByMessage) return
  const monitorKey = String(data.monitorId)
  if (finishedMonitors.has(monitorKey)) return
  finishedMonitors.add(monitorKey)
  const timeout = monitorTimeouts.get(monitorKey)
  if (timeout) window.clearTimeout(timeout)
  monitorTimeouts.delete(monitorKey)
  activeMonitorIds.delete(monitorKey)
  await FbMessageApi.reportMonitor(data.monitorId, Boolean(data.success), data.errorMessage)
  getBridge()?.CloseMessageBrowserAccount?.(String(data.accountId))
  await refreshAll()
}

const handleMonitorSaved = (event: any) => {
  const data = event.detail || {}
  if (!data.monitorId) return
  const monitorKey = String(data.monitorId)
  finishedMonitors.add(monitorKey)
  const timeout = monitorTimeouts.get(monitorKey)
  if (timeout) window.clearTimeout(timeout)
  monitorTimeouts.delete(monitorKey)
  activeMonitorIds.delete(monitorKey)
  getBridge()?.CloseMessageBrowserAccount?.(String(data.accountId || ''))
  void refreshAll()
}

const handleMonitorError = async (event: any) => {
  const data = event.detail || {}
  if (!data.monitorId) return
  const monitorKey = String(data.monitorId)
  if (finishedMonitors.has(monitorKey)) return
  finishedMonitors.add(monitorKey)
  const timeout = monitorTimeouts.get(monitorKey)
  if (timeout) window.clearTimeout(timeout)
  monitorTimeouts.delete(monitorKey)
  activeMonitorIds.delete(monitorKey)
  await FbMessageApi.reportMonitor(data.monitorId, false, data.errorMessage || '消息检查失败')
  getBridge()?.CloseMessageBrowserAccount?.(String(data.accountId))
  await refreshAll()
}

const claimMonitor = async () => {
  const result = await FbMessageApi.claimMonitor(3)
  for (const item of result || []) {
    const bridge = getBridge()
    if (bridge?.StartMessageMonitor) {
      const proxyConfigJson = await getFbAccountProxyJson(item.accountId)
      bridge.StartMessageMonitor(String(item.monitorId), String(item.accountId), item.cookie || '', String(item.deviceId || ''), item.url, item.mode, proxyConfigJson)
    }
    if (item.mode === 'scheduled') {
      const monitorKey = String(item.monitorId)
      activeMonitorIds.set(monitorKey, String(item.accountId))
      finishedMonitors.delete(monitorKey)
      const timer = window.setTimeout(() => {
        if (finishedMonitors.has(monitorKey)) return
        finishedMonitors.add(monitorKey)
        void FbMessageApi.reportMonitor(item.monitorId, false, '消息检查超过3分钟未回传')
        getBridge()?.CloseMessageBrowserAccount?.(String(item.accountId))
        monitorTimeouts.delete(monitorKey)
      }, 180000)
      monitorTimeouts.set(monitorKey, timer)
    }
  }
}

const handleMessageWindowClosed = async () => {
  const monitorIds = [...activeMonitorIds.keys()]
  activeMonitorIds.clear()
  await Promise.all(monitorIds.map((monitorId) => FbMessageApi.reportMonitor(monitorId, false, '消息管理窗口已关闭')))
}

const refreshRealtimeMonitors = async () => {
  for (const item of monitors.value.filter((monitor) => monitor.mode === 'realtime' && monitor.id)) {
    await FbMessageApi.heartbeat(item.id!)
  }
}

onMounted(async () => {
  const bridge = getBridge()
  if (!isDetached.value && bridge?.OpenMessageManagerWindow) {
    await router.replace({ name: 'Index' })
    bridge.OpenMessageManagerWindow()
    return
  }
  window.addEventListener('fb:message:received', handleIncomingMessage)
  window.addEventListener('fb:message:monitor-complete', handleMonitorComplete)
  window.addEventListener('fb:message:monitor-saved', handleMonitorSaved)
  window.addEventListener('fb:message:monitor-error', handleMonitorError)
  window.addEventListener('fb:message:browser-state', handleBrowserState)
  window.addEventListener('fb:message:window-closed', handleMessageWindowClosed)
  window.addEventListener('resize', syncBrowserBounds)
  await refreshAll()
  enabledAccountIds.value = monitors.value
    .filter((item) => item.mode !== 'disabled')
    .map((item) => String(item.accountId))
  await claimMonitor()
  monitorTimer = window.setInterval(() => { void claimMonitor(); void refreshRealtimeMonitors() }, 30000)
})

onBeforeUnmount(() => {
  window.removeEventListener('fb:message:received', handleIncomingMessage)
  window.removeEventListener('fb:message:monitor-complete', handleMonitorComplete)
  window.removeEventListener('fb:message:monitor-saved', handleMonitorSaved)
  window.removeEventListener('fb:message:monitor-error', handleMonitorError)
  window.removeEventListener('fb:message:browser-state', handleBrowserState)
  window.removeEventListener('fb:message:window-closed', handleMessageWindowClosed)
  window.removeEventListener('resize', syncBrowserBounds)
  if (monitorTimer) window.clearInterval(monitorTimer)
  monitorTimeouts.forEach((timer) => window.clearTimeout(timer))
  monitorTimeouts.clear()
  finishedMonitors.clear()
  getBridge()?.HideMessageBrowser?.()
})
</script>

<style scoped>
.message-manager { min-height: calc(100vh - 120px); }
.message-toolbar { display:flex; align-items:center; gap:16px; margin-bottom:12px; }
.message-title { flex:1; font-size:18px; font-weight:600; }
.message-layout { display:grid; grid-template-columns:220px minmax(420px,1fr) 420px; height:calc(100vh - 190px); min-height:620px; border:1px solid var(--el-border-color); background:var(--el-bg-color); }
.account-panel,.conversation-panel { overflow:hidden; display:flex; flex-direction:column; border-right:1px solid var(--el-border-color); }
.conversation-panel { border-right:0; border-left:1px solid var(--el-border-color); }
.panel-heading { padding:14px; font-weight:600; border-bottom:1px solid var(--el-border-color); }
.conversation-heading { display:flex; align-items:center; gap:10px; }
.conversation-heading .el-input { width:150px; margin-left:auto; }
.account-row,.conversation-row { cursor:pointer; padding:10px 12px; border-bottom:1px solid var(--el-border-color-lighter); }
.account-row.active,.conversation-row.active { background:var(--el-color-primary-light-9); }
.account-main,.account-meta,.conversation-line,.composer-actions,.composer-target-row { display:flex; align-items:center; justify-content:space-between; gap:8px; }
.account-name { overflow:hidden; text-overflow:ellipsis; white-space:nowrap; }
.account-meta { margin-top:7px; }
.account-state { margin-top:4px; color:var(--el-text-color-secondary); font-size:12px; overflow:hidden; text-overflow:ellipsis; white-space:nowrap; }
.status-dot { width:8px; height:8px; border-radius:50%; background:#a8abb2; display:inline-block; flex:none; }
.status-dot.online { background:#67c23a; }.status-dot.scheduled { background:#e6a23c; }
.account-panel>.el-input,.conversation-list { margin:10px; }.conversation-list { overflow:auto; flex:1; margin-top:0; }
.conversation-row { display:flex; align-items:center; gap:10px; }
.conversation-avatar { width:32px; height:32px; border-radius:50%; background:var(--el-color-primary-light-7); display:grid; place-items:center; flex:none; }
.conversation-body { min-width:0; flex:1; }.conversation-line span,.message-language { color:var(--el-text-color-secondary); font-size:12px; }.conversation-preview { overflow:hidden; text-overflow:ellipsis; white-space:nowrap; color:var(--el-text-color-secondary); font-size:13px; margin-top:4px; }
.browser-panel { position:relative; overflow:hidden; background:#f5f7fa; }
.browser-placeholder { position:absolute; inset:0; display:flex; flex-direction:column; align-items:center; justify-content:center; gap:10px; color:var(--el-text-color-secondary); pointer-events:none; }
.browser-placeholder small { font-size:12px; }
.reply-panel { border-top:1px solid var(--el-border-color); max-height:56%; display:flex; flex-direction:column; }
.message-history { overflow:auto; padding:12px; flex:1; }.message-item { margin-bottom:12px; max-width:92%; }.message-item.outbound { margin-left:auto; text-align:right; }.message-label,.composer-label { font-size:12px; color:var(--el-text-color-secondary); margin-bottom:4px; }.message-original,.message-translation { padding:8px 10px; border-radius:6px; background:var(--el-fill-color-light); white-space:pre-wrap; text-align:left; }.message-translation { margin-top:4px; color:var(--el-color-primary); }.reply-composer { padding:10px 12px; border-top:1px solid var(--el-border-color); }.composer-target-row { margin:8px 0; font-size:12px; color:var(--el-text-color-secondary); }.composer-actions { margin-top:8px; }
@media (max-width:1200px) { .message-layout { grid-template-columns:190px minmax(300px,1fr) 360px; } }
</style>
