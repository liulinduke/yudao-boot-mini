package cn.iocoder.yudao.module.facebook.service.agent;

import cn.hutool.core.collection.CollUtil;
import cn.hutool.core.util.StrUtil;
import cn.hutool.json.JSONObject;
import cn.hutool.json.JSONUtil;
import cn.iocoder.yudao.framework.common.pojo.PageResult;
import cn.iocoder.yudao.framework.common.util.object.BeanUtils;
import cn.iocoder.yudao.module.facebook.controller.admin.agent.vo.*;
import cn.iocoder.yudao.module.facebook.controller.admin.dmtask.vo.FbDmTaskSaveReqVO;
import cn.iocoder.yudao.module.facebook.controller.admin.operation.vo.FbOperationTaskSaveReqVO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.account.FbAccountDO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.agent.FbAiAgentConfigDO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.agent.FbAiTouchRecordDO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.collect.FbCollectDO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.collectdetail.FbCollectDetailDO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.collectuser.FbCollectUserDO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.fbcollectpost.FbCollectPostDO;
import cn.iocoder.yudao.module.facebook.dal.mysql.account.FbAccountMapper;
import cn.iocoder.yudao.module.facebook.dal.mysql.agent.FbAiAgentConfigMapper;
import cn.iocoder.yudao.module.facebook.dal.mysql.agent.FbAiTouchRecordMapper;
import cn.iocoder.yudao.module.facebook.dal.mysql.collect.FbCollectMapper;
import cn.iocoder.yudao.module.facebook.dal.mysql.collectdetail.FbCollectDetailMapper;
import cn.iocoder.yudao.module.facebook.dal.mysql.collectuser.FbCollectUserMapper;
import cn.iocoder.yudao.module.facebook.dal.mysql.fbcollectpost.FbCollectPostMapper;
import cn.iocoder.yudao.module.facebook.service.dmtask.FbDmTaskService;
import cn.iocoder.yudao.module.facebook.service.operation.FbOperationTaskService;
import com.baomidou.mybatisplus.core.conditions.query.LambdaQueryWrapper;
import jakarta.annotation.Resource;
import lombok.extern.slf4j.Slf4j;
import org.springframework.context.ApplicationContext;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;
import org.springframework.validation.annotation.Validated;

import java.lang.reflect.Method;
import java.net.URLEncoder;
import java.nio.charset.StandardCharsets;
import java.time.LocalDateTime;
import java.time.LocalTime;
import java.util.*;
import java.util.concurrent.ThreadLocalRandom;
import java.util.stream.Collectors;

import static cn.iocoder.yudao.framework.common.exception.util.ServiceExceptionUtil.exception0;

/**
 * Facebook AI获客Agent Service 实现类
 */
@Slf4j
@Service
@Validated
public class FbAiAgentServiceImpl implements FbAiAgentService {

    private static final String AUTO_COLLECT_REMARK = "AI_AGENT_AUTO_COLLECT";
    private static final String AUTO_GROUP_COLLECT_REMARK = "AI_AGENT_GROUP_MONITOR";
    private static final int POST_COLLECT_TASK_TYPE = 2;
    private static final int DEFAULT_COLLECT_EXPECTED_COUNT = 20;
    private static final int MAX_ANALYZE_PER_RUN = 30;
    private static final int MAX_TOUCH_QUEUE_PER_RUN = 20;
    private static final int POST_COMMENT_TASK_TYPE = 15;

    @Resource
    private FbAiAgentConfigMapper agentConfigMapper;
    @Resource
    private FbAiTouchRecordMapper touchRecordMapper;
    @Resource
    private FbCollectUserMapper collectUserMapper;
    @Resource
    private FbCollectPostMapper collectPostMapper;
    @Resource
    private FbCollectMapper collectMapper;
    @Resource
    private FbCollectDetailMapper collectDetailMapper;
    @Resource
    private FbAccountMapper accountMapper;
    @Resource
    private FbOperationTaskService operationTaskService;
    @Resource
    private FbDmTaskService dmTaskService;
    @Resource
    private ApplicationContext applicationContext;

    @Override
    @Transactional(rollbackFor = Exception.class)
    public Long saveConfig(FbAiAgentConfigSaveReqVO saveReqVO) {
        normalizeDefaults(saveReqVO);
        validateConfig(saveReqVO);

        FbAiAgentConfigDO existing = getConfig();
        FbAiAgentConfigDO config = BeanUtils.toBean(saveReqVO, FbAiAgentConfigDO.class);
        if (existing == null) {
            agentConfigMapper.insert(config);
            return config.getId();
        }
        config.setId(existing.getId());
        agentConfigMapper.updateById(config);
        return existing.getId();
    }

    @Override
    public FbAiAgentConfigDO getConfig() {
        return agentConfigMapper.selectOne(FbAiAgentConfigDO::getDeleted, false);
    }

    @Override
    public FbAiAgentConfigDO getEnabledConfig() {
        return agentConfigMapper.selectOne(FbAiAgentConfigDO::getStatus, 1);
    }

    @Override
    public PageResult<FbAiTouchRecordDO> getTouchRecordPage(FbAiTouchRecordPageReqVO pageReqVO) {
        return touchRecordMapper.selectPage(pageReqVO);
    }

    @Override
    public Long createTouchRecord(FbAiTouchRecordDO touchRecord) {
        if (touchRecord.getStatus() == null) {
            touchRecord.setStatus(0);
        }
        touchRecordMapper.insert(touchRecord);
        return touchRecord.getId();
    }

    @Override
    public void updateTouchRecordResult(Long id, Integer status, String failReason) {
        FbAiTouchRecordDO updateObj = new FbAiTouchRecordDO();
        updateObj.setId(id);
        updateObj.setStatus(status);
        updateObj.setFailReason(failReason);
        if (Integer.valueOf(2).equals(status)) {
            updateObj.setSentTime(LocalDateTime.now());
        }
        touchRecordMapper.updateById(updateObj);
    }

