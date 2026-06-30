package cn.iocoder.yudao.module.facebook.service.agent;

import cn.hutool.core.collection.CollUtil;
import cn.hutool.core.util.StrUtil;
import cn.hutool.json.JSONObject;
import cn.hutool.json.JSONUtil;
import cn.iocoder.yudao.framework.common.pojo.PageResult;
import cn.iocoder.yudao.framework.common.util.object.BeanUtils;
import cn.iocoder.yudao.module.ai.dal.dataobject.workflow.AiWorkflowDO;
import cn.iocoder.yudao.module.ai.dal.mysql.workflow.AiWorkflowMapper;
import cn.iocoder.yudao.module.ai.service.workflow.AiWorkflowService;
import cn.iocoder.yudao.module.facebook.controller.admin.agent.vo.*;
import cn.iocoder.yudao.module.facebook.controller.admin.dmtask.vo.FbDmTaskSaveReqVO;
import cn.iocoder.yudao.module.facebook.controller.admin.operation.vo.FbOperationTaskSaveReqVO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.account.FbAccountDO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.agent.FbAiAgentConfigDO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.agent.FbAiAgentDiscoveryLogDO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.agent.FbAiAgentRunLogDO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.agent.FbAiTouchRecordDO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.collect.FbCollectDO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.collectdetail.FbCollectDetailDO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.collectuser.FbCollectUserDO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.fbcollectpost.FbCollectPostDO;
import cn.iocoder.yudao.module.facebook.dal.mysql.account.FbAccountMapper;
import cn.iocoder.yudao.module.facebook.dal.mysql.agent.FbAiAgentConfigMapper;
import cn.iocoder.yudao.module.facebook.dal.mysql.agent.FbAiAgentDiscoveryLogMapper;
import cn.iocoder.yudao.module.facebook.dal.mysql.agent.FbAiAgentRunLogMapper;
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
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;
import org.springframework.validation.annotation.Validated;

