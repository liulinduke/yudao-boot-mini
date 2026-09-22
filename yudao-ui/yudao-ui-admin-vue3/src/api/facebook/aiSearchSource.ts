import request from '@/config/axios'

export interface AiSearchSourceVO { id?: string | number; name: string; domain?: string; sourceType?: string; sourceRole?: string; priority?: number; status?: string; verified?: boolean; usageCount?: number; companyCount?: number; qualifiedCompanyCount?: number; yieldScore?: number }
export const AiSearchSourceApi = {
  list: (status?: string) => request.get<AiSearchSourceVO[]>({ url: '/facebook/ai-search/source/list', params: { status } }),
  create: (data: AiSearchSourceVO) => request.post({ url: '/facebook/ai-search/source/create', data }),
  createBatch: (data: AiSearchSourceVO[]) => request.post({ url: '/facebook/ai-search/source/batch', data }),
  update: (data: AiSearchSourceVO) => request.put({ url: '/facebook/ai-search/source/update', data }),
  updateStatus: (id: string | number, status: string) => request.put({ url: '/facebook/ai-search/source/status', params: { id, status } })
}
