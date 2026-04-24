import request from '@/config/axios'

/**
 * Facebook 账号分组 API
 */
export const AccountGroupApi = {
  // 创建账号分组
  createAccountGroup: (data: any) => {
    return request.post({ url: '/facebook/account-group/create', data })
  },

  // 更新账号分组
  updateAccountGroup: (data: any) => {
    return request.put({ url: '/facebook/account-group/update', data })
  },

  // 删除账号分组
  deleteAccountGroup: (id: number) => {
    return request.delete({ url: '/facebook/account-group/delete', params: { id } })
  },

  // 获得账号分组
  getAccountGroup: (id: number) => {
    return request.get({ url: '/facebook/account-group/get', params: { id } })
  },

  // 获得账号分组分页
  getAccountGroupPage: (params: any) => {
    return request.get({ url: '/facebook/account-group/page', params })
  },

  // 获得所有启用的账号分组
  getAllEnabledGroups: () => {
    return request.get({ url: '/facebook/account-group/all-enabled' })
  }
}