    @Override
    public void saveLeadAnalysis(FbAiLeadAnalysisSaveReqVO saveReqVO) {
        LocalDateTime now = LocalDateTime.now();
        if ("user".equals(saveReqVO.getLeadType())) {
            FbCollectUserDO updateObj = new FbCollectUserDO();
            updateObj.setId(saveReqVO.getLeadId());
            updateObj.setAiTags(saveReqVO.getAiTags());
            updateObj.setIntentLevel(saveReqVO.getIntentLevel());
            updateObj.setIntentReason(saveReqVO.getIntentReason());
            updateObj.setSentiment(saveReqVO.getSentiment());
            updateObj.setLeadType(saveReqVO.getLeadCategory());
            updateObj.setCountry(saveReqVO.getCountry());
            updateObj.setProductRelevanceScore(saveReqVO.getProductRelevanceScore());
            updateObj.setAiSummary(saveReqVO.getAiSummary());
            updateObj.setTouchStatus(StrUtil.blankToDefault(saveReqVO.getTouchStatus(), "not_touched"));
            updateObj.setLastAiAnalyzeTime(now);
            collectUserMapper.updateById(updateObj);
            return;
        }
        if ("post".equals(saveReqVO.getLeadType())) {
            FbCollectPostDO updateObj = new FbCollectPostDO();
            updateObj.setId(saveReqVO.getLeadId());
            updateObj.setAiTags(saveReqVO.getAiTags());
            updateObj.setIntentLevel(saveReqVO.getIntentLevel());
            updateObj.setIntentReason(saveReqVO.getIntentReason());
            updateObj.setSentiment(saveReqVO.getSentiment());
            updateObj.setLeadType(saveReqVO.getLeadCategory());
            updateObj.setCountry(saveReqVO.getCountry());
            updateObj.setLanguage(saveReqVO.getLanguage());
            updateObj.setProductRelevanceScore(saveReqVO.getProductRelevanceScore());
            updateObj.setAiSummary(saveReqVO.getAiSummary());
            updateObj.setTouchStatus(StrUtil.blankToDefault(saveReqVO.getTouchStatus(), "not_touched"));
            updateObj.setLastAiAnalyzeTime(now);
            collectPostMapper.updateById(updateObj);
            return;
        }
        throw exception0(2_011_000_005, "不支持的线索类型：{}", saveReqVO.getLeadType());
    }

    @Override
    @Transactional(rollbackFor = Exception.class)
    public FbAiAgentDispatchRespVO dispatchOnce() {
        FbAiAgentConfigDO config = getEnabledConfig();
        if (config == null) {
            return new FbAiAgentDispatchRespVO(false, "AI获客Agent未启用");
        }
        AgentDispatchStats stats = new AgentDispatchStats();
        List<String> seedKeywords = parseJsonStringList(config.getSeedKeywords());
        List<String> accountIds = parseCsvStringList(config.getAccountIds());
        List<String> monitorGroupIds = parseCsvStringList(config.getMonitorGroupIds());
        List<String> targetCountries = parseJsonStringList(config.getTargetCountries());
        List<String> targetLanguages = parseJsonStringList(config.getTargetLanguages());

        stats.createdCollectTasks = createKeywordCollectTasks(config, seedKeywords, accountIds);
        stats.createdCollectTasks += createMonitorGroupCollectTasks(config, monitorGroupIds, accountIds);
        stats.analyzedPosts = analyzePendingPosts(config, seedKeywords, targetCountries, targetLanguages);
        stats.analyzedUsers = analyzePendingUsers(config, seedKeywords, targetCountries, targetLanguages);
        stats.queuedTouches = queueHighIntentTouches(config, accountIds);
        stats.activatedTouches = activateDueTouchRecords(config);

        String message = String.format("Agent调度完成：新建采集任务%s个，分析帖子%s条，分析潜客%s条，排队触达%s条，转执行任务%s条",
                stats.createdCollectTasks, stats.analyzedPosts, stats.analyzedUsers, stats.queuedTouches, stats.activatedTouches);
        log.info("[dispatchOnce][configId={}, {}]", config.getId(), message);
        return new FbAiAgentDispatchRespVO(true, message);
    }

    private int createKeywordCollectTasks(FbAiAgentConfigDO config, List<String> seedKeywords, List<String> accountIds) {
        if (CollUtil.isEmpty(seedKeywords) || CollUtil.isEmpty(accountIds)) {
            return 0;
        }
        List<Long> accountIdLongs = accountIds.stream()
                .map(this::parseLongOrNull)
                .filter(Objects::nonNull)
                .distinct()
                .collect(Collectors.toList());
        if (CollUtil.isEmpty(accountIdLongs)) {
            return 0;
        }
        List<FbAccountDO> accounts = accountMapper.selectBatchIds(accountIdLongs);
        if (CollUtil.isEmpty(accounts)) {
            return 0;
        }
        Map<Long, String> accountMap = accounts.stream()
                .collect(Collectors.toMap(FbAccountDO::getId, FbAccountDO::getFbAccount, (a, b) -> a));

        int created = 0;
        for (String keyword : seedKeywords) {
            String normalizedKeyword = StrUtil.trim(keyword);
            if (StrUtil.isBlank(normalizedKeyword)) {
                continue;
            }
            String searchUrl = buildSearchTopUrl(normalizedKeyword);
            if (existsTodayAutoCollect(searchUrl)) {
                continue;
            }

            FbCollectDO task = new FbCollectDO();
            task.setTaskType(POST_COLLECT_TASK_TYPE);
            task.setSearchType(1);
            task.setSearchUrl(searchUrl);
            task.setExpectedCount(DEFAULT_COLLECT_EXPECTED_COUNT);
            task.setIntervalSeconds(5);
            task.setStatus(1);
            task.setStartTime(LocalDateTime.now());
            task.setRemark(AUTO_COLLECT_REMARK + ":" + normalizedKeyword);
            task.setTotalExpectedCount(accountIdLongs.size() * DEFAULT_COLLECT_EXPECTED_COUNT);
            task.setTotalCollectedCount(0);
            task.setAccountCount(accountIdLongs.size());
            task.setUrlCount(1);
            task.setFbAccount(accountMap.get(accountIdLongs.get(0)));
            collectMapper.insert(task);

            for (Long accountId : accountIdLongs) {
                FbCollectDetailDO detail = new FbCollectDetailDO();
                detail.setTaskId(task.getId());
                detail.setFbAccount(StrUtil.blankToDefault(accountMap.get(accountId), "account_" + accountId));
                detail.setSearchUrl(searchUrl);
                detail.setExpectedCount(DEFAULT_COLLECT_EXPECTED_COUNT);
                detail.setCollectedCount(0);
                detail.setStatus(0);
                collectDetailMapper.insert(detail);
            }
            created++;
        }
        return created;
    }

