package cn.iocoder.yudao.module.facebook.service.operation;

import cn.iocoder.yudao.framework.common.pojo.PageResult;
import cn.iocoder.yudao.module.facebook.controller.admin.operation.vo.*;

import javax.validation.*;
import java.util.List;

/**
 * 运营任务 Service 接口
 *
 * @author 芋道源码
 */
public interface FbOperationTaskService {

    /**
     * 创建运营任务
     *
     * @param createReqVO 创建信息
     * @return 编号
     */
    Long createOperationTask(@Valid FbOperationTaskSaveReqVO createReqVO);

    /**
     * 更新运营任务
     *
     * @param updateReqVO 更新信息
     */
    void updateOperationTask(@Valid FbOperationTaskSaveReqVO updateReqVO);

    /**
     * 删除运营任务
     *
     * @param id 编号
     */
    void deleteOperationTask(Long id);

    /**
     * 获得运营任务
     *
     * @param id 编号
     * @return 运营任务（包含明细和结果）
     */
    FbOperationTaskDetailRespVO getOperationTask(Long id);

    /**
     * 获得运营任务分页
     *
     * @param pageReqVO 分页查询
     * @return 运营任务分页
     */
    PageResult<FbOperationTaskRespVO> getOperationTaskPage(FbOperationTaskPageReqVO pageReqVO);

    /**
     * 批量保存链接加组结果
     *
     * @param batchSaveReqVO 批量保存请求
     */
    void batchSaveAddGroupResult(@Valid FbOperationAddGroupResultBatchSaveReqVO batchSaveReqVO);

    /**
     * 获取待执行的明细列表
     *
     * @param accountId Facebook账号ID
     * @return 待执行明细列表
     */
    List<FbOperationTaskDetailItemRespVO> getPendingDetails(String accountId);

}
