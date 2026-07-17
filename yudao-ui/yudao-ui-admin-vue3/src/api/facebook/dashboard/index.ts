import request from '@/config/axios'

export interface FbDashboardAiResult {
  autoCollectedLeadCount: number
  autoAnalyzedCustomerCount: number
  generatedInteractionSuggestionCount: number
  autoTouchedCount: number
}

export interface FbDashboardSocialItem {
  type: string
  count: number
}

export interface FbDashboardSocialSummary {
  total: number
  items: FbDashboardSocialItem[]
}

export interface FbDashboardHomeRespVO {
  aiResult: FbDashboardAiResult
  socialCollection: FbDashboardSocialSummary
  socialOperation: FbDashboardSocialSummary
}

export const FbDashboardApi = {
  getHome: async (): Promise<FbDashboardHomeRespVO> => {
    return await request.get({ url: '/facebook/dashboard/home' })
  }
}
