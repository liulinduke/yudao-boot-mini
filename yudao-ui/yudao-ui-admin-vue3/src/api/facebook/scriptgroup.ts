import request from '@/config/axios'

export interface FbScriptGroupVO {
  id: number
  groupName: string
  groupType: number
  sort: number
  remark: string
  createTime: string
}

export interface FbScriptGroupPageReqVO extends PageParam {
  groupName?: string
  groupType?: number
}

// 查询话术分组分页
export const getScriptGroupPage = (params: FbScriptGroupPageReqVO) => {
  return request.get({ url: '/social-matrix/script-group/page', params })
}

// 查询话术分组列表
export const getScriptGroupList = () => {
  return request.get({ url: '/social-matrix/script-group/list' })
}

// 查询话术分组详情
export const getScriptGroup = (id: number) => {
  return request.get({ url: '/social-matrix/script-group/get?id=' + id })
}

// 新增话术分组
export const createScriptGroup = (data: FbScriptGroupVO) => {
  return request.post({ url: '/social-matrix/script-group/create', data })
}

// 修改话术分组
export const updateScriptGroup = (data: FbScriptGroupVO) => {
  return request.put({ url: '/social-matrix/script-group/update', data })
}

// 删除话术分组
export const deleteScriptGroup = (id: number) => {
  return request.delete({ url: '/social-matrix/script-group/delete?id=' + id })
}

// 批量删除话术分组
export const deleteScriptGroupList = (ids: number[]) => {
  return request.delete({ url: '/social-matrix/script-group/delete-list?ids=' + ids.join(',') })
}
