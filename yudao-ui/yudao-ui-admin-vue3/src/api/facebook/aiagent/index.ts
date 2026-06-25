import request from '@/config/axios'
import type { Dayjs } from 'dayjs'

export interface FbAiAgentConfig {
  id?: number
  agentName: string
  knowledgeIds?: string
  seedKeywords?: string
  targetCountries?: string
  targetLanguages?: string
  accountIds?: string
  monitorGroupIds?: string
  leadScoreWorkflowId?: number
  commentWorkflowId?: number
  dmWorkflowId?: number
  autoCommentEnabled?: boolean
  autoDmEnabled?: boolean
  dailyCommentLimit?: number
  dailyDmLimit?: number
  replyDelayRange?: string
  personaConfig?: string
  status?: number
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

export const FbAiAgentApi = {
  getConfig: async () => {
    return await request.get<FbAiAgentConfig>({ url: '/facebook/ai-agent/config' })
  },

  saveConfig: async (data: FbAiAgentConfig) => {
    return await request.post<number>({ url: '/facebook/ai-agent/config/save', data })
  },

  getTouchRecordPage: async (params: any) => {
    return await request.get({ url: '/facebook/ai-agent/touch-record/page', params })
  },

  createTouchRecord: async (data: FbAiTouchRecordSaveReq) => {
    return await request.post<number>({ url: '/facebook/ai-agent/touch-record/create', data })
  },

  updateTouchRecordResult: async (data: { id: number; status: number; failReason?: string }) => {
    return await request.put<boolean>({ url: '/facebook/ai-agent/touch-record/update-result', data })
  },

  dispatchOnce: async () => {
    return await request.post<{ dispatched: boolean; message: string }>({
      url: '/facebook/ai-agent/dispatch-once'
    })
  },

  saveLeadAnalysis: async (data: FbAiLeadAnalysisSaveReq) => {
    return await request.post<boolean>({ url: '/facebook/ai-agent/lead-analysis/save', data })
  }
}
