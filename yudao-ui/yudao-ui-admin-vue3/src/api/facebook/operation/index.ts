import request from '@/config/axios'

export interface FbOperationTask {
  id?: number
  taskType: number // 任务类型（1-链接加组 2-转贴 3-群发私信）
  taskName?: string
  status?: number // 任务状态（0-待执行 1-执行中 2-已完成 3-已停止 4-失败）
  expectedCount: number
  actualCount?: number
  accountIds?: string
  startTime?: string
  endTime?: string
  remark?: string
  createTime?: string
}

export interface FbOperationTaskDetail {
  id?: number
  taskId?: number
  accountId: string
  fbAccount?: string
  targetUrls?: string
  targetGroupIds?: string
  postUrl?: string // 帖子链接
  actionConfig?: string // 执行项配置（JSON格式）
  commentScript?: string // 评论话术
  scriptLibraryId?: number // 话术库ID
  expectedCount: number
  actualCount?: number
  status?: number
  startTime?: string
  endTime?: string
  errorMsg?: string
  createTime?: string
}

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
  joinStatus?: number // 加组状态（0-待处理 1-成功 2-失败 3-已加入）
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
  targetUrls?: string
  targetGroupIds?: string
  postUrl?: string // 帖子链接
  actionConfig?: any // 执行项配置
  commentScript?: string // 评论话术
  scriptLibraryId?: number // 话术库ID
  expectedCount: number
  remark?: string
}

export interface FbOperationAddGroupResultBatchSaveReqVO {
  detailId: number
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

export interface FbOperationTaskDetailRespVO {
  task: FbOperationTask
  details: FbOperationTaskDetail[]
  results: FbOperationAddGroupResult[] | FbRepostResult[] // 根据任务类型返回不同结果
}

// 转帖结果接口
export interface FbRepostResult {
  id?: number
  detailId?: number
  taskId?: number
  accountId: string
  fbAccount?: string
  postUrl?: string
  actionType?: number // 操作类型（1-点赞 2-转发到动态 3-转帖到个人中心 4-转贴到好友 5-转发到群组）
  targetType?: string // friend/group
  targetId?: string
  targetName?: string
  targetUrl?: string
  status?: number // 0-待处理 1-成功 2-失败
  failReason?: string
  executeTime?: string
  remark?: string
  createTime?: string
}

export interface FbRepostResultBatchSaveReqVO {
  detailId: number
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

// 查询运营任务分页
export const getFbOperationTaskPage = (params: FbOperationTaskPageReqVO) => {
  return request.get({ url: '/facebook/fb-operation-task/page', params })
}

// 查询运营任务详情
export const getFbOperationTask = (id: number) => {
  return request.get({ url: '/facebook/fb-operation-task/get', params: { id } })
}

// 新增运营任务
export const createFbOperationTask = (data: FbOperationTaskSaveReqVO) => {
  return request.post({ url: '/facebook/fb-operation-task/create', data })
}

// 修改运营任务
export const updateFbOperationTask = (data: FbOperationTaskSaveReqVO) => {
  return request.put({ url: '/facebook/fb-operation-task/update', data })
}

// 删除运营任务
export const deleteFbOperationTask = (id: number) => {
  return request.delete({ url: '/facebook/fb-operation-task/delete', params: { id } })
}

// 批量保存链接加组结果
export const batchSaveAddGroupResult = (data: FbOperationAddGroupResultBatchSaveReqVO) => {
  return request.post({ url: '/facebook/fb-operation-task/batch-save-add-group-result', data })
}

// 获取待执行的明细列表
export const getPendingDetails = (fbAccount: string) => {
  return request.get({ url: '/facebook/fb-operation-task/pending-details', params: { fbAccount } })
}

// 批量保存转帖结果
export const batchSaveRepostResult = (data: FbRepostResultBatchSaveReqVO) => {
  return request.post({ url: '/facebook/fb-operation-task/batch-save-repost-result', data })
}
