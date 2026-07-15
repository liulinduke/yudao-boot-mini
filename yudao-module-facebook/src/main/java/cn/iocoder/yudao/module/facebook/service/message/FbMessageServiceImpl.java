package cn.iocoder.yudao.module.facebook.service.message;

import cn.hutool.json.JSONUtil;
import cn.iocoder.yudao.framework.common.pojo.PageResult;
import cn.iocoder.yudao.module.ai.dal.dataobject.workflow.AiWorkflowDO;
import cn.iocoder.yudao.module.ai.dal.mysql.workflow.AiWorkflowMapper;
import cn.iocoder.yudao.module.ai.service.workflow.AiWorkflowService;
import cn.iocoder.yudao.module.facebook.controller.admin.dmtask.vo.FbDmTaskSaveReqVO;
import cn.iocoder.yudao.module.facebook.controller.admin.message.vo.*;
import cn.iocoder.yudao.module.facebook.dal.dataobject.account.FbAccountDO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.dmtask.FbDmTaskDetailDO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.message.*;
import cn.iocoder.yudao.module.facebook.dal.mysql.account.FbAccountMapper;
import cn.iocoder.yudao.module.facebook.dal.mysql.message.*;
import cn.iocoder.yudao.module.facebook.dal.mysql.dmtask.FbDmTaskDetailMapper;
import cn.iocoder.yudao.module.facebook.service.dmtask.FbDmTaskService;
import cn.iocoder.yudao.module.facebook.service.agent.FbAiAgentCollectQueueService;
import com.baomidou.mybatisplus.core.conditions.query.LambdaQueryWrapper;
import jakarta.annotation.Resource;
import lombok.extern.slf4j.Slf4j;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;
import org.springframework.validation.annotation.Validated;

import java.time.LocalDateTime;
import java.util.*;
import java.util.stream.Collectors;

@Slf4j
@Service
@Validated
public class FbMessageServiceImpl implements FbMessageService {

    private static final String TRANSLATE_WORKFLOW = "fb_message_translate_v1";

    @Resource private FbMessageMonitorAccountMapper monitorMapper;
    @Resource private FbMessageConversationMapper conversationMapper;
    @Resource private FbMessageMapper messageMapper;
    @Resource private FbAccountMapper accountMapper;
    @Resource private FbDmTaskService dmTaskService;
    @Resource private AiWorkflowMapper aiWorkflowMapper;
    @Resource private AiWorkflowService aiWorkflowService;
    @Resource private FbAiAgentCollectQueueService accountQueueService;
    @Resource private FbDmTaskDetailMapper dmTaskDetailMapper;

    @Override
    public List<FbMessageMonitorAccountDO> getMonitorAccounts() {
        return monitorMapper.selectList(new LambdaQueryWrapper<FbMessageMonitorAccountDO>()
                .orderByAsc(FbMessageMonitorAccountDO::getAccountId));
    }

    @Override
    @Transactional(rollbackFor = Exception.class)
    public Long saveMonitorAccount(FbMessageMonitorAccountSaveReqVO reqVO) {
        FbMessageMonitorAccountDO obj = reqVO.getId() == null
                ? monitorMapper.selectByAccountId(reqVO.getAccountId()) : monitorMapper.selectById(reqVO.getId());
        if (obj == null) {
            obj = new FbMessageMonitorAccountDO();
        }
        String previousMode = obj.getMode();
        String requestedMode = normalizeMode(reqVO.getMode());
        obj.setAccountId(reqVO.getAccountId());
        obj.setMode(requestedMode);
        obj.setCheckIntervalMinutes(Math.max(1, reqVO.getCheckIntervalMinutes() == null ? 30 : reqVO.getCheckIntervalMinutes()));
        obj.setStatus(reqVO.getStatus() == null ? 1 : reqVO.getStatus());
        if (obj.getNextCheckTime() == null || "disabled".equals(obj.getMode()) || !Objects.equals(previousMode, requestedMode)) {
            obj.setNextCheckTime("disabled".equals(obj.getMode()) ? null : LocalDateTime.now());
        }
        if ("disabled".equals(obj.getMode())) {
            FbAccountDO account = accountMapper.selectById(obj.getAccountId());
            if (account != null) accountQueueService.releaseRunning(account.getFbAccount());
        }
        if (obj.getId() == null) monitorMapper.insert(obj); else monitorMapper.updateById(obj);
        return obj.getId();
    }