    private int createMonitorGroupCollectTasks(FbAiAgentConfigDO config, List<String> monitorGroupIds, List<String> accountIds) {
        if (CollUtil.isEmpty(monitorGroupIds) || CollUtil.isEmpty(accountIds)) {
            return 0;
        }
        List<Long> accountIdLongs = accountIds.stream()
                .map(this::parseLongOrNull)
                .filter(Objects::nonNull)
                .distinct()
                .collect(Collectors.toList());
        if (CollUtil.isEmpty(accountIdLongs)) {
            return 0;
        }
        List<FbAccountDO> accounts = accountMapper.selectBatchIds(accountIdLongs);
        if (CollUtil.isEmpty(accounts)) {
            return 0;
        }
        Map<Long, String> accountMap = accounts.stream()
                .collect(Collectors.toMap(FbAccountDO::getId, FbAccountDO::getFbAccount, (a, b) -> a));

        int created = 0;
        for (String groupIdOrUrl : monitorGroupIds) {
            String groupUrl = normalizeGroupMonitorUrl(groupIdOrUrl);
            if (StrUtil.isBlank(groupUrl) || existsTodayAutoGroupCollect(groupUrl)) {
                continue;
            }
            FbCollectDO task = new FbCollectDO();
            task.setTaskType(POST_COLLECT_TASK_TYPE);
            task.setSearchType(2);
            task.setSearchUrl(groupUrl);
            task.setExpectedCount(DEFAULT_COLLECT_EXPECTED_COUNT);
            task.setIntervalSeconds(5);
            task.setStatus(1);
            task.setStartTime(LocalDateTime.now());
            task.setRemark(AUTO_GROUP_COLLECT_REMARK + ":" + groupIdOrUrl);
            task.setTotalExpectedCount(accountIdLongs.size() * DEFAULT_COLLECT_EXPECTED_COUNT);
            task.setTotalCollectedCount(0);
            task.setAccountCount(accountIdLongs.size());
            task.setUrlCount(1);
            task.setFbAccount(accountMap.get(accountIdLongs.get(0)));
            collectMapper.insert(task);

            for (Long accountId : accountIdLongs) {
                FbCollectDetailDO detail = new FbCollectDetailDO();
                detail.setTaskId(task.getId());
                detail.setFbAccount(StrUtil.blankToDefault(accountMap.get(accountId), "account_" + accountId));
                detail.setSearchUrl(groupUrl);
                detail.setExpectedCount(DEFAULT_COLLECT_EXPECTED_COUNT);
                detail.setCollectedCount(0);
                detail.setStatus(0);
                collectDetailMapper.insert(detail);
            }
            created++;
        }
        return created;
    }

    private boolean existsTodayAutoCollect(String searchUrl) {
        LocalDateTime startOfDay = LocalDateTime.now().with(LocalTime.MIN);
        Long count = collectMapper.selectCount(new LambdaQueryWrapper<FbCollectDO>()
                .eq(FbCollectDO::getTaskType, POST_COLLECT_TASK_TYPE)
                .eq(FbCollectDO::getSearchUrl, searchUrl)
                .like(FbCollectDO::getRemark, AUTO_COLLECT_REMARK)
                .ge(FbCollectDO::getCreateTime, startOfDay));
        return count != null && count > 0;
    }

    private boolean existsTodayAutoGroupCollect(String searchUrl) {
        LocalDateTime startOfDay = LocalDateTime.now().with(LocalTime.MIN);
        Long count = collectMapper.selectCount(new LambdaQueryWrapper<FbCollectDO>()
                .eq(FbCollectDO::getTaskType, POST_COLLECT_TASK_TYPE)
                .eq(FbCollectDO::getSearchUrl, searchUrl)
                .like(FbCollectDO::getRemark, AUTO_GROUP_COLLECT_REMARK)
                .ge(FbCollectDO::getCreateTime, startOfDay));
        return count != null && count > 0;
    }

    private int analyzePendingPosts(FbAiAgentConfigDO config, List<String> seedKeywords,
                                    List<String> targetCountries, List<String> targetLanguages) {
        List<FbCollectPostDO> posts = collectPostMapper.selectList(new LambdaQueryWrapper<FbCollectPostDO>()
                .isNull(FbCollectPostDO::getLastAiAnalyzeTime)
                .orderByDesc(FbCollectPostDO::getId)
                .last("LIMIT " + MAX_ANALYZE_PER_RUN));
        if (CollUtil.isEmpty(posts)) {
            return 0;
        }
        for (FbCollectPostDO post : posts) {
            String sourceText = joinText(post.getPostContent(), post.getPostUser(), post.getGroupName());
            LeadAnalysisResult result = analyzeLeadByWorkflow(config, "post", post.getId(), sourceText,
                    seedKeywords, targetCountries, targetLanguages);
            if (result == null) {
                result = analyzeText(sourceText, seedKeywords, targetCountries, targetLanguages, true);
            }
            FbCollectPostDO updateObj = new FbCollectPostDO();
            updateObj.setId(post.getId());
            fillPostAnalysis(updateObj, result);
            collectPostMapper.updateById(updateObj);
        }
        return posts.size();
    }

