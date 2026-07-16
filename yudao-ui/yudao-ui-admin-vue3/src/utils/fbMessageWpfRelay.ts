import { FbMessageApi } from '@/api/facebook/message'
import { GlobalConfigApi } from '@/api/facebook/globalconfig'
import { ScriptApi } from '@/api/facebook/script'

let initialized = false

export function setupFbMessageWpfRelay() {
  if (initialized) return
  initialized = true
  // WPF starts after the authenticated Vue shell is ready, so tenant context exists here.
  // Do not run this from the backend ApplicationReady event: that phase has no tenant.
  if (window.chrome?.webview) {
    void FbMessageApi.normalizeMonitorRuntime().catch(() => undefined)
  }
  window.addEventListener('fb:wpf:message-command', async (event: any) => {
    const command = event.detail || {}
    const send = (ok: boolean, data?: any, error?: string) => {
      window.chrome?.webview?.postMessage(JSON.stringify({
        type: 'fb:wpf:message-response',
        requestId: command.requestId,
        ok,
        data,
        error
      }))
    }

    try {
      const payload = command.payload || {}
      let data: any
      switch (command.action) {
        case 'accounts':
          data = await FbMessageApi.getMonitorCandidates()
          break
        case 'monitors':
          data = await FbMessageApi.getMonitorAccounts()
          break
        case 'monitorPool':
          data = await FbMessageApi.getMonitorPool()
          break
        case 'scripts':
          data = await ScriptApi.getScriptPage(payload)
          break
        case 'normalizeRuntime':
          data = await FbMessageApi.normalizeMonitorRuntime()
          break
        case 'addMonitorPool':
          data = await FbMessageApi.addMonitorPool(payload.accountIds || [], Number(payload.checkIntervalMinutes) || 30)
          break
        case 'removeMonitorPool':
          data = await FbMessageApi.removeMonitorPool(payload.accountIds || [])
          break
        case 'batchMonitorState':
          data = await FbMessageApi.batchMonitorState(payload.accountIds || [], payload.state, payload.checkIntervalMinutes, Boolean(payload.preserveOnline))
          break
        case 'updateMonitorIntervals':
          data = await FbMessageApi.updateMonitorIntervals(payload.accountIds || [], Number(payload.checkIntervalMinutes) || 30)
          break
        case 'saveMonitor':
          data = await FbMessageApi.saveMonitorAccount(payload)
          break
        case 'batchSaveMonitors':
          data = await FbMessageApi.batchSaveMonitorAccounts(payload.items || [])
          break
        case 'globalConfigs':
          data = await GlobalConfigApi.getAllConfigs()
          break
        case 'saveGlobalConfigs':
          data = await GlobalConfigApi.batchSaveConfigs(payload.items || [])
          break
        case 'claimMonitor':
          data = await FbMessageApi.claimMonitor(payload.limit || 3, payload.excludeAccounts || [], payload.accountIds || [], Boolean(payload.manual))
          break
        case 'heartbeat':
          data = await FbMessageApi.heartbeat(payload.monitorId)
          break
        case 'reportMonitor':
          data = await FbMessageApi.reportMonitor(payload.monitorId, Boolean(payload.success), payload.errorMessage)
          break
        case 'reportUnreadBadges':
          data = await FbMessageApi.reportUnreadBadges(payload)
          break
        case 'conversations':
          data = await FbMessageApi.getConversationPage(payload)
          break
        case 'messages':
          data = await FbMessageApi.getConversationMessages(payload.conversationId)
          break
        case 'translateUnread':
          data = await FbMessageApi.translateUnread(payload.conversationId)
          break
        case 'unreadSummary':
          data = await FbMessageApi.getUnreadSummary()
          break
        case 'markRead':
          data = await FbMessageApi.markRead(payload.conversationId)
          break
        case 'ingest': {
          if (payload.direction === 'outbound') {
            data = await FbMessageApi.ingest(payload)
            break
          }
          const messageId = await FbMessageApi.ingest({ ...payload, translatedText: null })
          data = {
            id: messageId,
            ...payload,
            detectedLanguage: payload.detectedLanguage || 'auto',
            translatedText: null
          }
          break
        }
        case 'translate':
          data = await FbMessageApi.translate(payload)
          break
        case 'send':
          data = await FbMessageApi.send(payload)
          break
        default:
          throw new Error(`未知消息管理命令：${command.action}`)
      }
      send(true, data)
    } catch (error: any) {
      const response = error?.response?.data
      const detail = response?.msg || response?.message || response?.error || response
      send(false, undefined, typeof detail === 'string' ? detail : JSON.stringify(detail || error?.message || error))
    }
  })
}