    @Override
    @Transactional(rollbackFor = Exception.class)
    public void batchSaveMonitorAccounts(List<FbMessageMonitorAccountSaveReqVO> reqVOList) {
        if (reqVOList == null) return;
        for (FbMessageMonitorAccountSaveReqVO reqVO : reqVOList) saveMonitorAccount(reqVO);
    }

    @Override
    public boolean refreshMonitor(Long monitorId) {
        FbMessageMonitorAccountDO row = monitorMapper.selectById(monitorId);
        if (row == null || !"realtime".equals(row.getMode()) || !Objects.equals(row.getStatus(), 1)) return false;
        FbAccountDO account = accountMapper.selectById(row.getAccountId());
        return account != null && accountQueueService.refreshAccountClaim(account.getFbAccount(), "message:" + row.getId(), 90);
    }

    @Override
    @Transactional(rollbackFor = Exception.class)
    public List<FbMessageMonitorClaimRespVO> claimMonitorAccounts(FbMessageMonitorClaimReqVO reqVO) {
        int limit = Math.max(1, Math.min(reqVO.getLimit() == null ? 3 : reqVO.getLimit(), 50));
        Set<String> excluded = Optional.ofNullable(reqVO.getExcludeAccounts()).orElse(List.of())
                .stream().filter(Objects::nonNull).collect(Collectors.toSet());
        List<FbMessageMonitorAccountDO> rows = monitorMapper.selectList(new LambdaQueryWrapper<FbMessageMonitorAccountDO>()
                .in(FbMessageMonitorAccountDO::getMode, "realtime", "scheduled")
                .eq(FbMessageMonitorAccountDO::getStatus, 1)
                // 实时在线账号每次消息窗口启动都应立即领取；只有定时检查账号受 nextCheckTime 限制。
                .and(w -> w.eq(FbMessageMonitorAccountDO::getMode, "realtime")
                        .or(q -> q.eq(FbMessageMonitorAccountDO::getMode, "scheduled")
                                .and(x -> x.isNull(FbMessageMonitorAccountDO::getNextCheckTime)
                                        .or().le(FbMessageMonitorAccountDO::getNextCheckTime, LocalDateTime.now()))))
                .orderByAsc(FbMessageMonitorAccountDO::getMode)
                .orderByAsc(FbMessageMonitorAccountDO::getNextCheckTime)
                .last("LIMIT " + limit));
        if (rows.isEmpty()) return List.of();
        Map<Long, FbAccountDO> accounts = accountMapper.selectBatchIds(rows.stream().map(FbMessageMonitorAccountDO::getAccountId).collect(Collectors.toList()))
                .stream().collect(Collectors.toMap(FbAccountDO::getId, a -> a, (a, b) -> a));
        List<FbMessageMonitorClaimRespVO> result = new ArrayList<>();
        for (FbMessageMonitorAccountDO row : rows) {
            FbAccountDO account = accounts.get(row.getAccountId());
            if (account == null || excluded.contains(String.valueOf(account.getFbAccount()))) continue;
            if (!accountQueueService.tryClaimAccount(account.getFbAccount(), "message:" + row.getId(), "realtime".equals(row.getMode()) ? 90 : 3)) continue;
            row.setLastCheckTime(LocalDateTime.now());
            row.setNextCheckTime(LocalDateTime.now().plusMinutes(Math.max(1, row.getCheckIntervalMinutes())));
            monitorMapper.updateById(row);
            FbMessageMonitorClaimRespVO item = new FbMessageMonitorClaimRespVO();
            item.setMonitorId(row.getId()); item.setAccountId(row.getAccountId());
            item.setFbAccount(account.getFbAccount()); item.setCookie(account.getCookie());
            item.setDeviceId(account.getDeviceId());
            item.setMode(row.getMode()); item.setCheckIntervalMinutes(row.getCheckIntervalMinutes());
            // 消息监控只读取 Facebook 当前界面上的 Messenger/通知红色未读角标，不再切换页面。
            item.setUrl("https://www.facebook.com/");
            result.add(item);
        }
        return result;
    }

