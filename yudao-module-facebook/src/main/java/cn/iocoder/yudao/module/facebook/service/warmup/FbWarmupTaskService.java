package cn.iocoder.yudao.module.facebook.service.warmup;

import cn.iocoder.yudao.framework.common.pojo.PageResult;
import cn.iocoder.yudao.module.facebook.controller.admin.warmup.vo.*;

import java.util.List;

public interface FbWarmupTaskService {
    Long create(FbWarmupTaskSaveReqVO req, boolean immediate);
    PageResult<FbWarmupTaskRespVO> page(FbWarmupTaskPageReqVO req);
    void delete(Long id);
    List<FbWarmupPendingDetailRespVO> claimPending(Integer limit);
    void reportDetail(Long detailId, Boolean success, String errorMessage);
    void markReady(Long taskId);
}
