import request from '@/config/axios'
import type { Dayjs } from 'dayjs';

/** FB帖子采集结果信息 */
export interface FbCollectPost {
          id: number; // id
          taskId: number; // 任务id
          itemId: string; // 帖子唯一id
          postUser: string; // 发贴人
          url: string; // 帖子链接
          fromResource: string; // 帖子来源
          groupName: string; // 群名
          sysGroupId: number; // 系统分组
          userId: number; // 系统用户id
          deptId: number; // 部门id
          groupId: number; // 采集回来的群id
          reshareCount: number; // 转发数
          commentCount: number; // 评论数
          reactionCount: number; // 点赞数
          usedCount: number; // 截流次数
          postContent: string; // 帖子内容
          fbAccount: string; // FB账号
          postCreateTime: string | Dayjs; // 帖子创建时间
  }

// FB帖子采集结果 API
export const FbCollectPostApi = {
  // 查询FB帖子采集结果分页
  getFbCollectPostPage: async (params: any) => {
    return await request.get({ url: `/facebook/fb-collect-post/page`, params })
  },

  // 查询FB帖子采集结果详情
  getFbCollectPost: async (id: number) => {
    return await request.get({ url: `/facebook/fb-collect-post/get?id=` + id })
  },

  // 新增FB帖子采集结果
  createFbCollectPost: async (data: FbCollectPost) => {
    return await request.post({ url: `/facebook/fb-collect-post/create`, data })
  },

  // 修改FB帖子采集结果
  updateFbCollectPost: async (data: FbCollectPost) => {
    return await request.put({ url: `/facebook/fb-collect-post/update`, data })
  },

  // 删除FB帖子采集结果
  deleteFbCollectPost: async (id: number) => {
    return await request.delete({ url: `/facebook/fb-collect-post/delete?id=` + id })
  },

  /** 批量删除FB帖子采集结果 */
  deleteFbCollectPostList: async (ids: number[]) => {
    return await request.delete({ url: `/facebook/fb-collect-post/delete-list?ids=${ids.join(',')}` })
  },

  // 导出FB帖子采集结果 Excel
  exportFbCollectPost: async (params) => {
    return await request.download({ url: `/facebook/fb-collect-post/export-excel`, params })
  },

  // 批量保存采集结果
  batchSaveFbCollectPost: async (data: { detailId: number; results: any[] }) => {
    return await request.post({ url: `/facebook/fb-collect-post/batch-save`, data })
  },
}