    @Override
    @Transactional(rollbackFor = Exception.class)
    public void reportMonitor(Long monitorId, boolean success, String errorMessage) {
        FbMessageMonitorAccountDO row = monitorMapper.selectById(monitorId);
        if (row == null) return;
        row.setStatus(1);
        row.setErrorMessage(success ? null : errorMessage);
        if (success) row.setLastSuccessTime(LocalDateTime.now());
        monitorMapper.updateById(row);
        FbAccountDO account = accountMapper.selectById(row.getAccountId());
        if (account != null) accountQueueService.releaseRunning(account.getFbAccount());
    }

    @Override
    @Transactional(rollbackFor = Exception.class)
    public void reportUnreadBadges(FbMessageMonitorBadgeReportReqVO reqVO) {
        FbMessageMonitorAccountDO row = monitorMapper.selectByAccountId(reqVO.getAccountId());
        if (row == null) return;
        row.setMessengerUnreadCount(Math.max(0, Optional.ofNullable(reqVO.getMessengerUnreadCount()).orElse(0)));
        row.setNotificationUnreadCount(Math.max(0, Optional.ofNullable(reqVO.getNotificationUnreadCount()).orElse(0)));
        row.setLastBadgeCheckTime(LocalDateTime.now());
        if (Boolean.FALSE.equals(reqVO.getLoggedIn())) row.setErrorMessage("Cookie失效");
        monitorMapper.updateById(row);
    }

    @Override
    @Transactional(rollbackFor = Exception.class)
    public Long ingest(FbMessageIngestReqVO reqVO) {
        String sourceType = blankDefault(reqVO.getSourceType(), "messenger");
        if (reqVO.getAccountId() == null || blankDefault(reqVO.getOriginalText(), "").isBlank()) return null;
        if (reqVO.getExternalMessageId() != null && messageMapper.selectByExternalKey(reqVO.getAccountId(), sourceType, reqVO.getExternalMessageId()) != null) {
            return messageMapper.selectByExternalKey(reqVO.getAccountId(), sourceType, reqVO.getExternalMessageId()).getId();
        }
        String conversationKey = blankDefault(reqVO.getConversationKey(), blankDefault(reqVO.getTargetUserId(), "unknown"));
        FbMessageConversationDO conversation = conversationMapper.selectByKey(reqVO.getAccountId(), conversationKey);
        if (conversation == null) {
            conversation = new FbMessageConversationDO();
            conversation.setAccountId(reqVO.getAccountId()); conversation.setConversationKey(conversationKey);
            conversation.setUnreadCount(0); conversation.setStatus(1); conversation.setSourceType(sourceType);
        }
        conversation.setTargetUserId(reqVO.getTargetUserId()); conversation.setTargetName(reqVO.getTargetName());
        conversation.setTargetUrl(reqVO.getTargetUrl()); conversation.setLastMessagePreview(reqVO.getOriginalText());
        LocalDateTime messageTime = parseTime(reqVO.getMessageTime());
        conversation.setLastMessageTime(messageTime);
        conversation.setDetectedLanguage(reqVO.getDetectedLanguage());
        conversation.setReplyTargetLanguage(reqVO.getDetectedLanguage());
        boolean inbound = !"outbound".equalsIgnoreCase(reqVO.getDirection());
        conversation.setUnreadCount(Math.max(0, Optional.ofNullable(conversation.getUnreadCount()).orElse(0) + (inbound ? 1 : 0)));
        if (conversation.getId() == null) conversationMapper.insert(conversation); else conversationMapper.updateById(conversation);
        FbMessageDO message = new FbMessageDO();
        message.setConversationId(conversation.getId()); message.setAccountId(reqVO.getAccountId());
        message.setExternalMessageId(reqVO.getExternalMessageId()); message.setDirection(inbound ? "inbound" : "outbound");
        message.setSourceType(sourceType); message.setSenderUserId(reqVO.getSenderUserId()); message.setSenderName(reqVO.getSenderName());
        message.setOriginalText(reqVO.getOriginalText()); message.setDetectedLanguage(reqVO.getDetectedLanguage());
        message.setTranslatedText(reqVO.getTranslatedText());
        message.setTranslationStatus(reqVO.getTranslatedText() == null ? 0 : 1);
        message.setSourcePostId(reqVO.getSourcePostId()); message.setSourcePostUrl(reqVO.getSourcePostUrl());
        message.setSourceCommentId(reqVO.getSourceCommentId()); message.setMessageTime(messageTime);
        message.setIsRead(!inbound); message.setSendStatus(inbound ? 2 : 0);
        messageMapper.insert(message);
        return message.getId();
    }

