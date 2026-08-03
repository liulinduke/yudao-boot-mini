import request from '@/config/axios'
import type { Dayjs } from 'dayjs'

/** FB个人主页采集结果信息 */
export interface FbCollectUser {
  id: number // 结果ID
  taskId?: number // 任务ID
  userId: number // 系统用户ID
  deptId: number // 部门ID
  fbAccount?: string // FB账号
  fbUserId: string // Facebook用户ID
  userName: string // 用户名称
  avatar?: string // 头像URL
  url: string // 主页链接
  dataType: number // 数据类型(0个人 1公共)
  followers?: number | null // 粉丝数
  city: string // 所在地
  location: string // 居住地
  hometown: string // 家乡
  phonenumber: string // 手机1
  phonenumber2: string // 手机2
  email: string // 邮箱1
  email2: string // 邮箱2
  wechat: string // 微信
  whatsapp: string // WhatsApp
  line: string // Line
  website: string // 社交网站
  profileStatus: string // 签名/状态
  language: string // 语言
  gender: string // 性别
  category?: string // 账户类别
  relationship: string // 婚姻状况
  workExperience: string // 工作经历
  education: string // 学历
  lastPostTime: string | Dayjs // 最近发帖时间
  lastPostSummary: string // 最近帖子摘要
  deepCollected?: boolean // 是否已深度采集
  groupId: number // 分组ID
  resourceGroupId?: number // 资源分组ID
  fromResource: string // 数据来源
  config: string // 配置信息
  commentContent?: string // 评论内容
  sourcePostId?: string | number // 来源帖子ID
  sourcePostUrl?: string // 来源帖子URL
  syncTime: string | Dayjs // 同步时间
  createTime?: string | Dayjs // 采集时间
  aiTags?: string // AI标签
  intentLevel?: string // 意向等级
  intentReason?: string // 意向判断理由
  sentiment?: string // 情绪
  leadType?: string // 线索类型
  country?: string // 国家
  productRelevanceScore?: number // 产品相关度
  aiSummary?: string // AI摘要
  lastAiAnalyzeTime?: string | Dayjs // 最近AI分析时间
  touchStatus?: string // 触达状态
  lastTouchTime?: string | Dayjs // 最近触达时间
}

// FB个人主页采集结果 API
export const FbCollectUserApi = {
  // 查询FB个人主页采集结果分页
  getFbCollectUserPage: async (params: any) => {
    return await request.get({ url: `/facebook/fb-collect-user/page`, params })
  },

  // 查询FB个人主页采集结果详情
  getFbCollectUser: async (id: number) => {
    return await request.get({ url: `/facebook/fb-collect-user/get?id=` + id })
  },

  // 新增FB个人主页采集结果
  createFbCollectUser: async (data: FbCollectUser) => {
    return await request.post({ url: `/facebook/fb-collect-user/create`, data })
  },

  // 修改FB个人主页采集结果
  updateFbCollectUser: async (data: FbCollectUser) => {
    return await request.put({ url: `/facebook/fb-collect-user/update`, data })
  },

  // 删除FB个人主页采集结果
  deleteFbCollectUser: async (id: number) => {
    return await request.delete({ url: `/facebook/fb-collect-user/delete?id=` + id })
  },

  /** 批量删除FB个人主页采集结果 */
  deleteFbCollectUserList: async (ids: number[]) => {
    return await request.delete({
      url: `/facebook/fb-collect-user/delete-list?ids=${ids.join(',')}`
    })
  },

  // 导出FB个人主页采集结果 Excel
  exportFbCollectUserExcel: async (params) => {
    return await request.download({ url: `/facebook/fb-collect-user/export-excel`, params })
  },

  // 批量保存采集结果
  batchSaveFbCollectUser: async (data: { detailId: number; results: any[] }) => {
    return await request.post({ url: `/facebook/fb-collect-user/batch-save`, data })
  }
}
