import request from '@/config/axios'
import type { Dayjs } from 'dayjs'

export interface FbAiAgentConfig {
  id?: string
  agentName: string
  agentType?: string
  searchMode?: string
  exportProduct?: string
  knowledgeIds?: string
  seedKeywords?: string
  keywordPool?: string
  keywordCursor?: number
  keywordsPerRun?: number
  aiKeywordExpandEnabled?: boolean
  aiKeywordExpandCount?: number
  targetCustomerCount?: number
  executeFrequency?: string
  executeTime?: string
  lastExecuteTime?: string | Dayjs
  targetCountries?: string
  targetLanguages?: string
  accountIds?: string
  monitorGroupIds?: string
  touchScoreThreshold?: number
  autoCommentEnabled?: boolean
  autoDmEnabled?: boolean
  dailyCommentLimit?: number
  dailyDmLimit?: number
  replyDelayRange?: string
  personaConfig?: string
  personaType?: string
  status?: number
  leadCount?: number
  pendingCount?: number
  createTime?: string | Dayjs
}

export interface FbAiAgentDiscoveryLog {
  id: string | number
  agentConfigId?: string | number
  keyword?: string
  sourceType?: string
  discoveredCount?: number
  highIntentCount?: number
  pageCollectCount?: number
  aiAnalyzeCount?: number
  filteredCount?: number
  finalLeadCount?: number
  collectTaskId?: string | number
  createTime?: string | Dayjs
  updateTime?: string | Dayjs
}

export interface FbAiAgentRunLog {
  id: string | number
  agentConfigId?: string | number
  title?: string
  content?: string
  logLevel?: string
  createTime?: string | Dayjs
}

export interface FbAiTouchRecord {
  id: string | number
  agentConfigId?: string | number
  leadType?: string
  leadId?: string | number
  targetUserId?: string
  targetUrl?: string
  accountDbId?: string | number
  accountId?: string
  fbAccount?: string
  touchType?: string
  generatedContent?: string
  aiReason?: string
  status?: number
  failReason?: string
  scheduledTime?: string | Dayjs
  sentTime?: string | Dayjs
  operationTaskId?: string | number
  operationDetailId?: string | number
  createTime?: string | Dayjs
}

export interface FbAiLeadAnalysisSaveReq {
  leadType: 'user' | 'post'
  leadId: string | number
  aiTags?: string
  intentLevel?: string
  intentReason?: string
  sentiment?: string
  leadCategory?: string
  country?: string
  language?: string
  productRelevanceScore?: number
  aiSummary?: string
  touchStatus?: string
}

export interface FbAiTouchRecordSaveReq {
  agentConfigId?: string | number
  leadType?: string
  leadId?: string | number
  targetUserId?: string
  targetUrl?: string
  accountDbId?: string | number
  accountId?: string
  fbAccount?: string
  touchType?: string
  generatedContent?: string
  aiReason?: string
  status?: number
  scheduledTime?: string | Dayjs
  operationTaskId?: string | number
  operationDetailId?: string | number
}

export interface FbAiKeywordGenerateReq {
  seedKeywords?: string[]
  targetCountries?: string[]
  productDescription?: string
  expandCount?: number
}

export interface FbAiKeywordGenerateResp {
  keywords: string[]
}

export interface FbAiAgentDispatchDetail {
  taskId?: string | number
  detailId: string | number
  fbAccount: string
  accountId?: string
  cookie?: string
  searchUrl?: string
  sourceUserId?: string | number
  expectedCount?: number
  taskType?: number
  sourceType?: 'collect' | 'dm' | 'operation'
  targetUserId?: string
  scriptContent?: string
  minIntervalSeconds?: number
  maxIntervalSeconds?: number
  actionConfig?: string
}

export const FbAiAgentApi = {
  getConfigPage: async (params: any) => {
    return await request.get({ url: '/facebook/ai-agent/page', params })
  },

  getConfigById: async (id: string | number) => {
    return await request.get<FbAiAgentConfig>({ url: '/facebook/ai-agent/get', params: { id } })
  },

  getConfig: async () => {
    return await request.get<FbAiAgentConfig>({ url: '/facebook/ai-agent/config' })
  },

  saveConfig: async (data: FbAiAgentConfig) => {
    return await request.post<number>({ url: '/facebook/ai-agent/config/save', data })
  },

  updateStatus: async (data: { id: string | number; status: number }) => {
    return await request.put<boolean>({ url: '/facebook/ai-agent/update-status', data })
  },

  deleteConfig: async (id: string | number) => {
    return await request.delete<boolean>({ url: '/facebook/ai-agent/delete', params: { id } })
  },

  generateKeywords: async (data: FbAiKeywordGenerateReq) => {
    return await request.post<FbAiKeywordGenerateResp>({ url: '/facebook/ai-agent/generate-keywords', data })
  },

  getDiscoveryLogPage: async (params: any) => {
    return await request.get({ url: '/facebook/ai-agent/discovery-log/page', params })
  },

  getTouchRecordPage: async (params: any) => {
    return await request.get({ url: '/facebook/ai-agent/touch-record/page', params })
  },

  getLeadPage: async (params: any) => {
    return await request.get({ url: '/facebook/ai-agent/lead/page', params })
  },

  getRunLogPage: async (params: any) => {
    return await request.get({ url: '/facebook/ai-agent/run-log/page', params })
  },

  createTouchRecord: async (data: FbAiTouchRecordSaveReq) => {
    return await request.post<number>({ url: '/facebook/ai-agent/touch-record/create', data })
  },

  updateTouchRecordResult: async (data: { id: string | number; status: number; failReason?: string }) => {
    return await request.put<boolean>({ url: '/facebook/ai-agent/touch-record/update-result', data })
  },

  dispatchOnce: async () => {
    return await request.post<{ dispatched: boolean; message: string; details?: FbAiAgentDispatchDetail[] }>({
      url: '/facebook/ai-agent/dispatch-once'
    })
  },

  executeNow: async (ids: Array<string | number>) => {
    return await request.post<{ dispatched: boolean; message: string; details?: FbAiAgentDispatchDetail[] }>({
      url: '/facebook/ai-agent/execute-now',
      data: { ids }
    })
  },

  claimPendingCollectDetails: async (limit: number, excludeAccounts?: string[]) => {
    return await request.get<FbAiAgentDispatchDetail[]>({
      url: '/facebook/fb-collect-detail/claim-pending',
      params: { limit, excludeAccounts: excludeAccounts?.join(',') }
    })
  },

  claimNextCollectDetail: async (fbAccount: string, taskId: string | number) => {
    return await request.get<FbAiAgentDispatchDetail>({
      url: '/facebook/fb-collect-detail/claim-next',
      params: { fbAccount, taskId }
    })
  },

  saveLeadAnalysis: async (data: FbAiLeadAnalysisSaveReq) => {
    return await request.post<boolean>({ url: '/facebook/ai-agent/lead-analysis/save', data })
  }
}