    @Override
    public PageResult<FbMessageConversationDO> getConversationPage(FbMessageConversationPageReqVO reqVO) {
        LambdaQueryWrapper<FbMessageConversationDO> w = new LambdaQueryWrapper<FbMessageConversationDO>()
                .eq(reqVO.getAccountId() != null, FbMessageConversationDO::getAccountId, reqVO.getAccountId())
                .eq(reqVO.getSourceType() != null, FbMessageConversationDO::getSourceType, reqVO.getSourceType())
                .and(reqVO.getKeyword() != null && !reqVO.getKeyword().isBlank(), x -> x.like(FbMessageConversationDO::getTargetName, reqVO.getKeyword()).or().like(FbMessageConversationDO::getTargetUserId, reqVO.getKeyword()))
                .orderByDesc(FbMessageConversationDO::getLastMessageTime);
        List<FbMessageConversationDO> all = conversationMapper.selectList(w);
        return page(all, reqVO.getPageNo(), reqVO.getPageSize());
    }

    @Override public List<FbMessageDO> getConversationMessages(Long conversationId) { return messageMapper.selectByConversationId(conversationId); }

    @Override public FbMessageConversationDO getConversation(Long conversationId) { return conversationMapper.selectById(conversationId); }

    @Override
    @Transactional(rollbackFor = Exception.class)
    public void markConversationRead(Long conversationId) {
        FbMessageConversationDO c = conversationMapper.selectById(conversationId);
        if (c == null) return;
        c.setUnreadCount(0); c.setLastReadTime(LocalDateTime.now()); conversationMapper.updateById(c);
        messageMapper.update(null, new com.baomidou.mybatisplus.core.conditions.update.LambdaUpdateWrapper<FbMessageDO>()
                .eq(FbMessageDO::getConversationId, conversationId).set(FbMessageDO::getIsRead, true));
    }

    @Override
    public List<Map<String, Object>> getUnreadSummary() {
        List<FbMessageMonitorAccountDO> monitors = monitorMapper.selectList(new LambdaQueryWrapper<FbMessageMonitorAccountDO>()
                .in(FbMessageMonitorAccountDO::getMode, "realtime", "scheduled")
                .eq(FbMessageMonitorAccountDO::getStatus, 1));
        List<Map<String, Object>> result = new ArrayList<>();
        for (FbMessageMonitorAccountDO monitor : monitors) {
            int messenger = Optional.ofNullable(monitor.getMessengerUnreadCount()).orElse(0);
            int notifications = Optional.ofNullable(monitor.getNotificationUnreadCount()).orElse(0);
            result.add(new LinkedHashMap<>(Map.of("accountId", monitor.getAccountId(),
                    "messengerUnreadCount", messenger, "commentUnreadCount", notifications,
                    "totalUnreadCount", messenger + notifications)));
        }
        return result;
    }

    @Override
    @Transactional(rollbackFor = Exception.class)
    public List<FbMessageDO> translateUnread(Long conversationId) {
        List<FbMessageDO> messages = messageMapper.selectByConversationId(conversationId);
        int translated = 0;
        for (FbMessageDO message : messages) {
            if (!"inbound".equalsIgnoreCase(message.getDirection()) || translated >= 50
                    || (message.getTranslatedText() != null && !message.getTranslatedText().isBlank())) continue;
            FbMessageTranslateReqVO request = new FbMessageTranslateReqVO();
            request.setText(message.getOriginalText());
            request.setSourceLanguage(message.getDetectedLanguage());
            request.setTargetLanguage("zh");
            request.setContext("facebook_messenger");
            Map<String, Object> result = translate(request);
            Object detectedLanguage = result.get("detectedLanguage");
            Object translation = result.get("translation");
            if (detectedLanguage != null) message.setDetectedLanguage(String.valueOf(detectedLanguage));
            message.setTranslatedText(translation == null ? "" : String.valueOf(translation));
            message.setTranslationStatus(message.getTranslatedText().isBlank() ? 2 : 1);
            messageMapper.updateById(message);
            translated++;
        }
        return messageMapper.selectByConversationId(conversationId);
    }