    private int analyzePendingUsers(FbAiAgentConfigDO config, List<String> seedKeywords,
                                    List<String> targetCountries, List<String> targetLanguages) {
        List<FbCollectUserDO> users = collectUserMapper.selectList(new LambdaQueryWrapper<FbCollectUserDO>()
                .isNull(FbCollectUserDO::getLastAiAnalyzeTime)
                .orderByDesc(FbCollectUserDO::getId)
                .last("LIMIT " + MAX_ANALYZE_PER_RUN));
        if (CollUtil.isEmpty(users)) {
            return 0;
        }
        for (FbCollectUserDO user : users) {
            String sourceText = joinText(user.getUserName(), user.getCategory(), user.getProfileStatus(), user.getLastPostSummary(),
                    user.getWebsite(), user.getCity(), user.getLocation());
            LeadAnalysisResult result = analyzeLeadByWorkflow(config, "user", user.getId(), sourceText,
                    seedKeywords, targetCountries, targetLanguages);
            if (result == null) {
                result = analyzeText(sourceText, seedKeywords, targetCountries, targetLanguages, false);
            }
            FbCollectUserDO updateObj = new FbCollectUserDO();
            updateObj.setId(user.getId());
            fillUserAnalysis(updateObj, result);
            collectUserMapper.updateById(updateObj);
        }
        return users.size();
    }

    private int queueHighIntentTouches(FbAiAgentConfigDO config, List<String> accountIds) {
        if (CollUtil.isEmpty(accountIds)) {
            return 0;
        }
        int queued = 0;
        if (Boolean.TRUE.equals(config.getAutoCommentEnabled())) {
            int remaining = Math.min(MAX_TOUCH_QUEUE_PER_RUN - queued, remainingDailyTouchLimit(config.getDailyCommentLimit(), "comment"));
            queued += queuePostCommentTouches(config, accountIds, remaining);
        }
        if (queued < MAX_TOUCH_QUEUE_PER_RUN && Boolean.TRUE.equals(config.getAutoDmEnabled())) {
            int remaining = Math.min(MAX_TOUCH_QUEUE_PER_RUN - queued, remainingDailyTouchLimit(config.getDailyDmLimit(), "dm"));
            queued += queueUserDmTouches(config, accountIds, remaining);
        }
        return queued;
    }

    private int activateDueTouchRecords(FbAiAgentConfigDO config) {
        List<FbAiTouchRecordDO> records = touchRecordMapper.selectList(new LambdaQueryWrapper<FbAiTouchRecordDO>()
                .eq(FbAiTouchRecordDO::getAgentConfigId, config.getId())
                .eq(FbAiTouchRecordDO::getStatus, 0)
                .le(FbAiTouchRecordDO::getScheduledTime, LocalDateTime.now())
                .orderByAsc(FbAiTouchRecordDO::getScheduledTime)
                .last("LIMIT " + MAX_TOUCH_QUEUE_PER_RUN));
        if (CollUtil.isEmpty(records)) {
            return 0;
        }
        int activated = 0;
        for (FbAiTouchRecordDO record : records) {
            try {
                if ("comment".equals(record.getTouchType())) {
                    Long taskId = createCommentOperationTask(record);
                    markTouchRecordRunning(record.getId(), taskId, null);
                    activated++;
                } else if ("dm".equals(record.getTouchType())) {
                    Long taskId = createDmOperationTask(config, record);
                    markTouchRecordRunning(record.getId(), taskId, null);
                    activated++;
                }
            } catch (Exception ex) {
                log.warn("AI触达记录转执行任务失败, recordId={}, reason={}", record.getId(), ex.getMessage(), ex);
                updateTouchRecordResult(record.getId(), 3, ex.getMessage());
            }
        }
        return activated;
    }

    private Long createCommentOperationTask(FbAiTouchRecordDO record) {
        FbOperationTaskSaveReqVO reqVO = new FbOperationTaskSaveReqVO();
        reqVO.setTaskType(POST_COMMENT_TASK_TYPE);
        reqVO.setTaskName("AI自动评论-" + record.getId());
        reqVO.setAccountIds(Collections.singletonList(record.getAccountId()));
        reqVO.setPostUrls(Collections.singletonList(record.getTargetUrl()));
        reqVO.setPostUrl(record.getTargetUrl());
        reqVO.setExpectedCount(1);
        reqVO.setCommentScript(record.getGeneratedContent());
        reqVO.setRemark("AI_AGENT_TOUCH_RECORD:" + record.getId());
        reqVO.setActionConfig(JSONUtil.createObj()
                .set("actions", Collections.singletonList(6))
                .set("postUrls", Collections.singletonList(record.getTargetUrl()))
                .set("commentScripts", Collections.singletonList(record.getGeneratedContent()))
                .set("commentAppendRandomEmoji", false)
                .set("source", "ai_agent")
                .set("touchRecordId", record.getId())
                .toString());
        return operationTaskService.createOperationTask(reqVO);
    }

    private Long createDmOperationTask(FbAiAgentConfigDO config, FbAiTouchRecordDO record) {
        List<Integer> delayRange = parseJsonIntegerList(config.getReplyDelayRange());
        int minDelay = CollUtil.isNotEmpty(delayRange) ? delayRange.get(0) : 180;
        int maxDelay = delayRange.size() > 1 ? delayRange.get(1) : 600;
        if (maxDelay < minDelay) {
            maxDelay = minDelay;
        }

        FbDmTaskSaveReqVO reqVO = new FbDmTaskSaveReqVO();
        reqVO.setTargetUserIds(Collections.singletonList(record.getTargetUserId()));
        reqVO.setScripts(Collections.singletonList(record.getGeneratedContent()));
        reqVO.setScriptType(1);
        reqVO.setAppendRandomEmoji(false);
        reqVO.setAccountIds(Collections.singletonList(record.getAccountId()));
        reqVO.setMinIntervalSeconds(minDelay);
        reqVO.setMaxIntervalSeconds(maxDelay);
        reqVO.setRemark("AI_AGENT_TOUCH_RECORD:" + record.getId());
        return dmTaskService.createDmTask(reqVO);
    }

