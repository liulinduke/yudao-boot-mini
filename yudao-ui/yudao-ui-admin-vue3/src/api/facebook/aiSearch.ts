import request from '@/config/axios'

export interface AiSearchTaskVO {
  id?: string | number
  name?: string
  userGoal: string
  company?: string
  keywords?: string
  targetCountry?: string
  targetCount?: number
  customerType?: string
  address?: string
  customerStage?: string
  customerTags?: string
  referenceWebsite?: string
  hsCodes?: string
  aiKeywordExpand?: boolean
  contactEnrichment?: boolean
  positionStrategy?: string
  scheduleType?: string
  scheduleInterval?: string
  status?: string
  searchSnapshot?: AiSearchSnapshotVO
}
export interface AiSearchExpansionVO {
  keywords: string[]
  scenes: string[]
  productTerms?: string[]
  customerRoles?: string[]
  purchasingTerms?: string[]
  localTerms?: string[]
  applications?: string[]
  ecommerceChannelTerms?: string[]
  upstreamDownstreamTerms?: string[]
}
export interface AiSearchSnapshotVO {
  version: number
  selectedKeywords: string[]
  productTerms: string[]
  customerRoleTerms: string[]
  purchasingTerms: string[]
  localLanguageTerms: string[]
  applications: string[]
  scenarios: string[]
  ecommerceChannelTerms: string[]
  upstreamDownstreamTerms: string[]
}

export interface AiSearchCompanyVO {
  id: string | number
  taskId: string | number
  name: string
  website?: string
  country?: string
  industry?: string
  customerType?: string
  description?: string
  icpScore?: number
  icpLevel?: string
  verificationStatus?: string
  contactCount?: number
  contactWithEmailCount?: number
  contactWithPhoneCount?: number
}

export interface AiSearchContactVO {
  id: string | number
  companyId: string | number
  taskId: string | number
  name?: string
  jobTitle?: string
  email?: string
  phone?: string
  linkedinUrl?: string
  otherSocialUrl?: string
  icpScore?: number
  companyNote?: string
}
export interface AiSearchStatsVO { taskId: string | number; companyCount: number; qualifiedCompanyCount: number; contactCount: number; contactWithEmailCount: number; contactWithPhoneCount: number }
export interface AiSearchTradeEvidenceVO { id?: string | number; companyId: string | number; url?: string; hsCode?: string; product?: string; tradeType?: string; tradeDate?: string; partnerCountry?: string; evidenceText?: string; confidence?: number }
export interface AiSearchCompanyDetailVO { company: AiSearchCompanyVO; contacts: AiSearchContactVO[]; evidences: Array<{ id?: string | number; url?: string; evidenceText?: string; confidence?: number }>; tradeEvidences: AiSearchTradeEvidenceVO[] }
export interface AiSearchRunVO { id: string | number; taskId: string | number; status?: string; targetQualifiedCount?: number; qualifiedCountBefore?: number; roundCount?: number; queryCount?: number; newCompanyCount?: number; qualifiedCompanyCount?: number; tradeVerificationCount?: number; tradeFoundCount?: number; contactCount?: number; errorCount?: number; startedAt?: string; completedAt?: string }
export interface AiSearchRoundVO { id: string | number; taskId: string | number; runId: string | number; roundNo?: number; strategy?: string; resultCount?: number; newCompanyCount?: number; qualifiedCompanyCount?: number; duplicateCount?: number; newSourceCount?: number }

export const AiSearchApi = {
  customers: (params: any) => request.get({ url: '/facebook/ai-search/resource/customers', params }),
  exportCustomers: (params: any) => request.download({ url: '/facebook/ai-search/resource/customers/export', params }),
  expand: (data: { userGoal: string; company?: string; keywords?: string; targetCountry?: string; customerType?: string; hsCode?: string; keywordLimit?: number; sceneLimit?: number }) => request.post<AiSearchExpansionVO>({ url: '/facebook/ai-search/task/expand', data }),
  create: (data: AiSearchTaskVO) => request.post({ url: '/facebook/ai-search/task/create', data }),
  update: (data: AiSearchTaskVO) => request.put({ url: '/facebook/ai-search/task/update', data }),
  delete: (id: string | number) => request.delete({ url: '/facebook/ai-search/task/delete', params: { id } }),
  get: (id: string | number) => request.get({ url: '/facebook/ai-search/task/get', params: { id } }),
  list: () => request.get({ url: '/facebook/ai-search/task/list' }),
  start: (id: string | number) => request.post({ url: '/facebook/ai-search/task/start', params: { id } }),
  status: (id: string | number, status: string) => request.put({ url: '/facebook/ai-search/task/status', params: { id, status } }),
  companies: (taskId: string | number) => request.get({ url: '/facebook/ai-search/result/companies', params: { taskId } }),
  contacts: (taskId: string | number, params: any = {}) => request.get({ url: '/facebook/ai-search/result/contacts/page', params: { ...params, taskId } })
  ,companyDetail: (companyId: string | number) => request.get<AiSearchCompanyDetailVO>({ url: '/facebook/ai-search/result/company-detail', params: { companyId } })
  ,stats: (taskId: string | number) => request.get<AiSearchStatsVO>({ url: '/facebook/ai-search/result/stats', params: { taskId } })
  ,runs: (taskId: string | number) => request.get<AiSearchRunVO[]>({ url: '/facebook/ai-search/result/runs', params: { taskId } })
  ,rounds: (taskId: string | number) => request.get<AiSearchRoundVO[]>({ url: '/facebook/ai-search/result/rounds', params: { taskId } })
  ,tradeVerify: (companyId: string | number, hsCode?: string, product?: string) => request.get<{ companyId: string | number; status: string; note?: string; evidenceCount?: number }>({ url: '/facebook/ai-search/trade/verify', params: { companyId, hsCode, product } })
}
