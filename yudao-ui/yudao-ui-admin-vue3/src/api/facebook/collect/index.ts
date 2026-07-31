import request from '@/config/axios'
import type { Dayjs } from 'dayjs'

/** FB采集任务信息 */
export interface FbCollect {
  id: number // 任务ID
  fbAccount: string // FB账号
  taskType: number // 任务类型
  searchUrl?: string // 采集链接
  searchType: number // 搜索类型(0链接 1关键词 )
  expectedCount: number // 期望采集数量
  intervalSeconds: number // 采集间隔(秒)
  status: number // 状态（0待执行 1采集中 2已完成 3已失败 4已取消）
  collectedCount: number // 已采集数量
  errorMessage: string // 错误信息
  startTime: string | Dayjs // 开始时间
  endTime: string | Dayjs // 结束时间
  remark: string // 备注
  sourceUserIds?: Array<string | number> // 深度采集来源资源库用户ID
  accountSelectionMode?: 'AUTO' | 'MANUAL'
  accountIds?: Array<string | number>
}

// FB采集任务 API
export const FbCollectApi = {
  // 查询FB采集任务分页
  getFbCollectPage: async (params: any) => {
    return await request.get({ url: `/facebook/fb-collect/page`, params })
  },

  // 查询FB采集任务详情
  getFbCollect: async (id: number | string) => {
    return await request.get({ url: `/facebook/fb-collect/get?id=` + id })
  },

  // 查询账号的待执行明细
  getPendingDetails: async (fbAccount: string, taskId?: string | number) => {
    return await request.get({ url: `/facebook/fb-collect-detail/pending`, params: { fbAccount, taskId } })
  },

  // 查询采集明细
  getCollectDetail: async (id: string | number) => {
    return await request.get({ url: `/facebook/fb-collect-detail/get`, params: { id } })
  },

  // 根据任务ID查询明细列表
  getDetailListByTaskId: async (taskId: string | number) => {
    return await request.get({ url: `/facebook/fb-collect-detail/list-by-task`, params: { taskId } })
  },

  // 标记采集明细失败
  markDetailFailed: async (data: { detailId: string | number; errorMessage?: string }) => {
    return await request.post({ url: `/facebook/fb-collect-detail/fail`, params: data })
  },

  // 新增FB采集任务
  createFbCollect: async (data: FbCollect) => {
    return await request.post({ url: `/facebook/fb-collect/create`, data })
  },

  // 修改FB采集任务
  updateFbCollect: async (data: FbCollect) => {
    return await request.put({ url: `/facebook/fb-collect/update`, data })
  },

  // 删除FB采集任务
  deleteFbCollect: async (id: number) => {
    return await request.delete({ url: `/facebook/fb-collect/delete?id=` + id })
  },

  /** 批量删除FB采集任务 */
  deleteFbCollectList: async (ids: number[]) => {
    return await request.delete({ url: `/facebook/fb-collect/delete-list?ids=${ids.join(',')}` })
  },

  // 导出FB采集任务 Excel
  exportFbCollect: async (params) => {
    return await request.download({ url: `/facebook/fb-collect/export-excel`, params })
  }
}
