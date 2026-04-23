import request from '@/config/axios'
import type { Dayjs } from 'dayjs';

/** FB群组采集结果信息 */
export interface FbCollectGroup {
          id: number; // id
          taskId: number; // 任务id
          groupName?: string; // 群组名称
          url: string; // 群组链接
          type: string; // 是否公开
          memberQuantity: number; // 成员数量
          activeQuantity: string; // 群活跃
          sysGroupId: number; // 分组
          remark: string; // 备注
          userId: number; // 系统用户
          deptId: number; // 部门
          groupId: number; // 采集回来的群id
          joinGroupTimes: number; // 加组次数
          commentTimes: number; // 评论次数
          postTimes: number; // 发帖次数
  }

// FB群组采集结果 API
export const FbCollectGroupApi = {
  // 查询FB群组采集结果分页
  getFbCollectGroupPage: async (params: any) => {
    return await request.get({ url: `/facebook/fb-collect-group/page`, params })
  },

  // 查询FB群组采集结果详情
  getFbCollectGroup: async (id: number) => {
    return await request.get({ url: `/facebook/fb-collect-group/get?id=` + id })
  },

  // 新增FB群组采集结果
  createFbCollectGroup: async (data: FbCollectGroup) => {
    return await request.post({ url: `/facebook/fb-collect-group/create`, data })
  },

  // 修改FB群组采集结果
  updateFbCollectGroup: async (data: FbCollectGroup) => {
    return await request.put({ url: `/facebook/fb-collect-group/update`, data })
  },

  // 删除FB群组采集结果
  deleteFbCollectGroup: async (id: number) => {
    return await request.delete({ url: `/facebook/fb-collect-group/delete?id=` + id })
  },

  /** 批量删除FB群组采集结果 */
  deleteFbCollectGroupList: async (ids: number[]) => {
    return await request.delete({ url: `/facebook/fb-collect-group/delete-list?ids=${ids.join(',')}` })
  },

  // 导出FB群组采集结果 Excel
  exportFbCollectGroup: async (params) => {
    return await request.download({ url: `/facebook/fb-collect-group/export-excel`, params })
  },

  // 批量保存采集结果
  batchSaveFbCollectGroup: async (data: { detailId: number; results: any[] }) => {
    return await request.post({ url: `/facebook/fb-collect-group/batch-save`, data })
  },
}
