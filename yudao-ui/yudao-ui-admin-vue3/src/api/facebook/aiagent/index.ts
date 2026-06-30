import request from '@/config/axios'
import type { Dayjs } from 'dayjs'

export interface FbAiAgentConfig {
  id?: number
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
  id: number
  agentConfigId?: number
  keyword?: string
  sourceType?: string
  discoveredCount?: number
  highIntentCount?: number
  pageCollectCount?: number
  aiAnalyzeCount?: number
  filteredCount?: number
  finalLeadCount?: number
  collectTaskId?: number
  createTime?: string | Dayjs
}

export interface FbAiAgentRunLog {
  id: number
  agentConfigId?: number
  title?: string
  content?: string
  logLevel?: string
  createTime?: string | Dayjs
}

export interface FbAiTouchRecord {
  id: number
  agentConfigId?: number
  leadType?: string
  leadId?: number
  targetUserId?: string
  targetUrl?: string
  accountDbId?: number
  accountId?: string
  fbAccount?: string
  touchType?: string
  generatedContent?: string
  aiReason?: string
  status?: number
  failReason?: string
  scheduledTime?: string | Dayjs
  sentTime?: string | Dayjs
  operationTaskId?: number
  operationDetailId?: number
  createTime?: string | Dayjs
}

export interface FbAiLeadAnalysisSaveReq {
  leadType: 'user' | 'post'
  leadId: number
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
  agentConfigId?: number
  leadType?: string
  leadId?: number
  targetUserId?: string
  targetUrl?: string
  accountDbId?: number
  accountId?: string
  fbAccount?: string
  touchType?: string
  generatedContent?: string
  aiReason?: string
  status?: number
  scheduledTime?: string | Dayjs
  operationTaskId?: number
  operationDetailId?: number
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
  taskId?: number
  detailId: number
  fbAccount: string
  cookie?: string
  searchUrl: string
  expectedCount?: number
  taskType?: number
}

export const FbAiAgentApi = {
  getConfigPage: async (params: any) => {
    return await request.get({ url: '/facebook/ai-agent/page', params })
  },

  getConfigById: async (id: number) => {
    return await request.get<FbAiAgentConfig>({ url: '/facebook/ai-agent/get', params: { id } })
  },

  getConfig: async () => {
    return await request.get<FbAiAgentConfig>({ url: '/facebook/ai-agent/config' })
  },

  saveConfig: async (data: FbAiAgentConfig) => {
    return await request.post<number>({ url: '/facebook/ai-agent/config/save', data })
  },

  updateStatus: async (data: { id: number; status: number }) => {
    return await request.put<boolean>({ url: '/facebook/ai-agent/update-status', data })
  },

  deleteConfig: async (id: number) => {
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

  updateTouchRecordResult: async (data: { id: number; status: number; failReason?: string }) => {
    return await request.put<boolean>({ url: '/facebook/ai-agent/touch-record/update-result', data })
  },

  dispatchOnce: async () => {
    return await request.post<{ dispatched: boolean; message: string; details?: FbAiAgentDispatchDetail[] }>({
      url: '/facebook/ai-agent/dispatch-once'
    })
  },

  saveLeadAnalysis: async (data: FbAiLeadAnalysisSaveReq) => {
    return await request.post<boolean>({ url: '/facebook/ai-agent/lead-analysis/save', data })
  }
}
