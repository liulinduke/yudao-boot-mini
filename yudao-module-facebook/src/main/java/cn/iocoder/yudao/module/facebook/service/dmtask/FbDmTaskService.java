package cn.iocoder.yudao.module.facebook.service.dmtask;

import cn.iocoder.yudao.framework.common.pojo.PageResult;
import cn.iocoder.yudao.module.facebook.controller.admin.dmtask.vo.FbDmTaskSaveReqVO;
import cn.iocoder.yudao.module.facebook.controller.admin.dmtask.vo.FbDmTaskRespVO;
import cn.iocoder.yudao.module.facebook.controller.admin.dmtask.vo.FbDmTaskPageReqVO;

/**
 * Facebook 群发私信任务 Service 接口
 *
 * @author 芋道源码
 */
public interface FbDmTaskService {

    /**
     * 创建群发私信任务
     *
     * @param saveReqVO 创建信息
     * @return 任务ID
     */
    Long createDmTask(FbDmTaskSaveReqVO saveReqVO);

    /**
     * 更新群发私信任务
     *
     * @param saveReqVO 更新信息
     */
    void updateDmTask(FbDmTaskSaveReqVO saveReqVO);

    /**
     * 删除群发私信任务
     *
     * @param id 编号
     */
    void deleteDmTask(Long id);

    /**
     * 获得群发私信任务
     *
     * @param id 编号
     * @return 任务
     */
    FbDmTaskRespVO getDmTask(Long id);

    /**
     * 获得群发私信任务分页
     *
     * @param pageReqVO 分页查询
     * @return 任务分页
     */
    PageResult<FbDmTaskRespVO> getDmTaskPage(FbDmTaskPageReqVO pageReqVO);

    /**
     * 启动任务执行
     *
     * @param taskId 任务ID
     */
    void startTask(Long taskId);

    /**
     * 取消任务
     *
     * @param taskId 任务ID
     */
    void cancelTask(Long taskId);

    /**
     * 更新任务明细状态
     *
     * @param detailId 明细ID
     * @param status 状态
     * @param errorMsg 错误信息
     */
    void updateDetailStatus(Long detailId, Integer status, String errorMsg);

}
