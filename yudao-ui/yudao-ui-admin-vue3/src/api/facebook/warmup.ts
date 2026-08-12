import request from '@/config/axios'

export interface FbWarmupTaskSave {
  taskName?: string
  scheduleTime: number
  accountIds: Array<string | number>
  warmupConfig: string
}

export const FbWarmupApi = {
  create: (data: FbWarmupTaskSave) => request.post({ url: '/facebook/warmup-task/create', data }),
  executeNow: (data: FbWarmupTaskSave) => request.post({ url: '/facebook/warmup-task/execute-now', data }),
  page: (params: any) => request.get({ url: '/facebook/warmup-task/page', params }),
  delete: (id: string | number) => request.delete({ url: '/facebook/warmup-task/delete', params: { id } }),
  claimPending: (limit = 10) => request.get({ url: '/facebook/warmup-task/claim-pending', params: { limit } }),
  reportDetail: (detailId: string | number, success: boolean, errorMessage?: string) =>
    request.post({ url: '/facebook/warmup-task/report-detail', params: { detailId, success, errorMessage } })
}
