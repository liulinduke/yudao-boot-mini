import { FbMessageApi } from '@/api/facebook/message'
import { GlobalConfigApi } from '@/api/facebook/globalconfig'

let initialized = false

export function setupFbMessageWpfRelay() {
  if (initialized) return
  initialized = true
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
          data = await FbMessageApi.claimMonitor(payload.limit || 3, payload.excludeAccounts || [])
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
