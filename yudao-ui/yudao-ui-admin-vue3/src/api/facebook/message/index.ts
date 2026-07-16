import request from '@/config/axios'

export interface FbMessageMonitorAccount {
  id?: string | number
  accountId: string | number
  receiveEnabled?: number
  onlineStatus?: number
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
  getMonitorCandidates: () => request.get<any[]>({ url: '/facebook/message/monitor/candidates' }),
  getMonitorAccounts: () => request.get<FbMessageMonitorAccount[]>({ url: '/facebook/message/monitor/accounts' }),
  getMonitorPool: () => request.get<FbMessageMonitorAccount[]>({ url: '/facebook/message/monitor/pool' }),
  addMonitorPool: (accountIds: (string | number)[], checkIntervalMinutes = 30) => request.post<boolean>({ url: '/facebook/message/monitor/pool/add', data: { accountIds, checkIntervalMinutes } }),
  removeMonitorPool: (accountIds: (string | number)[]) => request.post<boolean>({ url: '/facebook/message/monitor/pool/remove', data: { accountIds } }),
  batchMonitorState: (accountIds: (string | number)[], state: 'online' | 'scheduled', checkIntervalMinutes?: number, preserveOnline = false) => request.post<boolean>({ url: '/facebook/message/monitor/batch-state', data: { accountIds, state, checkIntervalMinutes, preserveOnline } }),
  updateMonitorIntervals: (accountIds: (string | number)[], checkIntervalMinutes: number) => request.post<boolean>({ url: '/facebook/message/monitor/interval', data: { accountIds, checkIntervalMinutes } }),
  normalizeMonitorRuntime: () => request.post<boolean>({ url: '/facebook/message/monitor/normalize-runtime' }),
  saveMonitorAccount: (data: FbMessageMonitorAccount) => request.post<number>({ url: '/facebook/message/monitor/accounts/save', data }),
  batchSaveMonitorAccounts: (data: FbMessageMonitorAccount[]) => request.post<boolean>({ url: '/facebook/message/monitor/accounts/batch-save', data }),
  claimMonitor: (limit = 3, excludeAccounts: string[] = [], accountIds: string[] = [], manual = false) => request.post<FbMessageMonitorClaim[]>({ url: '/facebook/message/monitor/claim', data: { limit, excludeAccounts, accountIds, manual } }),
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
