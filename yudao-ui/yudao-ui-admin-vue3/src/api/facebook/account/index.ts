import request from '@/config/axios'
import type { Dayjs } from 'dayjs';

/** FB账号信息 */
export interface FbAccount {
          id: number; // id
          fbAccount?: string; // FB账号
          password: string; // 密码
          area: string; // 地区
          friends: number; // 好友数
          groupId: number; // 账户分组
          status: boolean; // 账户状态
          remark: string; // 备注
          cookie: string; // cookie
          userAgent: string; // 用户代理
          tfa: string; // 2FA
          email: string; // 邮件信息
          emailPassword: string; // 邮箱密码
          deviceId: number; // 设备ID
          deviceName: string; // 设备名称
          reason: string; // 异常原因
          proxy: string; // 代理
          proxyId: number; // 代理ID
          creationDate: string | Dayjs; // 注册日期
  }

// FB账号 API
export const FbAccountApi = {
  // 查询FB账号分页
  getFbAccountPage: async (params: any) => {
    return await request.get({ url: `/facebook/fb-account/page`, params })
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
}