    private void markTouchRecordRunning(Long recordId, Long taskId, Long detailId) {
        FbAiTouchRecordDO updateObj = new FbAiTouchRecordDO();
        updateObj.setId(recordId);
        updateObj.setStatus(1);
        updateObj.setOperationTaskId(taskId);
        updateObj.setOperationDetailId(detailId);
        touchRecordMapper.updateById(updateObj);
    }

    private int queuePostCommentTouches(FbAiAgentConfigDO config, List<String> accountIds, int limit) {
        if (limit <= 0) {
            return 0;
        }
        List<FbCollectPostDO> posts = collectPostMapper.selectList(new LambdaQueryWrapper<FbCollectPostDO>()
                .eq(FbCollectPostDO::getIntentLevel, "high")
                .and(wrapper -> wrapper.isNull(FbCollectPostDO::getTouchStatus)
                        .or().eq(FbCollectPostDO::getTouchStatus, "not_touched"))
                .orderByDesc(FbCollectPostDO::getProductRelevanceScore)
                .orderByDesc(FbCollectPostDO::getId)
                .last("LIMIT " + limit));
        if (CollUtil.isEmpty(posts)) {
            return 0;
        }
        int queued = 0;
        for (FbCollectPostDO post : posts) {
            if (StrUtil.isBlank(post.getUrl()) || existsTouchRecord("post", post.getId(), "comment")) {
                continue;
            }
            String accountId = pickAccount(accountIds, queued);
            FbAiTouchRecordDO record = buildTouchRecord(config, "post", post.getId(), post.getUrl(),
                    null, accountId, "comment", buildCommentContent(config, post));
            createTouchRecord(record);
            markPostTouched(post.getId());
            queued++;
        }
        return queued;
    }

    private int queueUserDmTouches(FbAiAgentConfigDO config, List<String> accountIds, int limit) {
        if (limit <= 0) {
            return 0;
        }
        List<FbCollectUserDO> users = collectUserMapper.selectList(new LambdaQueryWrapper<FbCollectUserDO>()
                .eq(FbCollectUserDO::getIntentLevel, "high")
                .isNotNull(FbCollectUserDO::getFbUserId)
                .and(wrapper -> wrapper.isNull(FbCollectUserDO::getTouchStatus)
                        .or().eq(FbCollectUserDO::getTouchStatus, "not_touched"))
                .orderByDesc(FbCollectUserDO::getProductRelevanceScore)
                .orderByDesc(FbCollectUserDO::getId)
                .last("LIMIT " + limit));
        if (CollUtil.isEmpty(users)) {
            return 0;
        }
        int queued = 0;
        for (FbCollectUserDO user : users) {
            if (StrUtil.isBlank(user.getFbUserId()) || existsTouchRecord("user", user.getId(), "dm")) {
                continue;
            }
            String accountId = pickAccount(accountIds, queued);
            FbAiTouchRecordDO record = buildTouchRecord(config, "user", user.getId(), user.getUrl(),
                    user.getFbUserId(), accountId, "dm", buildDmContent(config, user));
            createTouchRecord(record);
            markUserTouched(user.getId());
            queued++;
        }
        return queued;
    }

    private FbAiTouchRecordDO buildTouchRecord(FbAiAgentConfigDO config, String leadType, Long leadId,
                                               String targetUrl, String targetUserId, String accountId,
                                               String touchType, String generatedContent) {
        FbAiTouchRecordDO record = new FbAiTouchRecordDO();
        record.setAgentConfigId(config.getId());
        record.setLeadType(leadType);
        record.setLeadId(leadId);
        record.setTargetUrl(targetUrl);
        record.setTargetUserId(targetUserId);
        record.setAccountId(accountId);
        record.setAccountDbId(parseLongOrNull(accountId));
        record.setFbAccount(resolveFbAccount(accountId));
        record.setTouchType(touchType);
        record.setGeneratedContent(generatedContent);
        record.setAiReason("命中高意向线索，进入AI自动触达队列");
        record.setStatus(0);
        record.setScheduledTime(LocalDateTime.now().plusSeconds(randomDelaySeconds(config.getReplyDelayRange())));
        return record;
    }

    private boolean existsTouchRecord(String leadType, Long leadId, String touchType) {
        Long count = touchRecordMapper.selectCount(new LambdaQueryWrapper<FbAiTouchRecordDO>()
                .eq(FbAiTouchRecordDO::getLeadType, leadType)
                .eq(FbAiTouchRecordDO::getLeadId, leadId)
                .eq(FbAiTouchRecordDO::getTouchType, touchType)
                .in(FbAiTouchRecordDO::getStatus, Arrays.asList(0, 1, 2)));
        return count != null && count > 0;
    }

    private void markPostTouched(Long postId) {
        FbCollectPostDO updateObj = new FbCollectPostDO();
        updateObj.setId(postId);
        updateObj.setTouchStatus("touched");
        updateObj.setLastTouchTime(LocalDateTime.now());
        collectPostMapper.updateById(updateObj);
    }

    private void markUserTouched(Long userId) {
        FbCollectUserDO updateObj = new FbCollectUserDO();
        updateObj.setId(userId);
        updateObj.setTouchStatus("touched");
        updateObj.setLastTouchTime(LocalDateTime.now());
        collectUserMapper.updateById(updateObj);
    }

