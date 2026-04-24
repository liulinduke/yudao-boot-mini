import request from '@/config/axios'

export interface FbOperationAddGroupResult {
  id?: number
  detailId?: number
  taskId?: number
  accountId: string
  fbAccount?: string
  targetUrl?: string
  groupId?: string
  groupName?: string
  groupUrl?: string
  joinStatus?: number // 0-待处理 1-成功 2-失败 3-已加入/待审核
  failReason?: string
  joinTime?: string
  syncTime?: string
  createTime?: string
}

export interface FbOperationAddGroupResultPageReqVO {
  pageNo: number
  pageSize: number
  taskId?: number
  detailId?: number
  accountId?: string
  joinStatus?: number
  groupId?: string
  groupName?: string
}

// 查询加组结果分页
export const getAddGroupResultPage = (params: FbOperationAddGroupResultPageReqVO) => {
  return request.get({ url: '/facebook/fb-operation-add-group-result/page', params })
}

// 根据任务ID查询加组结果列表
export const getAddGroupResultByTaskId = (taskId: number) => {
  return request.get({ url: '/facebook/fb-operation-add-group-result/list-by-task', params: { taskId } })
}

// 加组结果 API
export const FbOperationAddGroupResultApi = {
  getAddGroupResultPage,
  getAddGroupResultByTaskId
}
