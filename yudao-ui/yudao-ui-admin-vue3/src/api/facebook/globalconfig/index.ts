import request from '@/config/axios'

/**
 * Facebook 全局配置 API
 */
export const GlobalConfigApi = {
  // 保存配置
  saveConfig: (data: any) => {
    return request.post({ url: '/facebook/global-config/save', data })
  },

  // 批量保存配置
  batchSaveConfigs: (data: any[]) => {
    return request.post({ url: '/facebook/global-config/batch-save', data })
  },

  // 获取所有配置
  getAllConfigs: () => {
    return request.get({ url: '/facebook/global-config/all' })
  },

  // 获取配置值
  getConfigValue: (configKey: string) => {
    return request.get({ url: '/facebook/global-config/value', params: { configKey } })
  }
}
