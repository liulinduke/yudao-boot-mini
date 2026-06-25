package cn.iocoder.yudao.module.facebook.service.agent;

import cn.iocoder.yudao.framework.common.pojo.PageResult;
import cn.iocoder.yudao.module.facebook.controller.admin.agent.vo.*;
import cn.iocoder.yudao.module.facebook.dal.dataobject.agent.FbAiAgentConfigDO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.agent.FbAiTouchRecordDO;

import jakarta.validation.Valid;

public interface FbAiAgentService {

    Long saveConfig(@Valid FbAiAgentConfigSaveReqVO saveReqVO);

    FbAiAgentConfigDO getConfig();

    FbAiAgentConfigDO getEnabledConfig();

    PageResult<FbAiTouchRecordDO> getTouchRecordPage(FbAiTouchRecordPageReqVO pageReqVO);

    Long createTouchRecord(FbAiTouchRecordDO touchRecord);

    void updateTouchRecordResult(Long id, Integer status, String failReason);

    void saveLeadAnalysis(@Valid FbAiLeadAnalysisSaveReqVO saveReqVO);

    FbAiAgentDispatchRespVO dispatchOnce();

}
