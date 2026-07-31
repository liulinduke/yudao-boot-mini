import request from '@/config/axios'
import type { Dayjs } from 'dayjs';

export interface FbAccountUpdateProxyReqVO {
  ids: Array<number | string>
  proxyId: number | null
}

export interface FbAccountUpdateGroupReqVO {
  ids: Array<number | string>
  groupId: number | string | null
}

/** FB账号信息 */
export interface FbAccount {
          id: string | number; // id
          fbAccount?: string; // FB账号
          password: string; // 密码
          area: string; // 地区
          friends: number; // 好友数
          groupId: number; // 账户分组
          status: boolean; // 账户状态
          loginStatus?: string; // 登录状态
          loginErrorReason?: string; // 登录失败原因
          lastLoginTime?: string | Dayjs; // 最近登录时间
          avatarUrl?: string; // Facebook头像
          coverUrl?: string; // Facebook主页封面
          profileNickname?: string; // Facebook主页昵称
          profileSignature?: string; // Facebook个人签名
          profileUpdateStatus?: string; // 资料上传状态
          profileUpdateError?: string; // 资料上传失败原因
          remark: string; // 备注
          cookie: string; // cookie
          userAgent: string; // 用户代理
          tfa: string; // 2FA
          email: string; // 邮件信息
          emailPassword: string; // 邮箱密码
          deviceId: string; // 设备ID，雪花ID必须按字符串传输
          deviceName: string; // 设备名称
          reason: string; // 异常原因
          proxy: string; // 代理
          proxyId: number; // 代理ID
          language?: number; // 旧语言字段，仅兼容历史数据
          languageCode?: string; // Facebook界面语言代码
          creationDate: string | Dayjs; // 注册日期
  }

export interface FbAccountSelectorOption {
  id: string | number
  fbAccount?: string
  groupId?: string | number | null
  status?: boolean
  loginStatus?: string
  eligible?: boolean
  disabledReason?: string
  today?: Record<string, number>
  limits?: Record<string, number>
  total?: Record<string, number>
  lastExecuteTime?: string
}

// FB账号 API
export const FbAccountApi = {
  // 查询FB账号分页
  getFbAccountPage: async (params: any) => {
    return await request.get({ url: `/facebook/fb-account/page`, params })
  },

  getSelectorOptions: async (params: any) => {
    return await request.get({ url: `/facebook/fb-account/selector-options`, params })
  },

  // 查询FB账号详情
  getFbAccount: async (id: number) => {
    return await request.get({ url: `/facebook/fb-account/get?id=` + id })
  },

  // 新增FB账号
  createFbAccount: async (data: FbAccount) => {
    return await request.post({ url: `/facebook/fb-account/create`, data })
  },

  // 修改FB账号
  updateFbAccount: async (data: FbAccount) => {
    return await request.put({ url: `/facebook/fb-account/update`, data })
  },

  // 删除FB账号
  deleteFbAccount: async (id: number) => {
    return await request.delete({ url: `/facebook/fb-account/delete?id=` + id })
  },

  /** 批量删除FB账号 */
  deleteFbAccountList: async (ids: number[]) => {
    return await request.delete({ url: `/facebook/fb-account/delete-list?ids=${ids.join(',')}` })
  },

  // 导出FB账号 Excel
  exportFbAccount: async (params) => {
    return await request.download({ url: `/facebook/fb-account/export-excel`, params })
  },

  // 更新FB账号语言设置
  updateFbAccountLanguage: async (id: number, languageCode: string) => {
    return await request.put({
      url: `/facebook/fb-account/update-language`,
      params: { id, languageCode }
    })
  },

  /** 批量更新FB账号代理 */
  updateFbAccountProxy: async (data: FbAccountUpdateProxyReqVO) => {
    return await request.put({ url: `/facebook/fb-account/update-proxy`, data })
  },

  /** 批量更新FB账号分组 */
  updateFbAccountGroup: async (data: FbAccountUpdateGroupReqVO) => {
    return await request.put({ url: `/facebook/fb-account/update-group`, data })
  },

  /** 保存Facebook账号资料上传任务 */
  uploadFbAccountProfile: async (data: {
    items: Array<{
      accountId: number | string
      avatarUrl?: string
      coverUrl?: string
      nickname?: string
      signature?: string
    }>
  }) => {
    return await request.post({ url: `/facebook/fb-account/profile/upload`, data })
  },

  /** 回报Facebook账号资料上传结果 */
  reportFbAccountProfile: async (data: {
    accountId: number | string
    status: string
    errorMessage?: string
    avatarUrl?: string
    coverUrl?: string
    nickname?: string
    signature?: string
  }) => {
    return await request.post({ url: `/facebook/fb-account/profile/report`, data })
  },
}