    private LeadAnalysisResult analyzeText(String text, List<String> seedKeywords,
                                           List<String> targetCountries, List<String> targetLanguages,
                                           boolean postLead) {
        String source = StrUtil.nullToEmpty(text);
        String lower = source.toLowerCase(Locale.ROOT);
        int score = 0;
        List<String> tags = new ArrayList<>();

        int seedHits = 0;
        for (String keyword : seedKeywords) {
            if (StrUtil.isNotBlank(keyword) && lower.contains(keyword.toLowerCase(Locale.ROOT))) {
                seedHits++;
            }
        }
        score += Math.min(seedHits * 25, 50);

        if (containsAny(lower, "buy", "purchase", "looking for", "need supplier", "supplier", "quote", "quotation",
                "price", "bulk", "wholesale", "distributor", "采购", "供应商", "报价", "批发", "经销", "代理")) {
            score += 35;
            tags.add("寻找供应商");
            tags.add("高意向询价");
        }
        if (containsAny(lower, "distributor", "dealer", "reseller", "wholesale", "经销", "代理", "批发")) {
            score += 20;
            tags.add("潜在经销商");
        }
        if (containsAny(lower, "bad quality", "broken", "not working", "complaint", "expensive", "slow delivery",
                "质量差", "坏了", "太贵", "投诉", "发货慢")) {
            score += 15;
            tags.add("竞品抱怨");
        }
        if (tags.isEmpty()) {
            tags.add(score >= 45 ? "待人工确认" : "普通消费者");
        }

        LeadAnalysisResult result = new LeadAnalysisResult();
        result.aiTags = tags.stream().distinct().collect(Collectors.joining(","));
        result.productRelevanceScore = Math.min(score, 100);
        result.intentLevel = score >= 60 ? "high" : score >= 35 ? "medium" : "low";
        result.intentReason = buildIntentReason(seedHits, tags, score);
        result.sentiment = containsAny(lower, "bad", "broken", "complaint", "angry", "质量差", "投诉", "坏了")
                ? "negative" : "neutral";
        result.leadType = postLead ? "post" : inferUserLeadType(lower);
        result.country = inferFirstMatched(source, targetCountries);
        result.language = inferFirstMatched(source, targetLanguages);
        result.aiSummary = StrUtil.maxLength(source.replaceAll("\\s+", " ").trim(), 240);
        result.touchStatus = "not_touched";
        return result;
    }

    private void fillPostAnalysis(FbCollectPostDO updateObj, LeadAnalysisResult result) {
        updateObj.setAiTags(result.aiTags);
        updateObj.setIntentLevel(result.intentLevel);
        updateObj.setIntentReason(result.intentReason);
        updateObj.setSentiment(result.sentiment);
        updateObj.setLeadType(result.leadType);
        updateObj.setCountry(result.country);
        updateObj.setLanguage(result.language);
        updateObj.setProductRelevanceScore(result.productRelevanceScore);
        updateObj.setAiSummary(result.aiSummary);
        updateObj.setLastAiAnalyzeTime(LocalDateTime.now());
        updateObj.setTouchStatus(result.touchStatus);
    }

    private void fillUserAnalysis(FbCollectUserDO updateObj, LeadAnalysisResult result) {
        updateObj.setAiTags(result.aiTags);
        updateObj.setIntentLevel(result.intentLevel);
        updateObj.setIntentReason(result.intentReason);
        updateObj.setSentiment(result.sentiment);
        updateObj.setLeadType(result.leadType);
        updateObj.setCountry(result.country);
        updateObj.setProductRelevanceScore(result.productRelevanceScore);
        updateObj.setAiSummary(result.aiSummary);
        updateObj.setLastAiAnalyzeTime(LocalDateTime.now());
        updateObj.setTouchStatus(result.touchStatus);
    }

    private String buildSearchTopUrl(String keyword) {
        return "https://www.facebook.com/search/top?q=" + URLEncoder.encode(keyword, StandardCharsets.UTF_8);
    }

    private String normalizeGroupMonitorUrl(String groupIdOrUrl) {
        if (StrUtil.isBlank(groupIdOrUrl)) {
            return "";
        }
        String raw = groupIdOrUrl.trim();
        if (raw.startsWith("http://") || raw.startsWith("https://")) {
            int queryIndex = raw.indexOf('?');
            int hashIndex = raw.indexOf('#');
            int endIndex = raw.length();
            if (queryIndex >= 0) {
                endIndex = Math.min(endIndex, queryIndex);
            }
            if (hashIndex >= 0) {
                endIndex = Math.min(endIndex, hashIndex);
            }
            raw = raw.substring(0, endIndex);
            int groupsIndex = raw.indexOf("/groups/");
            if (groupsIndex >= 0) {
                String path = raw.substring(groupsIndex + "/groups/".length());
                String groupId = path.split("/")[0];
                return StrUtil.isBlank(groupId) ? "" : "https://www.facebook.com/groups/" + groupId;
            }
            return raw;
        }
        return "https://www.facebook.com/groups/" + raw;
    }

    private String buildCommentContent(FbAiAgentConfigDO config, FbCollectPostDO post) {
        String workflowContent = generateContentByWorkflow(config.getCommentWorkflowId(), "comment",
                joinText(post.getPostContent(), post.getPostUser(), post.getGroupName()), post.getUrl());
        if (StrUtil.isNotBlank(workflowContent)) {
            return workflowContent;
        }
        String topic = StrUtil.blankToDefault(post.getPostUser(), "there");
        return "Hi " + topic + ", this looks relevant. I can share more details if you're comparing suppliers.";
    }

    private String buildDmContent(FbAiAgentConfigDO config, FbCollectUserDO user) {
        String workflowContent = generateContentByWorkflow(config.getDmWorkflowId(), "dm",
                joinText(user.getUserName(), user.getCategory(), user.getProfileStatus(), user.getLastPostSummary(),
                        user.getWebsite(), user.getCity(), user.getLocation()), user.getUrl());
        if (StrUtil.isNotBlank(workflowContent)) {
            return workflowContent;
        }
        String name = StrUtil.blankToDefault(user.getUserName(), "there");
        return "Hi " + name + ", noticed your recent activity and thought our product info might be useful. Happy to share details if needed.";
    }

