package cn.iocoder.yudao.module.facebook.service.operation;

import cn.iocoder.yudao.framework.common.pojo.PageResult;
import cn.iocoder.yudao.module.facebook.controller.admin.operation.vo.FbOperationAddGroupResultPageReqVO;
import cn.iocoder.yudao.module.facebook.controller.admin.operation.vo.FbOperationAddGroupResultRespVO;

import java.util.List;

/**
 * 链接加组结果 Service 接口
 *
 * @author 芋道源码
 */
public interface FbOperationAddGroupResultService {

    /**
     * 获得加组结果分页
     *
     * @param pageReqVO 分页查询
     * @return 加组结果分页
     */
    PageResult<FbOperationAddGroupResultRespVO> getAddGroupResultPage(FbOperationAddGroupResultPageReqVO pageReqVO);

    /**
     * 根据任务ID获得加组结果列表
     *
     * @param taskId 任务ID
     * @return 加组结果列表
     */
    List<FbOperationAddGroupResultRespVO> getAddGroupResultByTaskId(Long taskId);

}
