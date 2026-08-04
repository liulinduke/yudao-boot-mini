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
  publishCount?: number
  lastPublishTime?: string
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
  accountIds?: string[]
  resourceGroupId?: number
  joinedBeforeDays?: number
}

export interface FbOperationGroupSelectorAccountReqVO {
  accountSelectionMode?: 'AUTO' | 'MANUAL'
  accountIds?: string[]
  targetAccountCount: number
  minGroupCount?: number
  joinedBeforeDays?: number
  resourceGroupId?: number
  groupName?: string
  actionType?: 'group_post' | 'repost'
}

// 查询加组结果分页
export const getAddGroupResultPage = (params: FbOperationAddGroupResultPageReqVO) => {
  return request.get({ url: '/facebook/fb-operation-add-group-result/page', params })
}

// 根据任务ID查询加组结果列表
export const getAddGroupResultByTaskId = (taskId: number) => {
  return request.get({ url: '/facebook/fb-operation-add-group-result/list-by-task', params: { taskId } })
}

// 根据账号分配模式和已加入群组条件，解析真正可以执行群组操作的账号。
export const getSelectorAccounts = (params: FbOperationGroupSelectorAccountReqVO) => {
  return request.post({ url: '/facebook/fb-operation-add-group-result/selector-accounts', data: params })
}

// 加组结果 API
export const FbOperationAddGroupResultApi = {
  getAddGroupResultPage,
  getAddGroupResultByTaskId,
  getSelectorAccounts
}
