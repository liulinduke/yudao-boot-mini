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

  updateFbAccountGroup: (data: FbAccountUpdateGroupReqVO) => {
    return request.put({ url: '/facebook/fb-account/update-group', data })
  },

  updateFbAccountStatus: (data: FbAccountBatchStatusReqVO) => {
    return request.put({ url: '/facebook/fb-account/update-status', data })
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

  uploadFbAccountProfile: (data: FbAccountProfileUploadReqVO) => {
    return request.post({ url: '/facebook/fb-account/profile/upload', data })
  },

  reportFbAccountProfile: (data: FbAccountProfileReportReqVO) => {
    return request.post({ url: '/facebook/fb-account/profile/report', data })
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
  status?: boolean | number | string
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
  id?: number | string
  fbAccount?: string
  password?: string
  area?: string
  friends?: number
  groupId?: number
  groupName?: string
  status?: boolean | number | string
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

/** 已启用且正常或尚未检测的账号可用于采集、运营、私信、AI 获客。 */
export const isFbAccountSelectable = (
  account?: Pick<FbAccount, 'status' | 'loginStatus' | 'loginErrorReason'> | null
) => {
  if (!account) return false

  const enabled = account.status === true || account.status === 1 || String(account.status).toLowerCase() === 'true' || String(account.status) === '1'
  if (!enabled) return false

  const status = String(account?.loginStatus || '').trim().toUpperCase()
  const reason = String(account?.loginErrorReason || '')
  return (
    (status === '' || status === 'PENDING' || status === 'SUCCESS') &&
    !['COOKIE_INVALID', 'COOKIE_EXPIRED', 'ABNORMAL', 'INVALID'].includes(status) &&
    !/cookie\s*(已)?失效|cookie\s*expired|登录页|checkpoint|账号被封/i.test(reason)
  )
}

export const filterSelectableFbAccounts = <
  T extends Pick<FbAccount, 'status' | 'loginStatus' | 'loginErrorReason'>
>(accounts: T[]) =>
  accounts.filter(isFbAccountSelectable)

export interface FbAccountUpdateProxyReqVO {
  ids: number[]
  proxyId: number | null
}

export interface FbAccountUpdateGroupReqVO {
  ids: Array<number | string>
  groupId: number | string | null
}

export interface FbAccountBatchStatusReqVO {
  ids: Array<number | string>
  status: boolean
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
  id: number | string
  loginStatus: string
  loginErrorReason?: string
  cookie?: string
}

export interface FbAccountProfileUploadReqVO {
  items: Array<{
    accountId: number | string
    avatarUrl?: string
    coverUrl?: string
    nickname?: string
    signature?: string
  }>
}

export interface FbAccountProfileReportReqVO {
  accountId: number | string
  status: string
  errorMessage?: string
  avatarUrl?: string
  coverUrl?: string
  nickname?: string
  signature?: string
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
