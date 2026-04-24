import request from '@/config/axios'

/**
 * Facebook 每日限制 API
 */
export const DailyLimitApi = {
  // 获取所有限制配置
  getConfig: () => {
    return request.get({ url: '/facebook/daily-limit/config' })
  },

  // 获取剩余次数
  getRemainingCount: (accountId: string, type: string) => {
    return request.get({ url: `/facebook/daily-limit/remaining/${accountId}/${type}` })
  },

  // 获取所有操作的剩余次数
  getAllRemainingCounts: (accountId: string) => {
    return request.get({ url: `/facebook/daily-limit/all-remaining/${accountId}` })
  }
}
