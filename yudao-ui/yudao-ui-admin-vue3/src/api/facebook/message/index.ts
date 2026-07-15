import request from '@/config/axios'

export interface FbMessageMonitorAccount {
  id?: string | number
  accountId: string | number
  mode: 'realtime' | 'scheduled' | 'disabled'
  checkIntervalMinutes?: number
  nextCheckTime?: string
  lastCheckTime?: string
  lastSuccessTime?: string
  status?: number
  errorMessage?: string
}

export interface FbMessageMonitorClaim {
  monitorId: string | number
  accountId: string | number
  fbAccount?: string
  cookie?: string
  deviceId?: number | null
  mode: 'realtime' | 'scheduled' | 'disabled'
  checkIntervalMinutes?: number
  url: string
}

export interface FbMessageConversation {
  id: string | number
  accountId: string | number
  conversationKey: string
  targetUserId?: string
  targetName?: string
  targetUrl?: string
  sourceType?: string
  detectedLanguage?: string
  replyTargetLanguage?: string
  unreadCount?: number
  lastMessagePreview?: string
  lastMessageTime?: string
}

export interface FbMessage {
  id: string | number
  conversationId: string | number
  accountId: string | number
  direction: 'inbound' | 'outbound'
  sourceType?: string
  originalText: string
  translatedText?: string
  detectedLanguage?: string
  targetLanguage?: string
  isRead?: boolean
  sendStatus?: number
  messageTime?: string
  sendTime?: string
  sourcePostUrl?: string
}

export const FbMessageApi = {
  getMonitorAccounts: () => request.get<FbMessageMonitorAccount[]>({ url: '/facebook/message/monitor/accounts' }),
  saveMonitorAccount: (data: FbMessageMonitorAccount) => request.post<number>({ url: '/facebook/message/monitor/accounts/save', data }),
  batchSaveMonitorAccounts: (data: FbMessageMonitorAccount[]) => request.post<boolean>({ url: '/facebook/message/monitor/accounts/batch-save', data }),
  claimMonitor: (limit = 3, excludeAccounts: string[] = []) => request.post<FbMessageMonitorClaim[]>({ url: '/facebook/message/monitor/claim', data: { limit, excludeAccounts } }),
  heartbeat: (monitorId: string | number) => request.post<boolean>({ url: '/facebook/message/monitor/heartbeat', params: { monitorId } }),
  reportMonitor: (monitorId: string | number, success: boolean, errorMessage?: string) => request.post({ url: '/facebook/message/monitor/report', params: { monitorId, success, errorMessage } }),
  reportUnreadBadges: (data: { accountId: string | number; messengerUnreadCount: number; notificationUnreadCount: number; loggedIn: boolean }) => request.post({ url: '/facebook/message/monitor/badge-report', data }),
  ingest: (data: any) => request.post<number>({ url: '/facebook/message/ingest', data }),
  getConversationPage: (params: any) => request.get<{ list: FbMessageConversation[]; total: number }>({ url: '/facebook/message/conversation/page', params }),
  getConversationMessages: (id: string | number) => request.get<FbMessage[]>({ url: `/facebook/message/conversation/${id}/messages` }),
  markRead: (id: string | number) => request.post({ url: `/facebook/message/conversation/${id}/read` }),
  translateUnread: (id: string | number) => request.post<FbMessage[]>({ url: `/facebook/message/conversation/${id}/translate-unread` }),
  getUnreadSummary: () => request.get<any[]>({ url: '/facebook/message/unread/summary' }),
  getMessagePage: (params: any) => request.get({ url: '/facebook/message/page', params }),
  translate: (data: { text: string; sourceLanguage?: string; targetLanguage: string; context?: string }) => request.post<{ translation?: string; detectedLanguage?: string }>({ url: '/facebook/message/translate', data }),
  retryTranslation: (data: { text: string; sourceLanguage?: string; targetLanguage: string; context?: string }) => request.post<{ translation?: string; detectedLanguage?: string }>({ url: '/facebook/message/retry-translation', data }),
  send: (data: any) => request.post<number>({ url: '/facebook/message/send', data })
}
