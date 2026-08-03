import request from '@/config/axios'

export interface FbOperationTask {
  id?: string | number
  taskType: number // 任务类型（9-链接加组 10-转贴 12-发个人帖 13-发群帖 14-群发私信 15-帖子评论 16-刷粉）
  taskName?: string
  status?: number // 任务状态（0-待执行 1-执行中 2-已完成 3-已停止 4-失败）
  expectedCount: number
  actualCount?: number
  accountIds?: string
  accountSelectionMode?: 'AUTO' | 'MANUAL'
  autoAccountCount?: number
  startTime?: string
  endTime?: string
  remark?: string
  createTime?: string
  actionConfig?: string
  sourceType?: 'operation' | 'dm'
}

export interface FbOperationTaskDetail {
  id?: string | number
  taskId?: string | number
  accountId: string
  fbAccount?: string
  targetUrls?: string
  targetGroupIds?: string
  postUrl?: string
  actionConfig?: string
  commentScript?: string
  scriptLibraryId?: number
  expectedCount: number
  actualCount?: number
  status?: number
  startTime?: string
  endTime?: string
  errorMsg?: string
  targetUserId?: string
  scriptContent?: string
  sendTime?: string
  createTime?: string
}

export interface FbOperationAddGroupResult {
  id?: string | number
  detailId?: string | number
  taskId?: string | number
  accountId: string
  fbAccount?: string
  targetUrl?: string
  groupId?: string
  groupName?: string
  groupUrl?: string
  joinStatus?: number
  failReason?: string
  joinTime?: string
  syncTime?: string
  createTime?: string
}

export interface FbOperationTaskPageReqVO {
  pageNo: number
  pageSize: number
  taskType?: number
  status?: number
  createTime?: string[]
}

export interface FbOperationTaskSaveReqVO {
  id?: number
  taskType: number
  taskName?: string
  accountIds: string[]
  accountSelectionMode?: 'AUTO' | 'MANUAL'
  autoAccountCount?: number
  targetUrls?: string
  targetGroupIds?: string
  postUrl?: string
  postUrls?: string[]
  actionConfig?: any
  commentScript?: string
  scriptLibraryId?: number
  expectedCount: number
  remark?: string
}

export interface FbOperationAddGroupResultBatchSaveReqVO {
  detailId: string | number
  results: FbOperationAddGroupResultItem[]
}

export interface FbOperationAddGroupResultItem {
  accountId?: string
  fbAccount?: string
  targetUrl?: string
  groupId?: string
  groupName?: string
  groupUrl?: string
  joinStatus?: number
  failReason?: string
  joinTime?: string
  syncTime?: string
}

export interface FbRepostResult {
  id?: number
  detailId?: number
  taskId?: number
  accountId: string
  fbAccount?: string
  postUrl?: string
  actionType?: number
  targetType?: string
  targetId?: string
  targetName?: string
  targetUrl?: string
  status?: number
  failReason?: string
  executeTime?: string
  remark?: string
  createTime?: string
}

export interface FbRepostResultBatchSaveReqVO {
  detailId: number | string
  results: FbRepostResultItem[]
}

export interface FbRepostResultItem {
  accountId?: string
  fbAccount?: string
  postUrl?: string
  actionType?: number
  targetType?: string
  targetId?: string
  targetName?: string
  targetUrl?: string
  status?: number
  failReason?: string
  executeTime?: string
  remark?: string
}

export interface FbOperationTaskDetailRespVO {
  task: FbOperationTask
  details: FbOperationTaskDetail[]
  results?: FbOperationAddGroupResult[]
  repostResults?: FbRepostResult[]
  groupPublishResults?: any[]
}

export const getFbOperationTaskPage = (params: FbOperationTaskPageReqVO) => {
  return request.get({ url: '/facebook/fb-operation-task/page', params })
}

export const getFbOperationTask = (id: string | number) => {
  return request.get({ url: '/facebook/fb-operation-task/get', params: { id } })
}

export const createFbOperationTask = (data: FbOperationTaskSaveReqVO) => {
  return request.post({ url: '/facebook/fb-operation-task/create', data })
}

export const updateFbOperationTask = (data: FbOperationTaskSaveReqVO) => {
  return request.put({ url: '/facebook/fb-operation-task/update', data })
}

export const deleteFbOperationTask = (id: number) => {
  return request.delete({ url: '/facebook/fb-operation-task/delete', params: { id } })
}

export const batchSaveAddGroupResult = (data: FbOperationAddGroupResultBatchSaveReqVO) => {
  return request.post({ url: '/facebook/fb-operation-task/batch-save-add-group-result', data })
}

export const getPendingDetails = (fbAccount: string) => {
  return request.get({ url: '/facebook/fb-operation-task/pending-details', params: { fbAccount } })
}

export const getFollowedAccountIds = (targetUrl: string) => {
  return request.get({ url: '/facebook/fb-operation-task/followed-account-ids', params: { targetUrl } })
}

export const batchSaveRepostResult = (data: FbRepostResultBatchSaveReqVO) => {
  return request.post({ url: '/facebook/fb-operation-task/batch-save-repost-result', data })
}

export const markOperationDetailFailed = (data: { detailId: string | number; errorMsg?: string }) => {
  return request.post({ url: '/facebook/fb-operation-task/detail-fail', params: data })
}