import java.net.URLEncoder;
import java.nio.charset.StandardCharsets;
import java.time.Duration;
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
    private static final String AGENT_TYPE_PAGE_LEAD = "page_lead";
    private static final int PAGE_COLLECT_TASK_TYPE = 1;
    private static final int POST_COLLECT_TASK_TYPE = 2;
    private static final int DEEP_COLLECT_TASK_TYPE = 12;
    private static final int DEFAULT_COLLECT_EXPECTED_COUNT = 20;
    private static final int MAX_ANALYZE_PER_RUN = 50;
    private static final int AI_ANALYZE_BATCH_SIZE = 50;
    private static final int MAX_TOUCH_QUEUE_PER_RUN = 20;
    private static final int POST_COMMENT_TASK_TYPE = 15;
    private static final String DEFAULT_KEYWORD_WORKFLOW_CODE = "fb_ai_keyword_expand_v1";
    private static final String DEFAULT_LEAD_ANALYZE_WORKFLOW_CODE = "fb_ai_page_lead_scoring_v1";

    @Resource
    private FbAiAgentConfigMapper agentConfigMapper;
    @Resource
    private FbAiAgentDiscoveryLogMapper discoveryLogMapper;
    @Resource
    private FbAiAgentRunLogMapper runLogMapper;
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
    private AiWorkflowService aiWorkflowService;
    @Resource
    private AiWorkflowMapper aiWorkflowMapper;
    @Resource
    private FbAiAgentCollectQueueService collectQueueService;

    @Override
    @Transactional(rollbackFor = Exception.class)
    public Long saveConfig(FbAiAgentConfigSaveReqVO saveReqVO) {
        normalizeDefaults(saveReqVO);
        validateConfig(saveReqVO);

        FbAiAgentConfigDO config = BeanUtils.toBean(saveReqVO, FbAiAgentConfigDO.class);
        if (config.getId() == null) {
            agentConfigMapper.insert(config);
            addRunLog(config.getId(), "Agent创建完成", "已创建AI主页获客Agent：" + config.getAgentName(), "success");
            return config.getId();
        }
        agentConfigMapper.updateById(config);
        addRunLog(config.getId(), "Agent配置已更新", "已保存Agent配置", "info");
        return config.getId();
    }

    @Override
    public PageResult<FbAiAgentConfigDO> getConfigPage(FbAiAgentConfigPageReqVO pageReqVO) {
        PageResult<FbAiAgentConfigDO> pageResult = agentConfigMapper.selectPage(pageReqVO);
        if (CollUtil.isEmpty(pageResult.getList())) {
            return pageResult;
        }
        pageResult.getList().forEach(this::fillAgentSummary);
        return pageResult;
    }

    @Override
    public FbAiAgentConfigDO getConfig(Long id) {
        FbAiAgentConfigDO config = agentConfigMapper.selectById(id);
        if (config != null) {
            fillAgentSummary(config);
        }
        return config;
    }

    @Override
    public FbAiAgentConfigDO getConfig() {
        FbAiAgentConfigDO config = agentConfigMapper.selectList(new LambdaQueryWrapper<FbAiAgentConfigDO>()
                .orderByDesc(FbAiAgentConfigDO::getId)
                .last("LIMIT 1")).stream().findFirst().orElse(null);
        if (config != null) {
            fillAgentSummary(config);
        }
        return config;
    }

    @Override
    public FbAiAgentConfigDO getEnabledConfig() {
        return agentConfigMapper.selectList(new LambdaQueryWrapper<FbAiAgentConfigDO>()
                .eq(FbAiAgentConfigDO::getStatus, 1)
                .eq(FbAiAgentConfigDO::getAgentType, AGENT_TYPE_PAGE_LEAD)
                .orderByAsc(FbAiAgentConfigDO::getId)
                .last("LIMIT 1")).stream().findFirst().orElse(null);
    }

    @Override
    public void updateStatus(FbAiAgentStatusUpdateReqVO reqVO) {
        FbAiAgentConfigDO updateObj = new FbAiAgentConfigDO();
        updateObj.setId(reqVO.getId());
        updateObj.setStatus(reqVO.getStatus());
        agentConfigMapper.updateById(updateObj);
        addRunLog(reqVO.getId(), "Agent状态已更新", "当前状态：" + reqVO.getStatus(), "info");
    }

    @Override
    public void deleteConfig(Long id) {
        agentConfigMapper.deleteById(id);
    }

    @Override
    public FbAiKeywordGenerateRespVO generateKeywords(FbAiKeywordGenerateReqVO reqVO) {
        List<String> seeds = reqVO.getSeedKeywords() == null ? Collections.emptyList() : reqVO.getSeedKeywords();
        int count = Optional.ofNullable(reqVO.getExpandCount()).orElse(20);
        Map<String, Object> params = new LinkedHashMap<>();
        params.put("type", "keyword_expand");
        params.put("seedKeywords", seeds);
        params.put("targetCountries", reqVO.getTargetCountries());
        params.put("productDescription", reqVO.getProductDescription());
        params.put("expandCount", count);
        Object result = invokeDefaultAiWorkflow(DEFAULT_KEYWORD_WORKFLOW_CODE, params);
        List<String> keywords = parseKeywordWorkflowResult(result);
        if (CollUtil.isEmpty(keywords)) {
            keywords = buildFallbackKeywords(seeds, count);
        }
        return new FbAiKeywordGenerateRespVO(keywords.stream().distinct().limit(count).collect(Collectors.toList()));
    }

    @Override
    public PageResult<FbAiAgentDiscoveryLogDO> getDiscoveryLogPage(FbAiAgentDiscoveryLogPageReqVO pageReqVO) {
        return discoveryLogMapper.selectPage(pageReqVO);
    }

    @Override
    public PageResult<FbAiAgentRunLogDO> getRunLogPage(FbAiAgentRunLogPageReqVO pageReqVO) {
        return runLogMapper.selectPage(pageReqVO);
    }

    @Override
    public PageResult<FbCollectUserDO> getLeadPage(FbAiAgentLeadPageReqVO pageReqVO) {
        List<Long> taskIds = discoveryLogMapper.selectList(new LambdaQueryWrapper<FbAiAgentDiscoveryLogDO>()
                        .eq(FbAiAgentDiscoveryLogDO::getAgentConfigId, pageReqVO.getAgentConfigId())
                        .select(FbAiAgentDiscoveryLogDO::getCollectTaskId))
                .stream()
                .map(FbAiAgentDiscoveryLogDO::getCollectTaskId)
                .filter(Objects::nonNull)
                .distinct()
                .collect(Collectors.toList());
        if (CollUtil.isEmpty(taskIds)) {
            return new PageResult<>(Collections.emptyList(), 0L);
        }
        List<FbCollectUserDO> records = collectUserMapper.selectList(new LambdaQueryWrapper<FbCollectUserDO>()
                .in(FbCollectUserDO::getTaskId, taskIds)
                .orderByDesc(FbCollectUserDO::getProductRelevanceScore)
                .orderByDesc(FbCollectUserDO::getId));
        int pageNo = Math.max(pageReqVO.getPageNo(), 1);
        int pageSize = Math.max(pageReqVO.getPageSize(), 10);
        int fromIndex = Math.min((pageNo - 1) * pageSize, records.size());
        int toIndex = Math.min(fromIndex + pageSize, records.size());
        return new PageResult<>(records.subList(fromIndex, toIndex), (long) records.size());
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
        return dispatchInternal(false, false);
    }

    @Override
    @Transactional(rollbackFor = Exception.class)
    public FbAiAgentDispatchRespVO dispatchScheduled() {
        return dispatchInternal(true, true);
    }

    @Override
    @Transactional(rollbackFor = Exception.class)
    public void continueAfterCollectTaskFinished(Long collectTaskId) {
        if (collectTaskId == null) {
            return;
        }
        FbCollectDO task = collectMapper.selectById(collectTaskId);
        if (task == null || task.getTaskType() == null) {
            return;
        }
        FbAiAgentConfigDO config = findAgentConfigByCollectTaskId(collectTaskId);
        if (config == null || !Objects.equals(config.getStatus(), 1)) {
            return;
        }
        List<String> accountIds = parseCsvStringList(config.getAccountIds());
        List<String> targetCountries = parseJsonStringList(config.getTargetCountries());
        List<String> targetLanguages = parseJsonStringList(config.getTargetLanguages());
        List<String> keywords = parseJsonStringList(config.getKeywordPool());
        if (CollUtil.isEmpty(keywords)) {
            keywords = parseJsonStringList(config.getSeedKeywords());
        }

        if (Objects.equals(task.getTaskType(), PAGE_COLLECT_TASK_TYPE)) {
            List<FbAiAgentDispatchRespVO.CollectDetail> ignored = new ArrayList<>();
            int deepCreated = createDeepCollectTasks(config, accountIds, ignored, true);
            refreshDiscoveryStats(config.getId());
            String deepMessage = deepCreated > 0
                    ? "已完成主页采集，创建深度采集：" + deepCreated + "个，已加入待执行队列"
                    : "已完成主页采集，创建深度采集：0个，可能暂无新主页、主页URL为空或已去重";
            addRunLog(config.getId(), "主页采集完成", deepMessage, deepCreated > 0 ? "success" : "info");
            return;
        }

        if (Objects.equals(task.getTaskType(), DEEP_COLLECT_TASK_TYPE)) {
            int analyzedUsers = analyzePendingUsers(config, keywords, targetCountries, targetLanguages);
            int queuedTouches = queueHighIntentTouches(config, accountIds);
            int activatedTouches = activateDueTouchRecords(config);
            refreshDiscoveryStats(config.getId());
            addRunLog(config.getId(), "深度采集完成", String.format("AI分析%s条，排队触达%s条，转执行%s条",
                    analyzedUsers, queuedTouches, activatedTouches), "success");
            if (queuedTouches > activatedTouches) {
                addRunLog(config.getId(), "触达等待执行", "已排队触达记录会按随机间隔到期后转成评论/私信任务", "info");
            }
        }
    }

    private FbAiAgentDispatchRespVO dispatchInternal(boolean scheduledOnly, boolean enqueueForVuePoller) {
        List<FbAiAgentConfigDO> configs = agentConfigMapper.selectList(new LambdaQueryWrapper<FbAiAgentConfigDO>()
                .eq(FbAiAgentConfigDO::getStatus, 1)
                .eq(FbAiAgentConfigDO::getAgentType, AGENT_TYPE_PAGE_LEAD)
                .orderByAsc(FbAiAgentConfigDO::getId));
        if (CollUtil.isEmpty(configs)) {
            return new FbAiAgentDispatchRespVO(false, "暂无运行中的AI主页获客Agent");
        }
        AgentDispatchStats stats = new AgentDispatchStats();
        List<FbAiAgentDispatchRespVO.CollectDetail> launchDetails = new ArrayList<>();
        int skipped = 0;

        for (FbAiAgentConfigDO config : configs) {
            if (scheduledOnly && !isAgentDue(config)) {
                skipped++;
                continue;
            }
            stats.executedAgents++;
            addRunLog(config.getId(), "开始执行任务", "开始执行AI主页获客：" + config.getAgentName(), "info");
            List<String> accountIds = parseCsvStringList(config.getAccountIds());
            List<String> targetCountries = parseJsonStringList(config.getTargetCountries());
            List<String> targetLanguages = parseJsonStringList(config.getTargetLanguages());
            List<String> runKeywords = pickRunKeywords(config);

            int created = createPageLeadCollectTasks(config, runKeywords, accountIds, launchDetails, enqueueForVuePoller);
            int deepCreated = createDeepCollectTasks(config, accountIds, launchDetails, enqueueForVuePoller);
            int analyzedUsers = analyzePendingUsers(config, runKeywords, targetCountries, targetLanguages);
            int queuedTouches = queueHighIntentTouches(config, accountIds);
            int activatedTouches = activateDueTouchRecords(config);
            stats.createdCollectTasks += created;
            stats.createdDeepCollectTasks += deepCreated;
            stats.analyzedUsers += analyzedUsers;
            stats.queuedTouches += queuedTouches;
            stats.activatedTouches += activatedTouches;
            advanceKeywordCursor(config, runKeywords.size());
            if (scheduledOnly) {
                markAgentExecuted(config.getId());
            }
            addRunLog(config.getId(), "调度结束", String.format("本轮关键词%s个，新建主页采集%s个，深度采集%s个，分析线索%s条，排队触达%s条，转执行%s条",
                    runKeywords.size(), created, deepCreated, analyzedUsers, queuedTouches, activatedTouches), "success");
        }

        if (stats.executedAgents == 0) {
            return new FbAiAgentDispatchRespVO(false, scheduledOnly
                    ? String.format("暂无到达执行时间的AI主页获客Agent，已跳过%s个", skipped)
                    : "暂无可执行的AI主页获客Agent");
        }
        String message = String.format("Agent调度完成：运行Agent%s个，新建主页采集%s个，新建深度采集%s个，分析潜客%s条，排队触达%s条，转执行任务%s条",
                stats.executedAgents, stats.createdCollectTasks, stats.createdDeepCollectTasks, stats.analyzedUsers, stats.queuedTouches, stats.activatedTouches);
        log.info("[dispatchOnce][{}]", message);
        FbAiAgentDispatchRespVO respVO = new FbAiAgentDispatchRespVO(true, message);
        respVO.setDetails(launchDetails);
        return respVO;
    }

    private boolean isAgentDue(FbAiAgentConfigDO config) {
        if (!"daily".equals(StrUtil.blankToDefault(config.getExecuteFrequency(), "daily"))) {
            return false;
        }
        LocalDateTime now = LocalDateTime.now();
        if (config.getLastExecuteTime() != null && config.getLastExecuteTime().toLocalDate().isEqual(now.toLocalDate())) {
            return false;
        }
        LocalTime executeTime = parseExecuteTime(config.getExecuteTime());
        return !now.toLocalTime().isBefore(executeTime);
    }

    private LocalTime parseExecuteTime(String executeTime) {
        if (StrUtil.isBlank(executeTime)) {
            return LocalTime.of(9, 0);
        }
        try {
            return LocalTime.parse(executeTime.trim());
        } catch (Exception ex) {
            return LocalTime.of(9, 0);
        }
    }

    private void markAgentExecuted(Long agentId) {
        FbAiAgentConfigDO updateObj = new FbAiAgentConfigDO();
        updateObj.setId(agentId);
        updateObj.setLastExecuteTime(LocalDateTime.now());
        agentConfigMapper.updateById(updateObj);
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

    private List<String> pickRunKeywords(FbAiAgentConfigDO config) {
        List<String> keywordPool = parseJsonStringList(config.getKeywordPool());
        if (CollUtil.isEmpty(keywordPool)) {
            keywordPool = parseJsonStringList(config.getSeedKeywords());
        }
        if (CollUtil.isEmpty(keywordPool)) {
            return Collections.emptyList();
        }
        int perRun = Optional.ofNullable(config.getKeywordsPerRun()).orElse(5);
        perRun = Math.max(1, Math.min(perRun, keywordPool.size()));
        int cursor = Math.floorMod(Optional.ofNullable(config.getKeywordCursor()).orElse(0), keywordPool.size());
        List<String> result = new ArrayList<>();
        for (int i = 0; i < perRun; i++) {
            result.add(keywordPool.get((cursor + i) % keywordPool.size()));
        }
        return result.stream().filter(StrUtil::isNotBlank).distinct().collect(Collectors.toList());
    }

    private void advanceKeywordCursor(FbAiAgentConfigDO config, int step) {
        List<String> keywordPool = parseJsonStringList(config.getKeywordPool());
        if (CollUtil.isEmpty(keywordPool) || step <= 0) {
            return;
        }
        int nextCursor = Math.floorMod(Optional.ofNullable(config.getKeywordCursor()).orElse(0) + step, keywordPool.size());
        FbAiAgentConfigDO updateObj = new FbAiAgentConfigDO();
        updateObj.setId(config.getId());
        updateObj.setKeywordCursor(nextCursor);
        agentConfigMapper.updateById(updateObj);
    }

    private int createPageLeadCollectTasks(FbAiAgentConfigDO config, List<String> keywords, List<String> accountIds,
                                           List<FbAiAgentDispatchRespVO.CollectDetail> launchDetails,
                                           boolean enqueueForVuePoller) {
        if (CollUtil.isEmpty(keywords) || CollUtil.isEmpty(accountIds)) {
            addRunLog(config.getId(), "主页发现跳过", "关键词池或账号池为空", "warning");
            return 0;
        }
        Map<Long, String> accountMap = resolveAccountMap(accountIds);
        if (accountMap.isEmpty()) {
            addRunLog(config.getId(), "主页发现跳过", "未找到可用账号", "warning");
            return 0;
        }
        List<Long> accountIdLongs = new ArrayList<>(accountMap.keySet());
        int created = 0;
        for (int i = 0; i < keywords.size(); i++) {
            String keyword = StrUtil.trim(keywords.get(i));
            if (StrUtil.isBlank(keyword)) {
                continue;
            }
            Long accountId = accountIdLongs.get(i % accountIdLongs.size());
            String searchUrl = buildSearchPagesUrl(keyword);
            if (!collectQueueService.tryMarkCreated(config.getId(), "page", searchUrl)) {
                continue;
            }
            FbCollectDO task = createCollectTask(PAGE_COLLECT_TASK_TYPE, searchUrl, 1,
                    "AI主页获客:" + config.getAgentName() + ":" + keyword,
                    Collections.singletonList(accountId), accountMap);
            FbCollectDetailDO detail = createCollectDetail(task.getId(), accountId, accountMap.get(accountId), searchUrl, DEFAULT_COLLECT_EXPECTED_COUNT);
            if (enqueueForVuePoller) {
                collectQueueService.push(detail.getId());
            }
            addLaunchDetail(launchDetails, task, detail, accountId);
            createDiscoveryLog(config.getId(), keyword, task.getId());
            addRunLog(config.getId(), "发现主页", "关键词：" + keyword + "，账号：" + accountMap.get(accountId), "info");
            created++;
        }
        return created;
    }

    private int createDeepCollectTasks(FbAiAgentConfigDO config, List<String> accountIds,
                                       List<FbAiAgentDispatchRespVO.CollectDetail> launchDetails,
                                       boolean enqueueForVuePoller) {
        if (CollUtil.isEmpty(accountIds)) {
            return 0;
        }
        Map<Long, String> accountMap = resolveAccountMap(accountIds);
        if (accountMap.isEmpty()) {
            return 0;
        }
        List<Long> accountIdLongs = new ArrayList<>(accountMap.keySet());
        List<Long> discoveryTaskIds = getAgentDiscoveryTaskIds(config.getId());
        if (CollUtil.isEmpty(discoveryTaskIds)) {
            return 0;
        }
        List<FbCollectUserDO> users = collectUserMapper.selectList(new LambdaQueryWrapper<FbCollectUserDO>()
                .in(FbCollectUserDO::getTaskId, discoveryTaskIds)
                .and(wrapper -> wrapper.isNull(FbCollectUserDO::getDeepCollected).or().eq(FbCollectUserDO::getDeepCollected, false))
                .isNotNull(FbCollectUserDO::getUrl)
                .orderByDesc(FbCollectUserDO::getId)
                .last("LIMIT " + Optional.ofNullable(config.getKeywordsPerRun()).orElse(5) * 5));
        if (CollUtil.isEmpty(users)) {
            return 0;
        }
        int created = 0;
        for (FbCollectUserDO user : users) {
            if (StrUtil.isBlank(user.getUrl()) || !collectQueueService.tryMarkCreated(config.getId(), "deep", user.getUrl())) {
                continue;
            }
            Long accountId = accountIdLongs.get(created % accountIdLongs.size());
            FbCollectDO task = createCollectTask(DEEP_COLLECT_TASK_TYPE, user.getUrl(), 0,
                    "AI主页深度采集:" + config.getAgentName() + ":" + user.getId(),
                    Collections.singletonList(accountId), accountMap);
            FbCollectDetailDO detail = createCollectDetail(task.getId(), accountId, accountMap.get(accountId), user.getUrl(), 1);
            detail.setSourceUserId(user.getId());
            collectDetailMapper.updateById(detail);
            if (enqueueForVuePoller) {
                collectQueueService.push(detail.getId());
            }
            addLaunchDetail(launchDetails, task, detail, accountId);
            createDiscoveryLog(config.getId(), "深度采集", task.getId());
            created++;
        }
        if (created > 0) {
            addRunLog(config.getId(), "深度采集排队", "已创建深度采集任务：" + created + "个", "info");
        }
        return created;
    }

    private Map<Long, String> resolveAccountMap(List<String> accountIds) {
        List<Long> accountIdLongs = accountIds.stream()
                .map(this::parseLongOrNull)
                .filter(Objects::nonNull)
                .distinct()
                .collect(Collectors.toList());
        if (CollUtil.isEmpty(accountIdLongs)) {
            return Collections.emptyMap();
        }
        List<FbAccountDO> accounts = accountMapper.selectBatchIds(accountIdLongs);
        if (CollUtil.isEmpty(accounts)) {
            return Collections.emptyMap();
        }
        return accounts.stream()
                .collect(Collectors.toMap(FbAccountDO::getId, FbAccountDO::getFbAccount, (a, b) -> a, LinkedHashMap::new));
    }

    private FbCollectDO createCollectTask(Integer taskType, String searchUrl, Integer searchType, String remark,
                                          List<Long> accountIds, Map<Long, String> accountMap) {
        FbCollectDO task = new FbCollectDO();
        task.setTaskType(taskType);
        task.setSearchType(searchType);
        task.setSearchUrl(searchUrl);
        task.setExpectedCount(taskType == DEEP_COLLECT_TASK_TYPE ? 1 : DEFAULT_COLLECT_EXPECTED_COUNT);
        task.setIntervalSeconds(5);
        task.setStatus(1);
        task.setStartTime(LocalDateTime.now());
        task.setRemark(remark);
        task.setTotalExpectedCount(accountIds.size() * task.getExpectedCount());
        task.setTotalCollectedCount(0);
        task.setAccountCount(accountIds.size());
        task.setUrlCount(1);
        task.setFbAccount(accountMap.get(accountIds.get(0)));
        collectMapper.insert(task);
        return task;
    }

    private FbCollectDetailDO createCollectDetail(Long taskId, Long accountId, String fbAccount, String searchUrl, Integer expectedCount) {
        FbCollectDetailDO detail = new FbCollectDetailDO();
        detail.setTaskId(taskId);
        detail.setFbAccount(StrUtil.blankToDefault(fbAccount, "account_" + accountId));
        detail.setSearchUrl(searchUrl);
        detail.setExpectedCount(expectedCount);
        detail.setCollectedCount(0);
        detail.setStatus(0);
        collectDetailMapper.insert(detail);
        return detail;
    }

    private void addLaunchDetail(List<FbAiAgentDispatchRespVO.CollectDetail> launchDetails, FbCollectDO task,
                                 FbCollectDetailDO detail, Long accountId) {
        if (launchDetails == null || task == null || detail == null) {
            return;
        }
        FbAccountDO account = accountMapper.selectById(accountId);
        launchDetails.add(new FbAiAgentDispatchRespVO.CollectDetail(
                task.getId(),
                detail.getId(),
                detail.getFbAccount(),
                account == null ? null : account.getCookie(),
                detail.getSearchUrl(),
                detail.getSourceUserId(),
                detail.getExpectedCount(),
                task.getTaskType()
        ));
    }

    private void createDiscoveryLog(Long agentConfigId, String keyword, Long collectTaskId) {
        FbAiAgentDiscoveryLogDO logDO = new FbAiAgentDiscoveryLogDO();
        logDO.setAgentConfigId(agentConfigId);
        logDO.setKeyword(keyword);
        logDO.setSourceType("page");
        logDO.setDiscoveredCount(0);
        logDO.setHighIntentCount(0);
        logDO.setPageCollectCount(0);
        logDO.setAiAnalyzeCount(0);
        logDO.setFilteredCount(0);
        logDO.setFinalLeadCount(0);
        logDO.setCollectTaskId(collectTaskId);
        discoveryLogMapper.insert(logDO);
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

    private int analyzePendingUsers(FbAiAgentConfigDO config, List<String> seedKeywords,
                                    List<String> targetCountries, List<String> targetLanguages) {
        List<Long> discoveryTaskIds = getAgentDiscoveryTaskIds(config.getId());
        if (CollUtil.isEmpty(discoveryTaskIds)) {
            return 0;
        }
        List<FbCollectUserDO> users = collectUserMapper.selectList(new LambdaQueryWrapper<FbCollectUserDO>()
                .in(FbCollectUserDO::getTaskId, discoveryTaskIds)
                .eq(FbCollectUserDO::getDeepCollected, true)
                .and(wrapper -> wrapper.isNull(FbCollectUserDO::getLastAiAnalyzeTime)
                        .or().isNull(FbCollectUserDO::getProductRelevanceScore))
                .orderByAsc(FbCollectUserDO::getId)
                .last("LIMIT " + MAX_ANALYZE_PER_RUN));
        if (CollUtil.isEmpty(users)) {
            return 0;
        }
        Map<Long, LeadAnalysisResult> workflowResults = analyzeUsersByWorkflow(config, seedKeywords, users);
        for (FbCollectUserDO user : users) {
            LeadAnalysisResult result = workflowResults.get(user.getId());
            if (result == null) {
                result = buildAiMissingResult();
            }
            FbCollectUserDO updateObj = new FbCollectUserDO();
            updateObj.setId(user.getId());
            fillUserAnalysis(updateObj, result);
            collectUserMapper.updateById(updateObj);
        }
        refreshDiscoveryStats(config.getId());
        addRunLog(config.getId(), "AI分析完成", "本轮AI分析主页客户：" + users.size() + "个", "success");
        return users.size();
    }

    private int queueHighIntentTouches(FbAiAgentConfigDO config, List<String> accountIds) {
        if (CollUtil.isEmpty(accountIds)) {
            return 0;
        }
        List<Long> discoveryTaskIds = getAgentDiscoveryTaskIds(config.getId());
        if (CollUtil.isEmpty(discoveryTaskIds)) {
            return 0;
        }
        int queued = 0;
        if (Boolean.TRUE.equals(config.getAutoCommentEnabled())) {
            int remaining = Math.min(MAX_TOUCH_QUEUE_PER_RUN - queued, remainingDailyTouchLimit(config.getDailyCommentLimit(), "comment"));
            queued += queuePageCommentTouches(config, accountIds, discoveryTaskIds, remaining);
        }
        if (queued < MAX_TOUCH_QUEUE_PER_RUN && Boolean.TRUE.equals(config.getAutoDmEnabled())) {
            int remaining = Math.min(MAX_TOUCH_QUEUE_PER_RUN - queued, remainingDailyTouchLimit(config.getDailyDmLimit(), "dm"));
            queued += queueUserDmTouches(config, accountIds, discoveryTaskIds, remaining);
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
                    markLeadTouched(record);
                    addRunLog(config.getId(), "触达转执行", "已创建AI评论任务：" + taskId, "success");
                    activated++;
                } else if ("dm".equals(record.getTouchType())) {
                    Long taskId = createDmOperationTask(config, record);
                    markTouchRecordRunning(record.getId(), taskId, null);
                    markLeadTouched(record);
                    addRunLog(config.getId(), "触达转执行", "已创建AI私信任务：" + taskId, "success");
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

    private int queueUserDmTouches(FbAiAgentConfigDO config, List<String> accountIds, List<Long> discoveryTaskIds, int limit) {
        if (limit <= 0) {
            return 0;
        }
        int minScore = mapIntentToScore(resolveTouchIntentLevel(config.getTouchScoreThreshold()));
        List<FbCollectUserDO> users = collectUserMapper.selectList(new LambdaQueryWrapper<FbCollectUserDO>()
                .in(FbCollectUserDO::getTaskId, discoveryTaskIds)
                .ge(FbCollectUserDO::getProductRelevanceScore, minScore)
                .isNotNull(FbCollectUserDO::getFbUserId)
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
            queued++;
        }
        return queued;
    }

    private int queuePageCommentTouches(FbAiAgentConfigDO config, List<String> accountIds, List<Long> discoveryTaskIds, int limit) {
        if (limit <= 0) {
            return 0;
        }
        int minScore = mapIntentToScore(resolveTouchIntentLevel(config.getTouchScoreThreshold()));
        List<FbCollectUserDO> users = collectUserMapper.selectList(new LambdaQueryWrapper<FbCollectUserDO>()
                .in(FbCollectUserDO::getTaskId, discoveryTaskIds)
                .ge(FbCollectUserDO::getProductRelevanceScore, minScore)
                .orderByDesc(FbCollectUserDO::getProductRelevanceScore)
                .orderByDesc(FbCollectUserDO::getId)
                .last("LIMIT " + limit));
        if (CollUtil.isEmpty(users)) {
            return 0;
        }
        int queued = 0;
        for (FbCollectUserDO user : users) {
            if (StrUtil.isBlank(user.getUrl()) || !isCommentablePostUrl(user.getUrl()) || existsTouchRecord("user", user.getId(), "comment")) {
                continue;
            }
            String accountId = pickAccount(accountIds, queued);
            FbAiTouchRecordDO record = buildTouchRecord(config, "user", user.getId(), user.getUrl(),
                    user.getFbUserId(), accountId, "comment", buildCommentContent(config, user));
            createTouchRecord(record);
            queued++;
        }
        if (queued == 0 && Boolean.TRUE.equals(config.getAutoCommentEnabled())) {
            addRunLog(config.getId(), "自动评论跳过", "当前主页深度采集结果缺少可直接评论的帖子URL，暂未生成评论任务", "warning");
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

    private void markLeadTouched(FbAiTouchRecordDO record) {
        if (record == null || record.getLeadId() == null) {
            return;
        }
        if ("post".equals(record.getLeadType())) {
            markPostTouched(record.getLeadId());
        } else {
            markUserTouched(record.getLeadId());
        }
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

    private String buildSearchPagesUrl(String keyword) {
        return "https://www.facebook.com/search/pages?q=" + URLEncoder.encode(keyword, StandardCharsets.UTF_8);
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
        String topic = StrUtil.blankToDefault(post.getPostUser(), "there");
        return "Hi " + topic + ", this looks relevant. I can share more details if you're comparing suppliers.";
    }

    private String buildCommentContent(FbAiAgentConfigDO config, FbCollectUserDO user) {
        String aiMessage = extractAiSummaryMessage(user.getAiSummary(), "comment_message");
        if (StrUtil.isNotBlank(aiMessage)) {
            return aiMessage;
        }
        String topic = StrUtil.blankToDefault(user.getUserName(), "there");
        return "Hi " + topic + ", your page looks relevant to our product line.";
    }

    private String buildDmContent(FbAiAgentConfigDO config, FbCollectUserDO user) {
        String aiMessage = extractAiSummaryMessage(user.getAiSummary(), "dm_message");
        if (StrUtil.isNotBlank(aiMessage)) {
            return aiMessage;
        }
        String name = StrUtil.blankToDefault(user.getUserName(), "there");
        return "Hi " + name + ", noticed your recent activity and thought our product info might be useful. Happy to share details if needed.";
    }

    private String extractAiSummaryMessage(String aiSummary, String field) {
        if (StrUtil.isBlank(aiSummary)) {
            return "";
        }
        try {
            JSONObject json = JSONUtil.parseObj(aiSummary);
            return StrUtil.blankToDefault(json.getStr(field), "");
        } catch (Exception ignored) {
            return "";
        }
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

    private Object invokeAiWorkflow(Long workflowId, Map<String, Object> params) {
        try {
            if (workflowId == null) {
                return null;
            }
            log.info("调用AI工作流开始, workflowId={}, params={}", workflowId, JSONUtil.toJsonStr(params));
            Object result = aiWorkflowService.executeWorkflow(workflowId, params);
            log.info("调用AI工作流完成, workflowId={}, resultType={}, result={}",
                    workflowId, result == null ? "null" : result.getClass().getName(), stringifyAiWorkflowResult(result));
            return result;
        } catch (Exception ex) {
            log.warn("调用AI工作流失败, workflowId={}, params={}, reason={}",
                    workflowId, JSONUtil.toJsonStr(params), ex.getMessage(), ex);
            return null;
        }
    }

    private String stringifyAiWorkflowResult(Object result) {
        if (result == null) {
            return "";
        }
        try {
            return JSONUtil.toJsonStr(result);
        } catch (Exception ignored) {
            return String.valueOf(result);
        }
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

    private Object invokeDefaultAiWorkflow(String workflowCode, Map<String, Object> params) {
        Long workflowId = resolveWorkflowIdByCode(workflowCode);
        if (workflowId == null) {
            log.warn("默认AI工作流不存在, workflowCode={}, params={}", workflowCode, JSONUtil.toJsonStr(params));
            return null;
        }
        log.info("准备调用默认AI工作流, workflowCode={}, workflowId={}", workflowCode, workflowId);
        return invokeAiWorkflow(workflowId, params);
    }

    private List<String> parseKeywordWorkflowResult(Object rawResult) {
        JSONObject json = parseWorkflowResultObject(rawResult);
        if (json == null) {
            log.info("AI关键词扩展解析为空, rawType={}, rawResult={}",
                    rawResult == null ? "null" : rawResult.getClass().getName(), stringifyAiWorkflowResult(rawResult));
            return Collections.emptyList();
        }
        Object keywords = json.get("keywords");
        if (!(keywords instanceof Collection) && keywords == null) {
            JSONObject nested = findFirstJsonObjectWithKey(json, "keywords");
            if (nested != null) {
                keywords = nested.get("keywords");
            }
        }
        if (keywords instanceof Collection) {
            List<String> result = ((Collection<?>) keywords).stream()
                    .map(String::valueOf)
                    .map(String::trim)
                    .filter(StrUtil::isNotBlank)
                    .distinct()
                    .collect(Collectors.toList());
            log.info("AI关键词扩展解析完成, count={}, keywords={}", result.size(), result);
            return result;
        }
        String text = json.getStr("keywords");
        if (StrUtil.isBlank(text)) {
            log.info("AI关键词扩展未找到keywords字段, rawResult={}", stringifyAiWorkflowResult(rawResult));
            return Collections.emptyList();
        }
        List<String> result = Arrays.stream(text.split("[,\\n]"))
                .map(String::trim)
                .filter(StrUtil::isNotBlank)
                .distinct()
                .collect(Collectors.toList());
        log.info("AI关键词扩展解析完成, count={}, keywords={}", result.size(), result);
        return result;
    }

    private List<String> buildFallbackKeywords(List<String> seeds, int count) {
        List<String> result = new ArrayList<>();
        List<String> suffixes = Arrays.asList("supplier", "manufacturer", "wholesaler", "distributor", "factory");
        for (String seed : seeds) {
            if (StrUtil.isBlank(seed)) {
                continue;
            }
            result.add(seed.trim());
            for (String suffix : suffixes) {
                result.add(seed.trim() + " " + suffix);
            }
        }
        return result.stream().distinct().limit(count).collect(Collectors.toList());
    }

    private void fillAgentSummary(FbAiAgentConfigDO config) {
        List<Long> taskIds = getAgentDiscoveryTaskIds(config.getId());
        if (CollUtil.isEmpty(taskIds)) {
            config.setLeadCount(0L);
            config.setPendingCount(0L);
            return;
        }
        List<FbCollectUserDO> users = collectUserMapper.selectList(new LambdaQueryWrapper<FbCollectUserDO>()
                .in(FbCollectUserDO::getTaskId, taskIds));
        long leadCount = users.stream()
                .filter(item -> item.getProductRelevanceScore() != null)
                .count();
        long pendingCount = users.stream()
                .filter(item -> item.getProductRelevanceScore() != null)
                .filter(item -> StrUtil.equalsAny(StrUtil.blankToDefault(item.getTouchStatus(), "not_touched"),
                        "not_touched", "pending"))
                .count();
        config.setLeadCount(leadCount);
        config.setPendingCount(pendingCount);
    }

    private Map<Long, LeadAnalysisResult> analyzeUsersByWorkflow(FbAiAgentConfigDO config, List<String> seedKeywords,
                                                                 List<FbCollectUserDO> users) {
        if (CollUtil.isEmpty(users)) {
            return Collections.emptyMap();
        }
        Map<Long, LeadAnalysisResult> resultMap = new HashMap<>();
        for (int fromIndex = 0; fromIndex < users.size(); fromIndex += AI_ANALYZE_BATCH_SIZE) {
            List<FbCollectUserDO> batch = users.subList(fromIndex, Math.min(fromIndex + AI_ANALYZE_BATCH_SIZE, users.size()));
            Map<String, Object> params = new LinkedHashMap<>();
            params.put("exportProduct", resolveExportProduct(config, seedKeywords));
            params.put("persona", StrUtil.blankToDefault(config.getPersonaType(), "professional_sales"));
            params.put("needComment", Boolean.TRUE.equals(config.getAutoCommentEnabled()));
            params.put("needDm", Boolean.TRUE.equals(config.getAutoDmEnabled()));
            params.put("customers", batch.stream().map(this::buildCustomerPayload).collect(Collectors.toList()));
            Object rawResult = invokeDefaultAiWorkflow(DEFAULT_LEAD_ANALYZE_WORKFLOW_CODE, params);
            Map<Long, LeadAnalysisResult> parsed = parseLeadWorkflowResults(rawResult, config.getTouchScoreThreshold());
            log.info("AI主页客户分析批次完成, agentId={}, fromIndex={}, batchSize={}, parsedCount={}",
                    config.getId(), fromIndex, batch.size(), parsed.size());
            resultMap.putAll(parsed);
        }
        return resultMap;
    }

    private Map<String, Object> buildCustomerPayload(FbCollectUserDO user) {
        Map<String, Object> item = new LinkedHashMap<>();
        item.put("id", user.getId());
        item.put("name", user.getUserName());
        item.put("category", user.getCategory());
        item.put("country", user.getCountry());
        item.put("description", user.getProfileStatus());
        item.put("recent_posts", user.getLastPostSummary());
        item.put("last_post_days", calculateLastPostDays(user.getLastPostTime()));
        return item;
    }

    private String resolveExportProduct(FbAiAgentConfigDO config, List<String> seedKeywords) {
        if (StrUtil.isNotBlank(config.getExportProduct())) {
            return config.getExportProduct().trim();
        }
        List<String> keywordPool = parseJsonStringList(config.getKeywordPool());
        if (CollUtil.isNotEmpty(keywordPool)) {
            return keywordPool.get(0);
        }
        return CollUtil.isNotEmpty(seedKeywords) ? seedKeywords.get(0) : "";
    }

    private Map<Long, LeadAnalysisResult> parseLeadWorkflowResults(Object rawResult, Integer threshold) {
        if (rawResult == null) {
            log.info("AI主页客户分析结果为空");
            return Collections.emptyMap();
        }
        List<Object> rows = new ArrayList<>();
        if (rawResult instanceof Collection<?>) {
            rows.addAll((Collection<?>) rawResult);
        } else if (rawResult instanceof CharSequence && JSONUtil.isTypeJSONArray(String.valueOf(rawResult))) {
            rows.addAll(JSONUtil.parseArray(String.valueOf(rawResult)));
        } else {
            JSONObject object = parseWorkflowResultObject(rawResult);
            if (object != null) {
                Object arrayResult = findFirstJsonArray(object);
                if (arrayResult instanceof Collection<?>) {
                    rows.addAll((Collection<?>) arrayResult);
                }
                Object customers = object.get("customers");
                if (customers instanceof Collection<?>) {
                    rows.addAll((Collection<?>) customers);
                } else if (object.containsKey("results") && object.get("results") instanceof Collection<?>) {
                    rows.addAll((Collection<?>) object.get("results"));
                }
            }
        }
        if (rows.isEmpty()) {
            log.info("AI主页客户分析未解析到数组结果, rawType={}, rawResult={}",
                    rawResult.getClass().getName(), stringifyAiWorkflowResult(rawResult));
            return Collections.emptyMap();
        }
        Map<Long, LeadAnalysisResult> resultMap = new HashMap<>();
        for (Object row : rows) {
            JSONObject json = row instanceof JSONObject ? (JSONObject) row : JSONUtil.parseObj(row);
            Long id = json.getLong("id");
            if (id == null) {
                continue;
            }
            LeadAnalysisResult result = new LeadAnalysisResult();
            String intent = normalizeIntent(json.getStr("intent"));
            int score = mapIntentToScore(intent);
            result.intentCode = intent;
            result.productRelevanceScore = score;
            result.leadType = "page_lead";
            result.intentReason = limitReason(StrUtil.blankToDefault(json.getStr("reason"), "AI已完成意向判断"));
            result.intentLevel = buildIntentLevelByIntent(intent);
            result.sentiment = "neutral";
            result.aiTags = buildTagsByIntent(intent, result.leadType, result.intentReason);
            result.touchStatus = "not_touched";
            boolean touchable = isIntentReachThreshold(intent, threshold);
            result.commentMessage = touchable ? json.getStr("comment_message") : null;
            result.dmMessage = touchable ? json.getStr("dm_message") : null;
            result.aiSummary = buildAiSummary(result, touchable);
            resultMap.put(id, result);
        }
        log.info("AI主页客户分析解析完成, rawRows={}, validRows={}, ids={}",
                rows.size(), resultMap.size(), resultMap.keySet());
        return resultMap;
    }

    private LeadAnalysisResult buildAiMissingResult() {
        LeadAnalysisResult result = new LeadAnalysisResult();
        result.intentCode = "D";
        result.productRelevanceScore = mapIntentToScore(result.intentCode);
        result.intentLevel = buildIntentLevelByIntent(result.intentCode);
        result.leadType = "page_lead";
        result.intentReason = "AI分析失败";
        result.sentiment = "neutral";
        result.aiTags = buildTagsByIntent(result.intentCode, result.leadType, result.intentReason);
        result.touchStatus = "not_touched";
        result.aiSummary = buildAiSummary(result, false);
        return result;
    }

    private JSONObject findFirstJsonObjectWithKey(JSONObject object, String key) {
        if (object == null) {
            return null;
        }
        if (object.containsKey(key)) {
            return object;
        }
        for (String itemKey : object.keySet()) {
            Object value = object.get(itemKey);
            JSONObject found = null;
            if (value instanceof JSONObject) {
                found = findFirstJsonObjectWithKey((JSONObject) value, key);
            } else if (value instanceof Map) {
                found = findFirstJsonObjectWithKey(JSONUtil.parseObj(value), key);
            } else if (value instanceof CharSequence && JSONUtil.isTypeJSON(String.valueOf(value))) {
                found = findFirstJsonObjectWithKey(JSONUtil.parseObj(String.valueOf(value)), key);
            }
            if (found != null) {
                return found;
            }
        }
        return null;
    }

    private Object findFirstJsonArray(JSONObject object) {
        if (object == null) {
            return null;
        }
        for (String itemKey : object.keySet()) {
            Object value = object.get(itemKey);
            Object found = null;
            if (value instanceof Collection<?>) {
                return value;
            }
            if (value instanceof CharSequence && JSONUtil.isTypeJSONArray(String.valueOf(value))) {
                return JSONUtil.parseArray(String.valueOf(value));
            }
            if (value instanceof JSONObject) {
                found = findFirstJsonArray((JSONObject) value);
            } else if (value instanceof Map) {
                found = findFirstJsonArray(JSONUtil.parseObj(value));
            } else if (value instanceof CharSequence && JSONUtil.isTypeJSON(String.valueOf(value))) {
                found = findFirstJsonArray(JSONUtil.parseObj(String.valueOf(value)));
            }
            if (found != null) {
                return found;
            }
        }
        return null;
    }

    private Integer calculateLastPostDays(LocalDateTime lastPostTime) {
        if (lastPostTime == null) {
            return null;
        }
        return (int) Math.max(Duration.between(lastPostTime, LocalDateTime.now()).toDays(), 0);
    }

    private String buildIntentLevelByIntent(String intent) {
        if ("A".equals(intent)) {
            return "high";
        }
        if ("B".equals(intent) || "C".equals(intent)) {
            return "medium";
        }
        return "low";
    }

    private String buildTagsByIntent(String intent, String customerType, String reason) {
        List<String> tags = new ArrayList<>();
        if ("A".equals(intent)) {
            tags.add("高意向询价");
        } else if ("B".equals(intent)) {
            tags.add("寻找供应商");
        } else if ("C".equals(intent)) {
            tags.add("待人工确认");
        } else {
            tags.add("普通消费者");
        }
        if (StrUtil.containsIgnoreCase(StrUtil.nullToEmpty(customerType), "Distributor")) {
            tags.add("潜在经销商");
        }
        if (StrUtil.containsIgnoreCase(StrUtil.nullToEmpty(reason), "supplier")) {
            tags.add("寻找供应商");
        }
        return tags.stream().distinct().collect(Collectors.joining(","));
    }

    private String buildAiSummary(LeadAnalysisResult result, boolean touchable) {
        JSONObject json = JSONUtil.createObj()
                .set("intent", result.intentCode)
                .set("score", result.productRelevanceScore)
                .set("reason", result.intentReason);
        if (touchable && StrUtil.isNotBlank(result.commentMessage)) {
            json.set("comment_message", result.commentMessage);
        }
        if (touchable && StrUtil.isNotBlank(result.dmMessage)) {
            json.set("dm_message", result.dmMessage);
        }
        return json.toString();
    }

    private String normalizeIntent(String intent) {
        if (StrUtil.isBlank(intent)) {
            return "D";
        }
        String value = intent.trim().toUpperCase(Locale.ROOT);
        if (StrUtil.startWithAny(value, "A", "B", "C", "D")) {
            return value.substring(0, 1);
        }
        return "D";
    }

    private String limitReason(String reason) {
        return StrUtil.maxLength(StrUtil.blankToDefault(reason, ""), 20);
    }

    private int mapIntentToScore(String intent) {
        switch (normalizeIntent(intent)) {
            case "A":
                return 95;
            case "B":
                return 85;
            case "C":
                return 70;
            default:
                return 50;
        }
    }

    private String mapScoreToIntent(Integer score) {
        int value = Optional.ofNullable(score).orElse(0);
        if (value >= 95) {
            return "A";
        }
        if (value >= 80) {
            return "B";
        }
        if (value >= 60) {
            return "C";
        }
        return "D";
    }

    private String resolveTouchIntentLevel(Integer touchScoreThreshold) {
        return mapScoreToIntent(Optional.ofNullable(touchScoreThreshold).orElse(95));
    }

    private boolean isIntentReachThreshold(String intent, Integer touchScoreThreshold) {
        return mapIntentToScore(intent) >= mapIntentToScore(resolveTouchIntentLevel(touchScoreThreshold));
    }

    private List<Long> getAgentDiscoveryTaskIds(Long agentConfigId) {
        return discoveryLogMapper.selectList(new LambdaQueryWrapper<FbAiAgentDiscoveryLogDO>()
                        .eq(FbAiAgentDiscoveryLogDO::getAgentConfigId, agentConfigId)
                        .select(FbAiAgentDiscoveryLogDO::getCollectTaskId))
                .stream()
                .map(FbAiAgentDiscoveryLogDO::getCollectTaskId)
                .filter(Objects::nonNull)
                .distinct()
                .collect(Collectors.toList());
    }

    private FbAiAgentConfigDO findAgentConfigByCollectTaskId(Long collectTaskId) {
        FbAiAgentDiscoveryLogDO logDO = discoveryLogMapper.selectOne(new LambdaQueryWrapper<FbAiAgentDiscoveryLogDO>()
                .eq(FbAiAgentDiscoveryLogDO::getCollectTaskId, collectTaskId)
                .last("LIMIT 1"));
        if (logDO == null || logDO.getAgentConfigId() == null) {
            return null;
        }
        return agentConfigMapper.selectById(logDO.getAgentConfigId());
    }

    private void refreshDiscoveryStats(Long agentConfigId) {
        List<FbAiAgentDiscoveryLogDO> logs = discoveryLogMapper.selectList(new LambdaQueryWrapper<FbAiAgentDiscoveryLogDO>()
                .eq(FbAiAgentDiscoveryLogDO::getAgentConfigId, agentConfigId));
        if (CollUtil.isEmpty(logs)) {
            return;
        }
        for (FbAiAgentDiscoveryLogDO logDO : logs) {
            if (logDO.getCollectTaskId() == null) {
                continue;
            }
            List<FbCollectUserDO> users = collectUserMapper.selectList(new LambdaQueryWrapper<FbCollectUserDO>()
                    .eq(FbCollectUserDO::getTaskId, logDO.getCollectTaskId()));
            FbAiAgentDiscoveryLogDO updateObj = new FbAiAgentDiscoveryLogDO();
            updateObj.setId(logDO.getId());
            updateObj.setDiscoveredCount(users.size());
            updateObj.setPageCollectCount(users.size());
            long analyzed = users.stream().filter(item -> item.getLastAiAnalyzeTime() != null).count();
            long highIntent = users.stream().filter(item -> StrUtil.equals(item.getIntentLevel(), "high")).count();
            updateObj.setAiAnalyzeCount((int) analyzed);
            updateObj.setHighIntentCount((int) highIntent);
            updateObj.setFilteredCount((int) Math.max(users.size() - highIntent, 0));
            updateObj.setFinalLeadCount((int) highIntent);
            discoveryLogMapper.updateById(updateObj);
        }
    }

    private boolean isCommentablePostUrl(String url) {
        if (StrUtil.isBlank(url)) {
            return false;
        }
        return StrUtil.containsAnyIgnoreCase(url, "/posts/", "/permalink/", "permalink.php", "multi_permalinks=");
    }

    private Long resolveWorkflowIdByCode(String workflowCode) {
        if (StrUtil.isBlank(workflowCode)) {
            return null;
        }
        try {
            AiWorkflowDO workflow = aiWorkflowMapper.selectByCode(workflowCode);
            if (workflow == null) {
                return null;
            }
            return workflow.getId();
        } catch (Exception ex) {
            log.warn("根据 code 读取 AI 工作流失败，code={}, reason={}", workflowCode, ex.getMessage());
            return null;
        }
    }

    private void addRunLog(Long agentConfigId, String title, String content, String logLevel) {
        if (agentConfigId == null) {
            return;
        }
        FbAiAgentRunLogDO logDO = new FbAiAgentRunLogDO();
        logDO.setAgentConfigId(agentConfigId);
        logDO.setTitle(title);
        logDO.setContent(content);
        logDO.setLogLevel(logLevel);
        runLogMapper.insert(logDO);
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

    private void normalizeDefaults(FbAiAgentConfigSaveReqVO reqVO) {
        if (reqVO.getStatus() == null) {
            reqVO.setStatus(0);
        }
        if (StrUtil.isBlank(reqVO.getAgentType())) {
            reqVO.setAgentType(AGENT_TYPE_PAGE_LEAD);
        }
        if (StrUtil.isBlank(reqVO.getSearchMode())) {
            reqVO.setSearchMode("keyword");
        }
        if (reqVO.getKeywordCursor() == null) {
            reqVO.setKeywordCursor(0);
        }
        if (reqVO.getKeywordsPerRun() == null) {
            reqVO.setKeywordsPerRun(5);
        }
        if (reqVO.getAiKeywordExpandEnabled() == null) {
            reqVO.setAiKeywordExpandEnabled(false);
        }
        if (reqVO.getAiKeywordExpandCount() == null) {
            reqVO.setAiKeywordExpandCount(20);
        }
        if (reqVO.getTargetCustomerCount() == null) {
            reqVO.setTargetCustomerCount(1000);
        }
        if (StrUtil.isBlank(reqVO.getExecuteFrequency())) {
            reqVO.setExecuteFrequency("daily");
        }
        if (StrUtil.isBlank(reqVO.getExecuteTime())) {
            reqVO.setExecuteTime("09:00");
        }
        if (reqVO.getTouchScoreThreshold() == null) {
            reqVO.setTouchScoreThreshold(90);
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
        if (StrUtil.isBlank(reqVO.getPersonaType())) {
            reqVO.setPersonaType("professional_sales");
        }
        if (StrUtil.isBlank(reqVO.getKeywordPool())) {
            reqVO.setKeywordPool(reqVO.getSeedKeywords());
        }
        if (StrUtil.isBlank(reqVO.getExportProduct())) {
            List<String> keywordPool = parseJsonStringList(reqVO.getKeywordPool());
            if (CollUtil.isNotEmpty(keywordPool)) {
                reqVO.setExportProduct(keywordPool.get(0));
            }
        }
    }

    private void validateConfig(FbAiAgentConfigSaveReqVO reqVO) {
        if (!Integer.valueOf(1).equals(reqVO.getStatus())) {
            return;
        }
        if (!AGENT_TYPE_PAGE_LEAD.equals(reqVO.getAgentType())) {
            throw exception0(2_011_000_001, "当前版本仅支持AI主页获客");
        }
        if (StrUtil.isBlank(reqVO.getSeedKeywords()) && StrUtil.isBlank(reqVO.getKeywordPool())) {
            throw exception0(2_011_000_002, "启用Agent前请配置关键词");
        }
        if (StrUtil.isBlank(reqVO.getAccountIds())) {
            throw exception0(2_011_000_003, "启用Agent前请选择执行账号池");
        }
        List<String> keywordPool = parseJsonStringList(reqVO.getKeywordPool());
        if (CollUtil.isEmpty(keywordPool)) {
            throw exception0(2_011_000_004, "关键词池不能为空");
        }
        if (StrUtil.isBlank(reqVO.getExportProduct())) {
            throw exception0(2_011_000_006, "启用Agent前请配置主营/出口产品");
        }
        if (!isValidExecuteTime(reqVO.getExecuteTime())) {
            throw exception0(2_011_000_007, "执行时间格式不正确，请使用 HH:mm");
        }
        if (reqVO.getKeywordsPerRun() != null && reqVO.getKeywordsPerRun() > keywordPool.size()) {
            throw exception0(2_011_000_005, "每轮执行关键词数量不能大于关键词池总数");
        }
    }

    private boolean isValidExecuteTime(String executeTime) {
        try {
            LocalTime.parse(executeTime);
            return true;
        } catch (Exception ex) {
            return false;
        }
    }

    private static class AgentDispatchStats {
        private int executedAgents;
        private int createdCollectTasks;
        private int createdDeepCollectTasks;
        private int analyzedPosts;
        private int analyzedUsers;
        private int queuedTouches;
        private int activatedTouches;
    }

    private static class LeadAnalysisResult {
        private String aiTags;
        private String intentCode;
        private String intentLevel;
        private String intentReason;
        private String sentiment;
        private String leadType;
        private String country;
        private String language;
        private Integer productRelevanceScore;
        private String aiSummary;
        private String touchStatus;
        private String commentMessage;
        private String dmMessage;
    }

}
