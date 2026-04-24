import request from '@/config/axios'

export interface FbScriptVO {
  id?: number
  groupId?: number
  scriptTitle?: string
  scriptContent: string
  contentType?: number // 1-文本 2-图文 3-视频 4-音频
  attachments?: string
  sendSequence?: boolean
  sort?: number
  remark?: string
  createTime?: string
}

export interface FbScriptPageReqVO extends PageParam {
  groupId?: number
  contentType?: number
}

// 查询话术分页
export const getScriptPage = (params: FbScriptPageReqVO) => {
  return request.get({ url: '/social-matrix/script/page', params })
}

// 查询话术列表（根据分组）
export const getScriptListByGroup = (groupId: number) => {
  return request.get({ url: '/social-matrix/script/list-by-group', params: { groupId } })
}

// 查询话术详情
export const getScript = (id: number) => {
  return request.get({ url: `/social-matrix/script/get?id=${id}` })
}

// 新增话术
export const createScript = (data: FbScriptVO) => {
  return request.post({ url: '/social-matrix/script/create', data })
}

// 修改话术
export const updateScript = (data: FbScriptVO) => {
  return request.put({ url: '/social-matrix/script/update', data })
}

// 删除话术
export const deleteScript = (id: number) => {
  return request.delete({ url: `/social-matrix/script/delete?id=${id}` })
}

// 批量删除话术
export const deleteScriptList = (ids: number[]) => {
  return request.delete({ url: `/social-matrix/script/delete-list?ids=${ids.join(',')}` })
}

// 话术 API
export const ScriptApi = {
  getScriptPage,
  getScriptListByGroup,
  getScript,
  createScript,
  updateScript,
  deleteScript,
  deleteScriptList
}
