package cn.iocoder.yudao.module.facebook.service.aisearch;

import cn.iocoder.yudao.module.facebook.controller.admin.aisearch.vo.*;

import java.util.List;

public interface AiSearchTaskService {
    Long create(AiSearchTaskSaveReqVO req);

    void update(AiSearchTaskSaveReqVO req);

    void delete(Long id);

    AiSearchTaskRespVO get(Long id);

    List<AiSearchTaskRespVO> list();

    void start(Long id);

    void updateStatus(Long id, String status);

    int dispatchScheduled();

    AiSearchExpansionRespVO expand(AiSearchExpansionReqVO req);
}