    private int remainingDailyTouchLimit(Integer dailyLimit, String touchType) {
        if (dailyLimit == null || dailyLimit <= 0) {
            return 0;
        }
        LocalDateTime startOfDay = LocalDateTime.now().with(LocalTime.MIN);
        Long used = touchRecordMapper.selectCount(new LambdaQueryWrapper<FbAiTouchRecordDO>()
                .eq(FbAiTouchRecordDO::getTouchType, touchType)
                .ge(FbAiTouchRecordDO::getCreateTime, startOfDay)
                .in(FbAiTouchRecordDO::getStatus, Arrays.asList(0, 1, 2)));
        return Math.max(0, dailyLimit - (used == null ? 0 : used.intValue()));
    }

    private LeadAnalysisResult analyzeLeadByWorkflow(FbAiAgentConfigDO config, String leadType, Long leadId, String sourceText,
                                                     List<String> seedKeywords, List<String> targetCountries,
                                                     List<String> targetLanguages) {
        if (config.getLeadScoreWorkflowId() == null) {
            return null;
        }
        Map<String, Object> params = new LinkedHashMap<>();
        params.put("leadType", leadType);
        params.put("leadId", leadId);
        params.put("text", sourceText);
        params.put("seedKeywords", seedKeywords);
        params.put("targetCountries", targetCountries);
        params.put("targetLanguages", targetLanguages);
        params.put("knowledgeIds", parseCsvStringList(config.getKnowledgeIds()));
        params.put("personaConfig", config.getPersonaConfig());

        Object result = invokeAiWorkflow(config.getLeadScoreWorkflowId(), params);
        return parseLeadAnalysisResult(result);
    }

    private String generateContentByWorkflow(Long workflowId, String touchType, String sourceText, String targetUrl) {
        if (workflowId == null) {
            return "";
        }
        Map<String, Object> params = new LinkedHashMap<>();
        params.put("touchType", touchType);
        params.put("text", sourceText);
        params.put("targetUrl", targetUrl);
        Object result = invokeAiWorkflow(workflowId, params);
        return parseGeneratedContent(result);
    }

    private Object invokeAiWorkflow(Long workflowId, Map<String, Object> params) {
        try {
            if (!applicationContext.containsBean("aiWorkflowServiceImpl")) {
                return null;
            }
            Object workflowService = applicationContext.getBean("aiWorkflowServiceImpl");
            Class<?> reqClass = Class.forName("cn.iocoder.yudao.module.ai.controller.admin.workflow.vo.AiWorkflowTestReqVO");
            Object req = reqClass.getDeclaredConstructor().newInstance();
            reqClass.getMethod("setId", Long.class).invoke(req, workflowId);
            reqClass.getMethod("setParams", Map.class).invoke(req, params);
            Method testWorkflow = workflowService.getClass().getMethod("testWorkflow", reqClass);
            return testWorkflow.invoke(workflowService, req);
        } catch (Exception ex) {
            log.warn("调用AI工作流失败，workflowId={}, reason={}", workflowId, ex.getMessage());
            return null;
        }
    }

    private LeadAnalysisResult parseLeadAnalysisResult(Object rawResult) {
        if (rawResult == null) {
            return null;
        }
        JSONObject json = parseWorkflowResultObject(rawResult);
        if (json == null || json.isEmpty()) {
            return null;
        }
        LeadAnalysisResult result = new LeadAnalysisResult();
        result.aiTags = normalizeTags(json.get("aiTags"));
        result.intentLevel = StrUtil.blankToDefault(json.getStr("intentLevel"), "medium");
        result.intentReason = json.getStr("intentReason");
        result.sentiment = StrUtil.blankToDefault(json.getStr("sentiment"), "neutral");
        result.leadType = json.getStr("leadType");
        result.country = json.getStr("country");
        result.language = json.getStr("language");
        result.productRelevanceScore = Optional.ofNullable(json.getInt("productRelevanceScore")).orElse(50);
        result.aiSummary = StrUtil.maxLength(StrUtil.blankToDefault(json.getStr("aiSummary"), json.toString()), 240);
        result.touchStatus = "not_touched";
        return result;
    }

    private String parseGeneratedContent(Object rawResult) {
        if (rawResult == null) {
            return "";
        }
        if (rawResult instanceof CharSequence) {
            String text = rawResult.toString().trim();
            if (!JSONUtil.isTypeJSON(text)) {
                return text;
            }
        }
        JSONObject json = parseWorkflowResultObject(rawResult);
        if (json == null) {
            return "";
        }
        return StrUtil.blankToDefault(json.getStr("content"),
                StrUtil.blankToDefault(json.getStr("reply"), json.getStr("message")));
    }

    private JSONObject parseWorkflowResultObject(Object rawResult) {
        try {
            if (rawResult instanceof JSONObject) {
                return (JSONObject) rawResult;
            }
            if (rawResult instanceof Map) {
                return JSONUtil.parseObj(rawResult);
            }
            String text = String.valueOf(rawResult);
            if (JSONUtil.isTypeJSON(text)) {
                return JSONUtil.parseObj(text);
            }
        } catch (Exception ex) {
            log.warn("解析AI工作流结果失败，result={}, reason={}", rawResult, ex.getMessage());
        }
        return null;
    }

    private String normalizeTags(Object tags) {
        if (tags == null) {
            return "待人工确认";
        }
        if (tags instanceof Collection) {
            return ((Collection<?>) tags).stream()
                    .map(String::valueOf)
                    .filter(StrUtil::isNotBlank)
                    .distinct()
                    .collect(Collectors.joining(","));
        }
        return String.valueOf(tags);
    }