    @Override
    public PageResult<FbMessageDO> getMessagePage(FbMessagePageReqVO reqVO) {
        List<FbMessageDO> all = messageMapper.selectList(new LambdaQueryWrapper<FbMessageDO>()
                .eq(reqVO.getConversationId() != null, FbMessageDO::getConversationId, reqVO.getConversationId())
                .eq(Boolean.TRUE.equals(reqVO.getUnreadOnly()), FbMessageDO::getIsRead, false)
                .orderByDesc(FbMessageDO::getMessageTime));
        return page(all, reqVO.getPageNo(), reqVO.getPageSize());
    }

    @Override
    public Map<String, Object> translate(FbMessageTranslateReqVO reqVO) {
        Map<String, Object> params = new LinkedHashMap<>(); params.put("text", reqVO.getText());
        params.put("sourceLanguage", blankDefault(reqVO.getSourceLanguage(), "auto")); params.put("targetLanguage", reqVO.getTargetLanguage());
        params.put("context", blankDefault(reqVO.getContext(), "facebook_messenger"));
        AiWorkflowDO workflow = aiWorkflowMapper.selectByCode(TRANSLATE_WORKFLOW);
        if (workflow == null) throw new IllegalStateException("翻译工作流不存在：" + TRANSLATE_WORKFLOW);
        Object raw = aiWorkflowService.executeWorkflow(workflow.getId(), params);
        if (raw instanceof Map) return new LinkedHashMap<>((Map<String, Object>) raw);
        if (raw instanceof CharSequence && JSONUtil.isTypeJSON(raw.toString())) return JSONUtil.toBean(raw.toString(), Map.class);
        return Map.of("translation", String.valueOf(raw));
    }

    @Override
    @Transactional(rollbackFor = Exception.class)
    public Long send(FbMessageSendReqVO reqVO) {
        FbAccountDO account = accountMapper.selectById(reqVO.getAccountId());
        if (account == null) throw new IllegalArgumentException("账号不存在");
        FbDmTaskSaveReqVO dm = new FbDmTaskSaveReqVO();
        dm.setTargetUserIds(List.of(reqVO.getTargetUserId())); dm.setScripts(List.of(reqVO.getText())); dm.setScriptType(1);
        dm.setAccountIds(List.of(String.valueOf(reqVO.getAccountId()))); dm.setMinIntervalSeconds(0); dm.setMaxIntervalSeconds(0); dm.setRemark("消息管理手动发送");
        Long taskId = dmTaskService.createDmTask(dm);
        FbMessageIngestReqVO out = new FbMessageIngestReqVO(); out.setAccountId(reqVO.getAccountId()); out.setConversationKey(blankDefault(reqVO.getConversationKey(), reqVO.getTargetUserId()));
        out.setTargetUserId(reqVO.getTargetUserId()); out.setTargetName(reqVO.getTargetName()); out.setTargetUrl(reqVO.getTargetUrl()); out.setOriginalText(reqVO.getText());
        out.setDirection("outbound"); out.setSourceType("messenger"); out.setDetectedLanguage(reqVO.getTargetLanguage());
        Long messageId = ingest(out);
        FbDmTaskDetailDO detail = dmTaskDetailMapper.selectListByTaskId(taskId).stream().findFirst().orElse(null);
        if (detail != null && messageId != null) {
            FbMessageDO update = new FbMessageDO(); update.setId(messageId); update.setSendTaskId(taskId); update.setSendDetailId(detail.getId());
            messageMapper.updateById(update);
        }
        return taskId;
    }

    private String normalizeMode(String mode) { return Set.of("realtime", "scheduled", "disabled").contains(mode) ? mode : "disabled"; }
    private String blankDefault(String value, String fallback) { return value == null || value.isBlank() ? fallback : value; }
    private LocalDateTime parseTime(String value) {
        if (value == null || value.isBlank()) return LocalDateTime.now();
        try { return LocalDateTime.parse(value.replace("Z", "")); } catch (Exception ignored) { return LocalDateTime.now(); }
    }
    private <T> PageResult<T> page(List<T> all, Integer pageNo, Integer pageSize) {
        int size = Math.max(1, pageSize == null ? 10 : pageSize); int page = Math.max(1, pageNo == null ? 1 : pageNo);
        int from = Math.min((page - 1) * size, all.size()); int to = Math.min(from + size, all.size());
        return new PageResult<>(all.subList(from, to), (long) all.size());
    }
}
