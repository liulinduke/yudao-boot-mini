package cn.iocoder.yudao.module.facebook.service.message;

import cn.iocoder.yudao.framework.common.pojo.PageResult;
import cn.iocoder.yudao.module.facebook.controller.admin.message.vo.*;
import cn.iocoder.yudao.module.facebook.dal.dataobject.message.*;
import jakarta.validation.Valid;

import java.util.List;
import java.util.Map;

public interface FbMessageService {
    List<FbMessageMonitorAccountDO> getMonitorAccounts();
    List<FbMessageMonitorAccountDO> getMonitorPool();
    List<Map<String, String>> getMonitorCandidates();
    Long saveMonitorAccount(@Valid FbMessageMonitorAccountSaveReqVO reqVO);
    void batchSaveMonitorAccounts(List<FbMessageMonitorAccountSaveReqVO> reqVOList);
    void addMonitorPool(@Valid FbMessageMonitorPoolReqVO reqVO);
    void removeMonitorPool(@Valid FbMessageMonitorPoolReqVO reqVO);
    void batchUpdateMonitorState(@Valid FbMessageMonitorBatchStateReqVO reqVO);
    void updateMonitorIntervals(@Valid FbMessageMonitorIntervalReqVO reqVO);
    void normalizeMonitorRuntimeStates();
    List<FbMessageMonitorClaimRespVO> claimMonitorAccounts(FbMessageMonitorClaimReqVO reqVO);
    boolean refreshMonitor(Long monitorId);
    void reportMonitor(Long monitorId, boolean success, String errorMessage);
    void reportUnreadBadges(@Valid FbMessageMonitorBadgeReportReqVO reqVO);
    Long ingest(@Valid FbMessageIngestReqVO reqVO);
    PageResult<FbMessageConversationDO> getConversationPage(FbMessageConversationPageReqVO reqVO);
    FbMessageConversationDO getConversation(Long conversationId);
    List<FbMessageDO> getConversationMessages(Long conversationId);
    void markConversationRead(Long conversationId);
    List<Map<String, Object>> getUnreadSummary();
    List<FbMessageDO> translateUnread(Long conversationId);
    PageResult<FbMessageDO> getMessagePage(FbMessagePageReqVO reqVO);
    Map<String, Object> translate(@Valid FbMessageTranslateReqVO reqVO);
    Long send(@Valid FbMessageSendReqVO reqVO);
}
