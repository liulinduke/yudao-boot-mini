
import request from '@/config/axios'

/**
 * 代理管理 API
 */
export const SysProxyApi = {
  /**
   * 分页查询代理列表
   */
  getProxyPage: (params: SysProxyPageReqVO) => {
    return request.get({ url: '/system/proxy/page', params })
  },

  /**
   * 获取代理详情
   */
  getProxy: (id: number) => {
    return request.get({ url: '/system/proxy/get', params: { id } })
  },

  /**
   * 创建代理
   */
  createProxy: (data: SysProxyCreateReqVO) => {
    return request.post({ url: '/system/proxy/create', data })
  },

  /**
   * 更新代理
   */
  updateProxy: (data: SysProxyUpdateReqVO) => {
    return request.put({ url: '/system/proxy/update', data })
  },

  /**
   * 删除代理
   */
  deleteProxy: (id: number) => {
    return request.delete({ url: '/system/proxy/delete', params: { id } })
  },

  /**
   * 获取所有启用的代理列表
   */
  getAllEnabledProxyList: () => {
    return request.get({ url: '/system/proxy/list' })
  },
}

// 请求和响应类型
export interface SysProxyPageReqVO {
  proxyName?: string
  proxyType?: number
  host?: string
  status?: number
  country?: string
}

export interface SysProxyCreateReqVO {
  proxyName: string
  proxyType: number
  host: string
  port: number
  username?: string
  password?: string
  country?: string
  status?: number
  remark?: string
}

export interface SysProxyUpdateReqVO extends SysProxyCreateReqVO {
  id: number
}

export interface SysProxyRespVO {
  id: number
  proxyName: string
  proxyType: number
  proxyTypeName: string
  host: string
  port: number
  username?: string
  country?: string
  status: number
  statusName: string
  remark?: string
  createTime: string
}
