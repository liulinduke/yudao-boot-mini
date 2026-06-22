import request from '@/config/axios'

export const FbAccountApi = {
  getFbAccountPage: (params: FbAccountPageReqVO) => {
    return request.get({ url: '/facebook/fb-account/page', params })
  },

  getFbAccount: (id: number) => {
    return request.get({ url: '/facebook/fb-account/get', params: { id } })
  },

  createFbAccount: (data: FbAccount) => {
    return request.post({ url: '/facebook/fb-account/create', data })
  },

  updateFbAccount: (data: FbAccount) => {
    return request.put({ url: '/facebook/fb-account/update', data })
  },

  deleteFbAccount: (id: number) => {
    return request.delete({ url: '/facebook/fb-account/delete', params: { id } })
  },

  deleteFbAccountList: (ids: number[]) => {
    return request.delete({ url: '/facebook/fb-account/delete-list', params: { ids } })
  },

  exportFbAccount: (params: FbAccountPageReqVO) => {
    return request.get({ url: '/facebook/fb-account/export', params, responseType: 'blob' })
  },

  updateFbAccountProxy: (data: FbAccountUpdateProxyReqVO) => {
    return request.put({ url: '/facebook/fb-account/update-proxy', data })
  },

  importFbAccount: (data: FbAccountImportReqVO) => {
    return request.post({ url: '/facebook/fb-account/import', data })
  },

  importFbAccountCookie: (data: FbAccountCookieImportReqVO) => {
    return request.post({ url: '/facebook/fb-account/import-cookie', data })
  },

  updateFbAccountLoginResult: (data: FbAccountLoginResultUpdateReqVO) => {
    return request.put({ url: '/facebook/fb-account/update-login-result', data })
  },
}

export interface FbAccountPageReqVO {
  pageNo: number
  pageSize: number
  fbAccount?: string
  password?: string
  area?: string
  friends?: number
  groupId?: number | null
  status?: string
  remark?: string
  cookie?: string
  userAgent?: string
  tfa?: string
  loginStatus?: string
  loginErrorReason?: string
  lastLoginTime?: string
  email?: string
  emailPassword?: string
  deviceId?: string
  deviceName?: string
  reason?: string
  proxy?: string
  proxyId?: number
  creationDate?: string[]
  createTime?: string[]
}

export interface FbAccount {
  id?: number
  fbAccount?: string
  password?: string
  area?: string
  friends?: number
  groupId?: number
  groupName?: string
  status?: string
  remark?: string
  cookie?: string
  userAgent?: string
  tfa?: string
  loginStatus?: string
  loginErrorReason?: string
  lastLoginTime?: string
  email?: string
  emailPassword?: string
  deviceId?: string
  deviceName?: string
  reason?: string
  proxy?: string
  proxyId?: number
  proxyName?: string
  language?: number
  creationDate?: string
  createTime?: string
}

export interface FbAccountUpdateProxyReqVO {
  ids: number[]
  proxyId: number | null
}

export interface FbAccountImportReqVO {
  data: string
  groupId?: number | null
  proxyId?: number | null
}

export interface FbAccountCookieImportReqVO {
  data: string
  groupId?: number | null
  proxyId?: number | null
  useSessionCookie?: boolean
}

export interface FbAccountLoginResultUpdateReqVO {
  id: number
  loginStatus: string
  loginErrorReason?: string
  cookie?: string
}

export interface FbAccountImportPreviewVO {
  no: number
  userName?: string
  password?: string
  securityKey?: string
  error?: string
}

export interface FbAccountCookieImportPreviewVO {
  no: number
  id?: string
  cookie?: string
  error?: string
}
