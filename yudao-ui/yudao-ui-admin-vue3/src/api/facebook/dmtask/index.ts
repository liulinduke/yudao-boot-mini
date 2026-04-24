import request from '@/config/axios'

/**
 * Facebook 群发私信任务 API
 */
export const DmTaskApi = {
  // 创建群发私信任务
  createDmTask: (data: any) => {
    return request.post({ url: '/facebook/dm-task/create', data })
  },

  // 更新群发私信任务
  updateDmTask: (data: any) => {
    return request.put({ url: '/facebook/dm-task/update', data })
  },

  // 删除群发私信任务
  deleteDmTask: (id: number) => {
    return request.delete({ url: '/facebook/dm-task/delete', params: { id } })
  },

  // 获得群发私信任务
  getDmTask: (id: number) => {
    return request.get({ url: '/facebook/dm-task/get', params: { id } })
  },

  // 获得群发私信任务分页
  getDmTaskPage: (params: any) => {
    return request.get({ url: '/facebook/dm-task/page', params })
  },

  // 启动任务
  startTask: (id: number) => {
    return request.post({ url: `/facebook/dm-task/start/${id}` })
  },

  // 取消任务
  cancelTask: (id: number) => {
    return request.post({ url: `/facebook/dm-task/cancel/${id}` })
  }
}
