package cn.iocoder.yudao.module.facebook.service.agent;

import cn.iocoder.yudao.framework.common.pojo.PageResult;
import cn.iocoder.yudao.module.facebook.controller.admin.agent.vo.*;
import cn.iocoder.yudao.module.facebook.dal.dataobject.agent.FbAiAgentDiscoveryLogDO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.agent.FbAiAgentRunLogDO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.agent.FbAiAgentConfigDO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.agent.FbAiTouchRecordDO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.collectuser.FbCollectUserDO;

import jakarta.validation.Valid;

import java.util.List;

public interface FbAiAgentService {

    Long saveConfig(@Valid FbAiAgentConfigSaveReqVO saveReqVO);

    PageResult<FbAiAgentConfigDO> getConfigPage(FbAiAgentConfigPageReqVO pageReqVO);

    FbAiAgentConfigDO getConfig(Long id);

    FbAiAgentConfigDO getConfig();

    FbAiAgentConfigDO getEnabledConfig();

    void updateStatus(@Valid FbAiAgentStatusUpdateReqVO reqVO);

    void deleteConfig(Long id);

    FbAiKeywordGenerateRespVO generateKeywords(@Valid FbAiKeywordGenerateReqVO reqVO);

    PageResult<FbAiAgentDiscoveryLogDO> getDiscoveryLogPage(FbAiAgentDiscoveryLogPageReqVO pageReqVO);

    PageResult<FbAiAgentRunLogDO> getRunLogPage(FbAiAgentRunLogPageReqVO pageReqVO);

    PageResult<FbCollectUserDO> getLeadPage(FbAiAgentLeadPageReqVO pageReqVO);

    PageResult<FbAiTouchRecordDO> getTouchRecordPage(FbAiTouchRecordPageReqVO pageReqVO);

    Long createTouchRecord(FbAiTouchRecordDO touchRecord);

    void updateTouchRecordResult(Long id, Integer status, String failReason);

    void saveLeadAnalysis(@Valid FbAiLeadAnalysisSaveReqVO saveReqVO);

    FbAiAgentDispatchRespVO dispatchOnce();

    FbAiAgentDispatchRespVO executeNow(List<Long> ids);

    FbAiAgentDispatchRespVO dispatchScheduled();

    void continueAfterCollectTaskFinished(Long collectTaskId);

    /**
     * 根据已落库的采集结果刷新对应 AI 发现记录的统计值。
     */
    void refreshDiscoveryStatsByCollectTaskId(Long collectTaskId);

}