    private String resolveFbAccount(String accountId) {
        Long id = parseLongOrNull(accountId);
        if (id == null) {
            return "";
        }
        FbAccountDO account = accountMapper.selectById(id);
        return account != null ? StrUtil.nullToEmpty(account.getFbAccount()) : "";
    }

    private String pickAccount(List<String> accountIds, int offset) {
        return accountIds.get(Math.floorMod(offset, accountIds.size()));
    }

    private int randomDelaySeconds(String replyDelayRange) {
        List<Integer> range = parseJsonIntegerList(replyDelayRange);
        int min = CollUtil.isNotEmpty(range) ? range.get(0) : 180;
        int max = range.size() > 1 ? range.get(1) : 600;
        if (max < min) {
            max = min;
        }
        return ThreadLocalRandom.current().nextInt(min, max + 1);
    }

    private List<String> parseJsonStringList(String value) {
        if (StrUtil.isBlank(value)) {
            return Collections.emptyList();
        }
        try {
            return JSONUtil.parseArray(value).stream()
                    .map(item -> item == null ? "" : String.valueOf(item).trim())
                    .filter(StrUtil::isNotBlank)
                    .distinct()
                    .collect(Collectors.toList());
        } catch (Exception ignored) {
            return parseCsvStringList(value);
        }
    }

    private List<Integer> parseJsonIntegerList(String value) {
        if (StrUtil.isBlank(value)) {
            return Collections.emptyList();
        }
        try {
            return JSONUtil.parseArray(value).stream()
                    .map(item -> item instanceof Number ? ((Number) item).intValue() : Integer.parseInt(String.valueOf(item)))
                    .collect(Collectors.toList());
        } catch (Exception ignored) {
            return Collections.emptyList();
        }
    }

    private List<String> parseCsvStringList(String value) {
        if (StrUtil.isBlank(value)) {
            return Collections.emptyList();
        }
        return Arrays.stream(value.split(","))
                .map(String::trim)
                .filter(StrUtil::isNotBlank)
                .distinct()
                .collect(Collectors.toList());
    }

    private Long parseLongOrNull(String value) {
        try {
            return StrUtil.isBlank(value) ? null : Long.valueOf(value.trim());
        } catch (NumberFormatException ex) {
            return null;
        }
    }

    private String joinText(String... values) {
        return Arrays.stream(values)
                .filter(StrUtil::isNotBlank)
                .collect(Collectors.joining("\n"));
    }

    private boolean containsAny(String text, String... keywords) {
        for (String keyword : keywords) {
            if (text.contains(keyword.toLowerCase(Locale.ROOT))) {
                return true;
            }
        }
        return false;
    }

    private String inferFirstMatched(String text, List<String> candidates) {
        if (CollUtil.isEmpty(candidates) || StrUtil.isBlank(text)) {
            return null;
        }
        String lower = text.toLowerCase(Locale.ROOT);
        return candidates.stream()
                .filter(StrUtil::isNotBlank)
                .filter(item -> lower.contains(item.toLowerCase(Locale.ROOT)))
                .findFirst()
                .orElse(null);
    }

    private String inferUserLeadType(String lower) {
        if (containsAny(lower, "distributor", "dealer", "reseller", "wholesale", "经销", "代理", "批发")) {
            return "distributor";
        }
        if (containsAny(lower, "company", "factory", "import", "export", "manufacturer", "公司", "工厂", "进口", "出口")) {
            return "business";
        }
        return "consumer";
    }

    private String buildIntentReason(int seedHits, List<String> tags, int score) {
        return String.format("关键词命中%s个，标签=%s，综合分=%s", seedHits, String.join("/", tags), Math.min(score, 100));
    }

    private void normalizeDefaults(FbAiAgentConfigSaveReqVO reqVO) {
        if (reqVO.getStatus() == null) {
            reqVO.setStatus(0);
        }
        if (reqVO.getAutoCommentEnabled() == null) {
            reqVO.setAutoCommentEnabled(false);
        }
        if (reqVO.getAutoDmEnabled() == null) {
            reqVO.setAutoDmEnabled(false);
        }
        if (reqVO.getDailyCommentLimit() == null) {
            reqVO.setDailyCommentLimit(50);
        }
        if (reqVO.getDailyDmLimit() == null) {
            reqVO.setDailyDmLimit(30);
        }
        if (StrUtil.isBlank(reqVO.getReplyDelayRange())) {
            reqVO.setReplyDelayRange("[180,600]");
        }
    }

    private void validateConfig(FbAiAgentConfigSaveReqVO reqVO) {
        if (!Integer.valueOf(1).equals(reqVO.getStatus())) {
            return;
        }
        if (StrUtil.isBlank(reqVO.getKnowledgeIds())) {
            throw exception0(2_011_000_001, "启用Agent前请选择产品知识库");
        }
        if (StrUtil.isBlank(reqVO.getSeedKeywords())) {
            throw exception0(2_011_000_002, "启用Agent前请配置3-5个核心关键词种子");
        }
        if (StrUtil.isBlank(reqVO.getAccountIds())) {
            throw exception0(2_011_000_003, "启用Agent前请选择执行账号池");
        }
        if (!Boolean.TRUE.equals(reqVO.getAutoCommentEnabled()) && !Boolean.TRUE.equals(reqVO.getAutoDmEnabled())) {
            throw exception0(2_011_000_004, "启用Agent前至少开启自动评论或自动私信");
        }
    }

    private static class AgentDispatchStats {
        private int createdCollectTasks;
        private int analyzedPosts;
        private int analyzedUsers;
        private int queuedTouches;
        private int activatedTouches;
    }

    private static class LeadAnalysisResult {
        private String aiTags;
        private String intentLevel;
        private String intentReason;
        private String sentiment;
        private String leadType;
        private String country;
        private String language;
        private Integer productRelevanceScore;
        private String aiSummary;
        private String touchStatus;
    }

}
