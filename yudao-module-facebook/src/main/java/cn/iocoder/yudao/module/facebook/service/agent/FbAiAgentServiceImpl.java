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
import cn.iocoder.yudao.module.facebook.controller.admin.operation.vo.FbOperationTaskSaveReqVO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.account.FbAccountDO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.agent.FbAiAgentConfigDO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.agent.FbAiAgentDiscoveryLogDO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.agent.FbAiAgentRunLogDO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.agent.FbAiTouchRecordDO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.collect.FbCollectDO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.collectdetail.FbCollectDetailDO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.collectuser.FbCollectUserDO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.dmtask.FbDmTaskDO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.dmtask.FbDmTaskDetailDO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.fbcollectpost.FbCollectPostDO;
import cn.iocoder.yudao.module.facebook.dal.mysql.account.FbAccountMapper;
import cn.iocoder.yudao.module.facebook.dal.mysql.agent.FbAiAgentConfigMapper;
import cn.iocoder.yudao.module.facebook.dal.mysql.agent.FbAiAgentDiscoveryLogMapper;
import cn.iocoder.yudao.module.facebook.dal.mysql.agent.FbAiAgentRunLogMapper;
import cn.iocoder.yudao.module.facebook.dal.mysql.agent.FbAiTouchRecordMapper;
import cn.iocoder.yudao.module.facebook.dal.mysql.collect.FbCollectMapper;
import cn.iocoder.yudao.module.facebook.dal.mysql.collectdetail.FbCollectDetailMapper;
import cn.iocoder.yudao.module.facebook.dal.mysql.collectuser.FbCollectUserMapper;
import cn.iocoder.yudao.module.facebook.dal.mysql.dmtask.FbDmTaskDetailMapper;
import cn.iocoder.yudao.module.facebook.dal.mysql.dmtask.FbDmTaskMapper;
import cn.iocoder.yudao.module.facebook.dal.mysql.fbcollectpost.FbCollectPostMapper;
import cn.iocoder.yudao.module.facebook.service.operation.FbOperationTaskService;
import cn.iocoder.yudao.module.facebook.service.account.FbAccountTaskAllocationService;
import com.baomidou.mybatisplus.core.conditions.query.LambdaQueryWrapper;
import jakarta.annotation.Resource;
import lombok.AllArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;
import org.springframework.validation.annotation.Validated;

import java.net.URLEncoder;
import java.net.URI;
import java.nio.charset.StandardCharsets;
import java.time.Duration;
import java.time.LocalDateTime;
import java.time.LocalTime;
import java.util.*;
import java.util.concurrent.ThreadLocalRandom;
import java.util.function.Function;
import java.util.stream.Collectors;
import java.util.regex.Matcher;
import java.util.regex.Pattern;

import static cn.iocoder.yudao.framework.common.exception.util.ServiceExceptionUtil.exception0;

/**
 * Facebook AI获客Agent Service 实现类
 */
@Slf4j
@Service
@Validated
public class FbAiAgentServiceImpl implements FbAiAgentService {

    /**
     * 服务本次启动时间。用于区分“服务启动前已经错过的计划”与“服务运行期间等待到达的计划”。
     * 例如服务 10:00 启动、Agent 计划 09:00 时，今天直接跳过，不补执行昨天/今天的过期计划。
     */
    private final LocalDateTime serviceStartTime = LocalDateTime.now();

    private static final String AUTO_COLLECT_REMARK = "AI_AGENT_AUTO_COLLECT";
    private static final String AUTO_GROUP_COLLECT_REMARK = "AI_AGENT_GROUP_MONITOR";
    private static final String AGENT_TYPE_PAGE_LEAD = "page_lead";
    private static final String AGENT_TYPE_POST_LEAD = "post_lead";
    private static final String AGENT_TYPE_GROUP_POST = "group_post";
    private static final String AGENT_TYPE_GROUP_COMMENT = "group_comment";
    private static final String AGENT_TYPE_COMPETITOR_BUYER = "competitor_buyer";
    private static final int PAGE_COLLECT_TASK_TYPE = 1;
    private static final int POST_COLLECT_TASK_TYPE = 2;
    private static final int COMMENT_LIKE_COLLECT_TASK_TYPE = 11;
    private static final int DEEP_COLLECT_TASK_TYPE = 12;
    private static final int DEFAULT_COLLECT_EXPECTED_COUNT = 20;
    private static final int GROUP_POST_COLLECT_SAFETY_LIMIT = 1000;
    private static final int MAX_ANALYZE_PER_RUN = 50;
    private static final int AI_ANALYZE_BATCH_SIZE = 50;
    private static final int MAX_TOUCH_QUEUE_PER_RUN = 20;
    private static final Pattern COLLECTION_SAVE_SUMMARY_PATTERN = Pattern.compile(
            "本轮采集：接收\\s*(\\d+)\\s*条，新增保存\\s*(\\d+)\\s*条，重复跳过\\s*(\\d+)\\s*条");
    private static final int POST_COMMENT_TASK_TYPE = 15;
    private static final int COLLECT_RUNNING_TIMEOUT_MINUTES = 3;
    private static final String DEFAULT_KEYWORD_WORKFLOW_CODE = "fb_ai_keyword_expand_v1";
    private static final String DEFAULT_LEAD_ANALYZE_WORKFLOW_CODE = "fb_ai_page_lead_scoring_v1";
    private static final String DEFAULT_GROUP_POST_ANALYZE_WORKFLOW_CODE = "fb_ai_group_post_analyze_v1";
    private static final String DEFAULT_POST_LEAD_ANALYZE_WORKFLOW_CODE = "fb_ai_post_lead_analyze_v1";
    private static final String RECENT_POSTS_FILTER = "eyJyZWNlbnRfcG9zdHM6MCI6IntcIm5hbWVcIjpcInJlY2VudF9wb3N0c1wiLFwiYXJnc1wiOlwiXCJ9In0%3D";
    private static final String DEFAULT_GROUP_COMMENT_POST_FILTER_WORKFLOW_CODE = "fb_ai_group_comment_post_filter_v1";
    private static final String DEFAULT_GROUP_COMMENT_ANALYZE_WORKFLOW_CODE = "fb_ai_group_comment_analyze_v1";
    private static final String DEFAULT_COMPETITOR_COMMENT_ANALYZE_WORKFLOW_CODE = "fb_ai_competitor_comment_analyze_v1";

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
    private FbDmTaskMapper dmTaskMapper;
    @Resource
    private FbDmTaskDetailMapper dmTaskDetailMapper;
    @Resource
    private AiWorkflowService aiWorkflowService;
    @Resource
    private AiWorkflowMapper aiWorkflowMapper;
    @Resource
    private FbAiAgentCollectQueueService collectQueueService;
    @Resource
    private FbAccountTaskAllocationService accountAllocationService;

    @Override
    @Transactional(rollbackFor = Exception.class)
    public Long saveConfig(FbAiAgentConfigSaveReqVO saveReqVO) {
        normalizeDefaults(saveReqVO);
        validateConfig(saveReqVO);

        FbAiAgentConfigDO config = BeanUtils.toBean(saveReqVO, FbAiAgentConfigDO.class);
        if (config.getId() == null) {
            agentConfigMapper.insert(config);
            addRunLog(config.getId(), "Agent创建完成", "已创建" + getAgentTypeLabel(config.getAgentType()) + "：" + config.getAgentName(), "success");
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
        FbAiAgentConfigDO config = agentConfigMapper.selectById(pageReqVO.getAgentConfigId());
        if (config != null && (AGENT_TYPE_GROUP_POST.equals(config.getAgentType())
                || AGENT_TYPE_POST_LEAD.equals(config.getAgentType()))) {
            List<Long> postIds = getAgentPostLeadIds(pageReqVO.getAgentConfigId());
            if (CollUtil.isEmpty(postIds)) {
                return new PageResult(Collections.emptyList(), 0L);
            }
            List<FbCollectPostDO> records = collectPostMapper.selectList(new LambdaQueryWrapper<FbCollectPostDO>()
                    .in(FbCollectPostDO::getId, postIds)
                    .orderByDesc(FbCollectPostDO::getCreateTime)
                    .orderByDesc(FbCollectPostDO::getId));
            int pageNo = Math.max(pageReqVO.getPageNo(), 1);
            int pageSize = Math.max(pageReqVO.getPageSize(), 10);
            int fromIndex = Math.min((pageNo - 1) * pageSize, records.size());
            int toIndex = Math.min(fromIndex + pageSize, records.size());
            return new PageResult(records.subList(fromIndex, toIndex), (long) records.size());
        }
        List<Long> leadIds = getAgentLeadIds(pageReqVO.getAgentConfigId());
        if (CollUtil.isEmpty(leadIds)) {
            return new PageResult<>(Collections.emptyList(), 0L);
        }
        List<FbCollectUserDO> records = collectUserMapper.selectList(new LambdaQueryWrapper<FbCollectUserDO>()
                .in(FbCollectUserDO::getId, leadIds)
                .orderByDesc(FbCollectUserDO::getCreateTime)
                .orderByDesc(FbCollectUserDO::getId));
        if (config != null && (AGENT_TYPE_GROUP_COMMENT.equals(config.getAgentType())
                || AGENT_TYPE_COMPETITOR_BUYER.equals(config.getAgentType()))) {
            enrichCommentLeadPostFields(records);
        }
        int pageNo = Math.max(pageReqVO.getPageNo(), 1);
        int pageSize = Math.max(pageReqVO.getPageSize(), 10);
        int fromIndex = Math.min((pageNo - 1) * pageSize, records.size());
        int toIndex = Math.min(fromIndex + pageSize, records.size());
        return new PageResult<>(records.subList(fromIndex, toIndex), (long) records.size());
    }

    private void enrichCommentLeadPostFields(List<FbCollectUserDO> records) {
        List<Long> postIds = records.stream()
                .map(FbCollectUserDO::getSourcePostId)
                .filter(Objects::nonNull)
                .distinct()
                .collect(Collectors.toList());
        if (CollUtil.isEmpty(postIds)) {
            return;
        }
        Map<Long, FbCollectPostDO> postMap = collectPostMapper.selectBatchIds(postIds).stream()
                .collect(Collectors.toMap(FbCollectPostDO::getId, Function.identity(), (left, right) -> left));
        records.forEach(record -> {
            FbCollectPostDO post = postMap.get(record.getSourcePostId());
            if (post == null) {
                return;
            }
            record.setPostContent(post.getPostContent());
            record.setPostCreateTime(post.getPostCreateTime());
            if (StrUtil.isBlank(record.getSourcePostUrl())) {
                record.setSourcePostUrl(post.getUrl());
            }
        });
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
        return dispatchInternal(false, true);
    }

    @Override
    @Transactional(rollbackFor = Exception.class)
    public FbAiAgentDispatchRespVO executeNow(List<Long> ids) {
        if (CollUtil.isEmpty(ids)) {
            return new FbAiAgentDispatchRespVO(false, "请选择要执行的Agent");
        }
        List<Long> agentIds = ids.stream().filter(Objects::nonNull).distinct().collect(Collectors.toList());
        if (CollUtil.isEmpty(agentIds)) {
            return new FbAiAgentDispatchRespVO(false, "请选择要执行的Agent");
        }

        List<FbAiAgentConfigDO> configs = agentConfigMapper.selectList(new LambdaQueryWrapper<FbAiAgentConfigDO>()
                .in(FbAiAgentConfigDO::getId, agentIds)
                .orderByAsc(FbAiAgentConfigDO::getId));
        if (CollUtil.isEmpty(configs)) {
            return new FbAiAgentDispatchRespVO(false, "未找到可执行的Agent");
        }

        AgentDispatchStats stats = new AgentDispatchStats();
        List<FbAiAgentDispatchRespVO.CollectDetail> launchDetails = new ArrayList<>();
        List<String> skippedReasons = new ArrayList<>();

        for (FbAiAgentConfigDO config : configs) {
            if (!Objects.equals(config.getStatus(), 1)) {
                skippedReasons.add(config.getAgentName() + "：不是运行中状态");
                continue;
            }
            if (!isSupportedAgentType(config.getAgentType())) {
                skippedReasons.add(config.getAgentName() + "：暂不支持该Agent类型");
                continue;
            }

            List<String> accountIds = resolveAgentAccountIds(config, resolveTargetCustomerCount(config));
            if (CollUtil.isEmpty(accountIds)) {
                skippedReasons.add(config.getAgentName() + "：账号池为空");
                continue;
            }
            int created;
            if (AGENT_TYPE_COMPETITOR_BUYER.equals(config.getAgentType())) {
                List<String> pageUrls = resolveCompetitorPageUrls(config);
                if (CollUtil.isEmpty(pageUrls)) {
                    skippedReasons.add(config.getAgentName() + "：竞品主页为空");
                    continue;
                }
                addRunLog(config.getId(), "立即执行",
                        String.format("竞品主页%s个，采集最近%s天", pageUrls.size(), resolveCompetitorRecentDays(config)), "info");
                created = createCompetitorPostCollectTasks(config, pageUrls, accountIds, launchDetails, true);
            } else if (isGroupMonitorAgent(config.getAgentType())) {
                List<String> groupUrls = resolveGroupPostUrls(config);
                if (CollUtil.isEmpty(groupUrls)) {
                    skippedReasons.add(config.getAgentName() + "：监控群组为空");
                    continue;
                }
                addRunLog(config.getId(), "立即执行",
                        String.format("群组%s个，采集最近%s天", groupUrls.size(), resolveGroupPostRecentDays(config)), "info");
                created = AGENT_TYPE_GROUP_COMMENT.equals(config.getAgentType())
                        ? createGroupCommentPostCollectTasks(config, groupUrls, accountIds, launchDetails, true, true)
                        : createGroupPostCollectTasks(config, groupUrls, accountIds, launchDetails, true, true);
            } else if (AGENT_TYPE_POST_LEAD.equals(config.getAgentType())) {
                List<String> runKeywords = pickRunKeywords(config);
                if (CollUtil.isEmpty(runKeywords)) {
                    skippedReasons.add(config.getAgentName() + "：关键词为空");
                    continue;
                }
                addRunLog(config.getId(), "立即执行",
                        String.format("帖子关键词%s个，目标%s个", runKeywords.size(), resolveTargetCustomerCount(config)), "info");
                created = createPostLeadCollectTasks(config, runKeywords, accountIds, launchDetails, true);
                advanceKeywordCursor(config, runKeywords.size());
            } else {
                List<String> runKeywords = pickRunKeywords(config);
                if (CollUtil.isEmpty(runKeywords)) {
                    skippedReasons.add(config.getAgentName() + "：关键词为空");
                    continue;
                }
                addRunLog(config.getId(), "立即执行",
                        String.format("关键词%s个，目标%s个", runKeywords.size(), resolveTargetCustomerCount(config)), "info");
                created = createPageLeadCollectTasks(config, runKeywords, accountIds, launchDetails, true);
                advanceKeywordCursor(config, runKeywords.size());
            }
            if (created <= 0) {
                skippedReasons.add(config.getAgentName() + "：未创建采集任务");
                continue;
            }

            stats.executedAgents++;
            stats.createdCollectTasks += created;
            markAgentExecuted(config.getId());
        }

        if (stats.executedAgents == 0) {
            String message = CollUtil.isEmpty(skippedReasons)
                    ? "暂无可立即执行的Agent"
                    : "暂无可立即执行的Agent，原因：" + String.join("；", skippedReasons);
            return new FbAiAgentDispatchRespVO(false, message);
        }

        String message = String.format("立即执行完成：Agent%s个，新建采集%s个%s",
                stats.executedAgents, stats.createdCollectTasks,
                CollUtil.isEmpty(skippedReasons) ? "" : "，跳过：" + String.join("；", skippedReasons));
        FbAiAgentDispatchRespVO respVO = new FbAiAgentDispatchRespVO(true, message);
        respVO.setDetails(launchDetails);
        return respVO;
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
        List<String> accountIds = resolveAgentAccountIds(config, resolveTargetCustomerCount(config));
        List<String> targetCountries = parseJsonStringList(config.getTargetCountries());
        List<String> targetLanguages = parseJsonStringList(config.getTargetLanguages());
        List<String> keywords = parseJsonStringList(config.getKeywordPool());
        if (CollUtil.isEmpty(keywords)) {
            keywords = parseJsonStringList(config.getSeedKeywords());
        }

        if (Objects.equals(task.getTaskType(), PAGE_COLLECT_TASK_TYPE)) {
            List<FbAiAgentDispatchRespVO.CollectDetail> ignored = new ArrayList<>();
            int deepCreated = createDeepCollectTasks(config, accountIds, ignored, true);
            int discoveredCount = getCollectTaskLeadIds(collectTaskId).size();
            refreshDiscoveryStats(config.getId());
            addRunLog(config.getId(), "主页采集完成",
                    String.format("发现%s个，创建深度采集%s个", discoveredCount, deepCreated),
                    deepCreated > 0 ? "success" : "info");
            return;
        }

        if (Objects.equals(task.getTaskType(), DEEP_COLLECT_TASK_TYPE)) {
            List<Long> currentLeadIds = getCollectTaskLeadIds(collectTaskId);
            addRunLog(config.getId(), "深度采集完成",
                    String.format("完成%s个，进入AI分析", currentLeadIds.size()), "success");
            int analyzedUsers = analyzePendingUsers(config, keywords, targetCountries, targetLanguages, currentLeadIds);
            int threshold = resolveTouchScoreThreshold(config);
            long qualifiedUsers = countQualifiedLeads(currentLeadIds, threshold);
            long missingTargetUsers = countQualifiedMissingTargetUsers(currentLeadIds, threshold);
            long existingTargetTouches = countQualifiedExistingTargetTouches(currentLeadIds, threshold);
            addRunLog(config.getId(), "AI分析完成",
                    String.format("分析%s个，达标%s个，触达阈值%s", analyzedUsers, qualifiedUsers,
                            formatUserAnalysisThreshold(currentLeadIds, threshold)), "success");
            int queuedTouches = queueHighIntentTouches(config, accountIds, currentLeadIds);
            TouchActivateResult activatedTouches = activateDueTouchRecords(config, currentLeadIds);
            refreshDiscoveryStats(config.getId());
            addTouchSummaryLog(config.getId(), queuedTouches, activatedTouches, qualifiedUsers, missingTargetUsers, existingTargetTouches);
            return;
        }

        if (Objects.equals(task.getTaskType(), POST_COLLECT_TASK_TYPE) && AGENT_TYPE_GROUP_POST.equals(config.getAgentType())) {
            List<Long> currentPostIds = getCollectTaskPostIds(collectTaskId);
            CollectionSaveSummary saveSummary = getCollectionSaveSummary(collectTaskId, currentPostIds.size());
            addRunLog(config.getId(), "群帖采集完成",
                    saveSummary.toLogContent("进入AI分析"),
                    saveSummary.duplicateCount > 0 ? "warning" : "success");
            int analyzedPosts = analyzePendingPosts(config, currentPostIds);
            int threshold = resolveTouchScoreThreshold(config);
            long qualifiedPosts = countQualifiedPostLeads(currentPostIds, threshold);
            addRunLog(config.getId(), "AI分析完成",
                    String.format("分析%s条，达标%s条，触达阈值%s", analyzedPosts, qualifiedPosts,
                            formatPostAnalysisThreshold(currentPostIds, threshold)), "success");
            int queuedTouches = queuePostHighIntentTouches(config, accountIds, currentPostIds);
            TouchActivateResult activatedTouches = activateDueTouchRecords(config, currentPostIds);
            refreshDiscoveryStats(config.getId());
            addRunLog(config.getId(), "触达完成",
                    String.format("评论%s条，私信%s条", activatedTouches.commentDetailCount, activatedTouches.dmDetailCount),
                    activatedTouches.failed > 0 ? "warning" : "success");
            return;
        }

        if (Objects.equals(task.getTaskType(), POST_COLLECT_TASK_TYPE) && AGENT_TYPE_POST_LEAD.equals(config.getAgentType())) {
            List<Long> currentPostIds = getCollectTaskPostIds(collectTaskId);
            CollectionSaveSummary saveSummary = getCollectionSaveSummary(collectTaskId, currentPostIds.size());
            int analyzedPosts = analyzePendingPosts(config, currentPostIds);
            int threshold = resolveTouchScoreThreshold(config);
            long qualifiedPosts = countQualifiedPostLeads(currentPostIds, threshold);
            addRunLog(config.getId(), "帖子采集完成",
                    saveSummary.toLogContent("进入AI分析"),
                    saveSummary.duplicateCount > 0 ? "warning" : "success");
            addRunLog(config.getId(), "AI分析完成",
                    String.format("分析%s条，达标%s条，触达阈值%s", analyzedPosts, qualifiedPosts,
                            formatPostAnalysisThreshold(currentPostIds, threshold)), "success");
            queuePostHighIntentTouches(config, accountIds, currentPostIds);
            TouchActivateResult activatedTouches = activateDueTouchRecords(config, currentPostIds);
            refreshDiscoveryStats(config.getId());
            addRunLog(config.getId(), "触达完成",
                    String.format("评论%s条，私信%s条", activatedTouches.commentDetailCount, activatedTouches.dmDetailCount),
                    activatedTouches.failed > 0 ? "warning" : "success");
            return;
        }

        if (Objects.equals(task.getTaskType(), POST_COLLECT_TASK_TYPE) && AGENT_TYPE_GROUP_COMMENT.equals(config.getAgentType())) {
            List<Long> currentPostIds = getCollectTaskPostIds(collectTaskId);
            CollectionSaveSummary saveSummary = getCollectionSaveSummary(collectTaskId, currentPostIds.size());
            int analyzedPosts = analyzeGroupCommentPosts(config, currentPostIds);
            int threshold = resolveTouchScoreThreshold(config);
            long qualifiedPosts = countQualifiedPostLeads(currentPostIds, threshold);
            int commentTasks = createGroupCommentCollectTasks(config, accountIds, currentPostIds, true);
            refreshDiscoveryStats(config.getId());
            addRunLog(config.getId(), "群帖采集完成",
                    saveSummary.toLogContent(String.format("分析%s条，广告帖%s条，创建评论采集%s个", analyzedPosts, qualifiedPosts, commentTasks)),
                    commentTasks > 0 ? "success" : "info");
            return;
        }

        if (Objects.equals(task.getTaskType(), POST_COLLECT_TASK_TYPE) && AGENT_TYPE_COMPETITOR_BUYER.equals(config.getAgentType())) {
            List<Long> currentPostIds = getCollectTaskPostIds(collectTaskId);
            CollectionSaveSummary saveSummary = getCollectionSaveSummary(collectTaskId, currentPostIds.size());
            int commentTasks = createCompetitorCommentCollectTasks(config, accountIds, currentPostIds, true);
            refreshDiscoveryStats(config.getId());
            addRunLog(config.getId(), "主页帖子采集完成",
                    saveSummary.toLogContent(String.format("创建评论采集%s个", commentTasks)),
                    commentTasks > 0 ? "success" : "info");
            return;
        }

        if (Objects.equals(task.getTaskType(), COMMENT_LIKE_COLLECT_TASK_TYPE) && AGENT_TYPE_GROUP_COMMENT.equals(config.getAgentType())) {
            List<Long> currentCommentLeadIds = getCollectTaskLeadIds(collectTaskId);
            addRunLog(config.getId(), "评论采集完成",
                    String.format("采集评论%s条，进入AI分析", currentCommentLeadIds.size()), "success");
            int analyzedUsers = analyzePendingCommentUsers(config, currentCommentLeadIds);
            int threshold = resolveTouchScoreThreshold(config);
            long qualifiedUsers = countQualifiedLeads(currentCommentLeadIds, threshold);
            long missingTargetUsers = countQualifiedMissingTargetUsers(currentCommentLeadIds, threshold);
            long existingTargetTouches = countQualifiedExistingTargetTouches(currentCommentLeadIds, threshold);
            addRunLog(config.getId(), "AI分析完成",
                    String.format("分析%s条，达标%s条，触达阈值%s", analyzedUsers, qualifiedUsers,
                            formatUserAnalysisThreshold(currentCommentLeadIds, threshold)), "success");
            int queuedTouches = queueCommentLeadDmTouches(config, accountIds, currentCommentLeadIds);
            TouchActivateResult activatedTouches = activateDueTouchRecords(config, currentCommentLeadIds);
            refreshDiscoveryStats(config.getId());
            addTouchSummaryLog(config.getId(), queuedTouches, activatedTouches, qualifiedUsers, missingTargetUsers, existingTargetTouches);
            return;
        }

        if (Objects.equals(task.getTaskType(), COMMENT_LIKE_COLLECT_TASK_TYPE) && AGENT_TYPE_COMPETITOR_BUYER.equals(config.getAgentType())) {
            List<Long> currentCommentLeadIds = getCollectTaskLeadIds(collectTaskId);
            addRunLog(config.getId(), "评论采集完成",
                    String.format("采集评论%s条，进入AI分析", currentCommentLeadIds.size()), "success");
            int analyzedUsers = analyzePendingCompetitorCommentUsers(config, currentCommentLeadIds);
            int threshold = resolveTouchScoreThreshold(config);
            long qualifiedUsers = countQualifiedLeads(currentCommentLeadIds, threshold);
            long missingTargetUsers = countQualifiedMissingTargetUsers(currentCommentLeadIds, threshold);
            long existingTargetTouches = countQualifiedExistingTargetTouches(currentCommentLeadIds, threshold);
            addRunLog(config.getId(), "AI分析完成",
                    String.format("分析%s条，达标%s条，触达阈值%s", analyzedUsers, qualifiedUsers,
                            formatUserAnalysisThreshold(currentCommentLeadIds, threshold)), "success");
            int queuedTouches = queueCommentLeadDmTouches(config, accountIds, currentCommentLeadIds);
            TouchActivateResult activatedTouches = activateDueTouchRecords(config, currentCommentLeadIds);
            refreshDiscoveryStats(config.getId());
            addTouchSummaryLog(config.getId(), queuedTouches, activatedTouches, qualifiedUsers, missingTargetUsers, existingTargetTouches);
        }
    }

    private FbAiAgentDispatchRespVO dispatchInternal(boolean scheduledOnly, boolean enqueueForVuePoller) {
        List<FbAiAgentConfigDO> configs = agentConfigMapper.selectList(new LambdaQueryWrapper<FbAiAgentConfigDO>()
                .eq(FbAiAgentConfigDO::getStatus, 1)
                .in(FbAiAgentConfigDO::getAgentType, Arrays.asList(AGENT_TYPE_PAGE_LEAD, AGENT_TYPE_POST_LEAD, AGENT_TYPE_GROUP_POST, AGENT_TYPE_GROUP_COMMENT, AGENT_TYPE_COMPETITOR_BUYER))
                .orderByAsc(FbAiAgentConfigDO::getId));
        if (CollUtil.isEmpty(configs)) {
            return new FbAiAgentDispatchRespVO(false, "暂无运行中的AI获客Agent");
        }
        AgentDispatchStats stats = new AgentDispatchStats();
        List<FbAiAgentDispatchRespVO.CollectDetail> launchDetails = new ArrayList<>();
        int skipped = 0;
        int timeoutFailedDetails = 0;
        List<String> skipReasons = new ArrayList<>();

        for (FbAiAgentConfigDO config : configs) {
            timeoutFailedDetails += failTimedOutCollectDetails(config);
            TouchActivateResult activatedExistingTouches = activateDueTouchRecords(config, null);
            stats.activatedTouches += activatedExistingTouches.totalDetails();
            AgentDueResult dueResult = checkAgentDue(config);
            if (scheduledOnly && !dueResult.due) {
                skipped++;
                skipReasons.add(config.getAgentName() + "：" + dueResult.reason);
                log.debug("AI主页获客Agent跳过新一轮主页发现, reason={}, agentId={}, agentName={}, executeTime={}, lastExecuteTime={}",
                        dueResult.reason, config.getId(), config.getAgentName(), config.getExecuteTime(), config.getLastExecuteTime());
                continue;
            }
            stats.executedAgents++;
            List<String> accountIds = resolveAgentAccountIds(config, resolveTargetCustomerCount(config));
            if (AGENT_TYPE_COMPETITOR_BUYER.equals(config.getAgentType())) {
                List<String> pageUrls = resolveCompetitorPageUrls(config);
                if (CollUtil.isEmpty(pageUrls)) {
                    stats.executedAgents--;
                    skipped++;
                    skipReasons.add(config.getAgentName() + "：竞品主页为空");
                    continue;
                }
                addRunLog(config.getId(), "开始执行",
                        String.format("竞品主页%s个，采集最近%s天", pageUrls.size(), resolveCompetitorRecentDays(config)), "info");
                int created = createCompetitorPostCollectTasks(config, pageUrls, accountIds, launchDetails, enqueueForVuePoller);
                TouchActivateResult activatedTouches = activateDueTouchRecords(config, null);
                stats.createdCollectTasks += created;
                stats.activatedTouches += activatedTouches.totalDetails();
            } else if (isGroupMonitorAgent(config.getAgentType())) {
                List<String> groupUrls = resolveGroupPostUrls(config);
                if (CollUtil.isEmpty(groupUrls)) {
                    stats.executedAgents--;
                    skipped++;
                    skipReasons.add(config.getAgentName() + "：监控群组为空");
                    continue;
                }
                addRunLog(config.getId(), "开始执行",
                        String.format("群组%s个，采集最近%s天", groupUrls.size(), resolveGroupPostRecentDays(config)), "info");
                int created = AGENT_TYPE_GROUP_COMMENT.equals(config.getAgentType())
                        ? createGroupCommentPostCollectTasks(config, groupUrls, accountIds, launchDetails, enqueueForVuePoller, false)
                        : createGroupPostCollectTasks(config, groupUrls, accountIds, launchDetails, enqueueForVuePoller, false);
                int analyzedPosts = AGENT_TYPE_GROUP_COMMENT.equals(config.getAgentType()) ? 0 : analyzePendingPosts(config, null);
                int queuedTouches = AGENT_TYPE_GROUP_COMMENT.equals(config.getAgentType()) ? 0 : queuePostHighIntentTouches(config, accountIds, null);
                TouchActivateResult activatedTouches = activateDueTouchRecords(config, null);
                stats.createdCollectTasks += created;
                stats.analyzedPosts += analyzedPosts;
                stats.queuedTouches += queuedTouches;
                stats.activatedTouches += activatedTouches.totalDetails();
            } else if (AGENT_TYPE_POST_LEAD.equals(config.getAgentType())) {
                List<String> runKeywords = pickRunKeywords(config);
                addRunLog(config.getId(), "开始执行",
                        String.format("帖子关键词%s个，目标%s个", runKeywords.size(), resolveTargetCustomerCount(config)), "info");
                int created = createPostLeadCollectTasks(config, runKeywords, accountIds, launchDetails, enqueueForVuePoller);
                stats.createdCollectTasks += created;
                advanceKeywordCursor(config, runKeywords.size());
            } else {
                List<String> targetCountries = parseJsonStringList(config.getTargetCountries());
                List<String> targetLanguages = parseJsonStringList(config.getTargetLanguages());
                List<String> runKeywords = pickRunKeywords(config);
                addRunLog(config.getId(), "开始执行",
                        String.format("关键词%s个，目标%s个", runKeywords.size(), resolveTargetCustomerCount(config)), "info");
                int created = createPageLeadCollectTasks(config, runKeywords, accountIds, launchDetails, enqueueForVuePoller);
                int deepCreated = createDeepCollectTasks(config, accountIds, launchDetails, enqueueForVuePoller);
                int analyzedUsers = analyzePendingUsers(config, runKeywords, targetCountries, targetLanguages, null);
                int queuedTouches = queueHighIntentTouches(config, accountIds, null);
                TouchActivateResult activatedTouches = activateDueTouchRecords(config, null);
                stats.createdCollectTasks += created;
                stats.createdDeepCollectTasks += deepCreated;
                stats.analyzedUsers += analyzedUsers;
                stats.queuedTouches += queuedTouches;
                stats.activatedTouches += activatedTouches.totalDetails();
                advanceKeywordCursor(config, runKeywords.size());
            }
            if (scheduledOnly) {
                markAgentExecuted(config.getId());
            }
        }

        if (stats.executedAgents == 0) {
            String message = scheduledOnly
                    ? String.format("AI主页获客维护完成：无需启动新一轮主页发现%s个，超时失败%s个，转执行任务%s条%s",
                    skipped, timeoutFailedDetails, stats.activatedTouches,
                    CollUtil.isEmpty(skipReasons) ? "" : "，原因：" + String.join("；", skipReasons))
                    : "暂无可执行的AI主页获客Agent";
            log.info("[dispatchOnce][{}]", message);
            return new FbAiAgentDispatchRespVO(true, message);
        }
        String message = String.format("Agent调度完成：运行Agent%s个，新建主页采集%s个，新建深度采集%s个，分析潜客%s条，排队触达%s条，转执行任务%s条",
                stats.executedAgents, stats.createdCollectTasks, stats.createdDeepCollectTasks, stats.analyzedUsers, stats.queuedTouches, stats.activatedTouches);
        log.info("[dispatchOnce][{}]", message);
        FbAiAgentDispatchRespVO respVO = new FbAiAgentDispatchRespVO(true, message);
        respVO.setDetails(launchDetails);
        return respVO;
    }

    private int failTimedOutCollectDetails(FbAiAgentConfigDO config) {
        List<Long> taskIds = getAgentDiscoveryTaskIds(config.getId());
        if (CollUtil.isEmpty(taskIds)) {
            return 0;
        }
        LocalDateTime deadline = LocalDateTime.now().minusMinutes(COLLECT_RUNNING_TIMEOUT_MINUTES);
        List<FbCollectDetailDO> timeoutDetails = collectDetailMapper.selectList(new LambdaQueryWrapper<FbCollectDetailDO>()
                .in(FbCollectDetailDO::getTaskId, taskIds)
                .eq(FbCollectDetailDO::getStatus, 1)
                .isNotNull(FbCollectDetailDO::getStartTime)
                .le(FbCollectDetailDO::getStartTime, deadline)
                .last("LIMIT 200"));
        if (CollUtil.isEmpty(timeoutDetails)) {
            return 0;
        }

        LocalDateTime now = LocalDateTime.now();
        Set<Long> changedTaskIds = new LinkedHashSet<>();
        for (FbCollectDetailDO detail : timeoutDetails) {
            FbCollectDetailDO updateObj = new FbCollectDetailDO();
            updateObj.setId(detail.getId());
            updateObj.setStatus(3);
            updateObj.setErrorMessage("AI Agent采集超过3分钟未回传，已自动标记失败");
            updateObj.setEndTime(now);
            collectDetailMapper.updateById(updateObj);
            collectQueueService.remove(detail.getId(), detail.getFbAccount());
            changedTaskIds.add(detail.getTaskId());
        }

        for (Long taskId : changedTaskIds) {
            boolean taskFinished = updateCollectTaskProgress(taskId);
            if (taskFinished) {
                continueAfterCollectTaskFinished(taskId);
            }
        }
        addRunLog(config.getId(), "采集异常", "3分钟未回传，失败" + timeoutDetails.size() + "条", "warning");
        return timeoutDetails.size();
    }

    private boolean updateCollectTaskProgress(Long taskId) {
        Map<String, Object> stats = collectDetailMapper.selectTaskStats(taskId);
        if (stats == null || stats.isEmpty()) {
            return false;
        }
        List<FbCollectDetailDO> details = collectDetailMapper.selectListByTaskId(taskId);
        long unfinishedCount = details.stream()
                .filter(d -> d.getStatus() != null && (d.getStatus() == 0 || d.getStatus() == 1))
                .count();
        long failedCount = details.stream()
                .filter(d -> Objects.equals(d.getStatus(), 3))
                .count();
        int totalCollected = Optional.ofNullable(stats.get("total_collected"))
                .map(Number.class::cast)
                .map(Number::intValue)
                .orElse(0);

        FbCollectDO updateObj = new FbCollectDO();
        updateObj.setId(taskId);
        updateObj.setTotalCollectedCount(totalCollected);
        if (unfinishedCount == 0) {
            updateObj.setStatus(failedCount > 0 ? 3 : 2);
            updateObj.setEndTime(LocalDateTime.now());
        } else {
            updateObj.setStatus(1);
        }
        collectMapper.updateById(updateObj);
        return unfinishedCount == 0;
    }

    private boolean isAgentDue(FbAiAgentConfigDO config) {
        return checkAgentDue(config).due;
    }

    private AgentDueResult checkAgentDue(FbAiAgentConfigDO config) {
        String frequency = StrUtil.blankToDefault(config.getExecuteFrequency(), "1");
        if (!"daily".equals(frequency) && parseIntervalDays(frequency) < 1) {
            return new AgentDueResult(false, "不支持的执行频率：" + frequency);
        }
        LocalDateTime now = LocalDateTime.now();
        LocalTime executeTime = parseExecuteTime(config.getExecuteTime());
        int intervalDays = resolveExecuteIntervalDays(config);
        if (config.getLastExecuteTime() != null
                && now.toLocalDate().isBefore(config.getLastExecuteTime().toLocalDate().plusDays(intervalDays))) {
            return new AgentDueResult(false, "未到下次执行间隔");
        }

        // 服务当天晚于计划时间启动，说明本次计划已经错过，今天不再补执行。
        // 服务在计划时间前启动，则允许调度器在计划时间后正常执行。
        if (serviceStartTime.toLocalDate().isEqual(now.toLocalDate())
                && serviceStartTime.toLocalTime().isAfter(executeTime)) {
            return new AgentDueResult(false,
                    String.format("服务启动时间%s已错过今日计划时间%s，今日跳过",
                            serviceStartTime.toLocalTime().withSecond(0).withNano(0), executeTime));
        }

        if (now.toLocalTime().isBefore(executeTime)) {
            return new AgentDueResult(false, "未到计划执行时间");
        }
        return new AgentDueResult(true, "已到计划执行时间");
    }

    private int resolveExecuteIntervalDays(FbAiAgentConfigDO config) {
        return parseIntervalDays(config.getExecuteFrequency());
    }

    private int parseIntervalDays(String executeFrequency) {
        if ("daily".equals(executeFrequency)) {
            return 1;
        }
        try {
            int intervalDays = Integer.parseInt(StrUtil.blankToDefault(executeFrequency, "1"));
            return intervalDays >= 1 && intervalDays <= 7 ? intervalDays : -1;
        } catch (NumberFormatException ex) {
            return -1;
        }
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
            Long accountId = accountIdLongs.get(created % accountIdLongs.size());
            task.setTotalExpectedCount(DEFAULT_COLLECT_EXPECTED_COUNT);
            task.setTotalCollectedCount(0);
            task.setAccountCount(1);
            task.setUrlCount(1);
            task.setFbAccount(accountMap.get(accountId));
            collectMapper.insert(task);

            FbCollectDetailDO detail = new FbCollectDetailDO();
            detail.setTaskId(task.getId());
            detail.setFbAccount(StrUtil.blankToDefault(accountMap.get(accountId), "account_" + accountId));
            detail.setSearchUrl(searchUrl);
            detail.setExpectedCount(DEFAULT_COLLECT_EXPECTED_COUNT);
            detail.setCollectedCount(0);
            detail.setStatus(0);
            collectDetailMapper.insert(detail);
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
            addRunLog(config.getId(), "采集异常", "关键词池或账号池为空", "warning");
            return 0;
        }
        Map<Long, String> accountMap = resolveAccountMap(accountIds);
        if (accountMap.isEmpty()) {
            addRunLog(config.getId(), "采集异常", "未找到可用账号", "warning");
            return 0;
        }
        List<Long> accountIdLongs = new ArrayList<>(accountMap.keySet());
        int created = 0;
        int targetTotal = resolveTargetCustomerCount(config);
        for (int i = 0; i < keywords.size(); i++) {
            String keyword = StrUtil.trim(keywords.get(i));
            if (StrUtil.isBlank(keyword)) {
                continue;
            }
            int expectedCount = distributeExpectedCount(targetTotal, keywords.size(), i);
            Long accountId = accountIdLongs.get(i % accountIdLongs.size());
            String searchUrl = buildKeywordSearchUrl(config, keyword, true, false);
            if (!collectQueueService.tryMarkCreated(config.getId(), "page", searchUrl)) {
                continue;
            }
            FbCollectDO task = createCollectTask(PAGE_COLLECT_TASK_TYPE, searchUrl, 1,
                    "AI主页获客:" + config.getAgentName() + ":" + keyword,
                    Collections.singletonList(accountId), accountMap);
            updateCollectTaskExpected(task.getId(), expectedCount, expectedCount, 1);
            FbCollectDetailDO detail = createCollectDetail(task.getId(), accountId, accountMap.get(accountId), searchUrl, expectedCount);
            if (enqueueForVuePoller) {
                collectQueueService.push(detail.getId(), detail.getFbAccount());
            }
            addLaunchDetail(launchDetails, task, detail, accountId);
            createDiscoveryLog(config.getId(), keyword, task.getId(), "page");
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
                .last("LIMIT " + resolveTargetCustomerCount(config)));
        if (CollUtil.isEmpty(users)) {
            return 0;
        }
        List<FbCollectUserDO> pendingUsers = new ArrayList<>();
        for (FbCollectUserDO user : users) {
            if (StrUtil.isNotBlank(user.getUrl()) && collectQueueService.tryMarkCreated(config.getId(), "deep", user.getUrl())) {
                pendingUsers.add(user);
            }
        }
        if (CollUtil.isEmpty(pendingUsers)) {
            return 0;
        }

        List<Long> taskAccountIds = accountIdLongs.subList(0, Math.min(accountIdLongs.size(), pendingUsers.size()));
        String searchUrls = pendingUsers.stream().map(FbCollectUserDO::getUrl).collect(Collectors.joining("\n"));
        FbCollectDO task = createCollectTask(DEEP_COLLECT_TASK_TYPE, searchUrls, 0,
                "AI主页深度采集:" + config.getAgentName() + ":" + pendingUsers.size() + "个主页",
                taskAccountIds, accountMap);
        updateCollectTaskExpected(task.getId(), 1, pendingUsers.size(), pendingUsers.size());
        createDiscoveryLog(config.getId(), "深度采集", task.getId(), "deep");

        int created = 0;
        for (FbCollectUserDO user : pendingUsers) {
            Long accountId = taskAccountIds.get(created % taskAccountIds.size());
            FbCollectDetailDO detail = createCollectDetail(task.getId(), accountId, accountMap.get(accountId), user.getUrl(), 1);
            detail.setSourceUserId(user.getId());
            collectDetailMapper.updateById(detail);
            if (enqueueForVuePoller) {
                collectQueueService.push(detail.getId(), detail.getFbAccount());
            }
            addLaunchDetail(launchDetails, task, detail, accountId);
            created++;
        }
        return created;
    }

    private int createPostLeadCollectTasks(FbAiAgentConfigDO config, List<String> keywords, List<String> accountIds,
                                           List<FbAiAgentDispatchRespVO.CollectDetail> launchDetails,
                                           boolean enqueueForVuePoller) {
        if (CollUtil.isEmpty(keywords) || CollUtil.isEmpty(accountIds)) {
            addRunLog(config.getId(), "采集异常", "关键词池或账号池为空", "warning");
            return 0;
        }
        Map<Long, String> accountMap = resolveAccountMap(accountIds);
        if (accountMap.isEmpty()) {
            addRunLog(config.getId(), "采集异常", "未找到可用账号", "warning");
            return 0;
        }
        List<Long> accountIdLongs = new ArrayList<>(accountMap.keySet());
        int created = 0;
        int targetTotal = resolveTargetCustomerCount(config);
        for (int i = 0; i < keywords.size(); i++) {
            String keyword = StrUtil.trim(keywords.get(i));
            if (StrUtil.isBlank(keyword)) {
                continue;
            }
            String searchUrl = buildKeywordSearchUrl(config, keyword, false, isPostLeadLatestPosts(config));
            if (!collectQueueService.tryMarkCreated(config.getId(), "post_lead", searchUrl)) {
                continue;
            }
            int expectedCount = distributeExpectedCount(targetTotal, keywords.size(), i);
            Long accountId = accountIdLongs.get(i % accountIdLongs.size());
            FbCollectDO task = createCollectTask(POST_COLLECT_TASK_TYPE, searchUrl, 2,
                    "AI帖子获客:" + config.getAgentName() + ":" + keyword,
                    Collections.singletonList(accountId), accountMap);
            updateCollectTaskExpected(task.getId(), expectedCount, expectedCount, 1);
            FbCollectDetailDO detail = createCollectDetail(task.getId(), accountId, accountMap.get(accountId), searchUrl, expectedCount);
            if (enqueueForVuePoller) {
                collectQueueService.push(detail.getId(), detail.getFbAccount());
            }
            addLaunchDetail(launchDetails, task, detail, accountId);
            createDiscoveryLog(config.getId(), keyword, task.getId(), "post_lead");
            created++;
        }
        return created;
    }

    private int createGroupPostCollectTasks(FbAiAgentConfigDO config, List<String> groupUrls, List<String> accountIds,
                                            List<FbAiAgentDispatchRespVO.CollectDetail> launchDetails,
                                            boolean enqueueForVuePoller, boolean forceCreate) {
        if (CollUtil.isEmpty(groupUrls) || CollUtil.isEmpty(accountIds)) {
            addRunLog(config.getId(), "采集异常", "群组或账号池为空", "warning");
            return 0;
        }
        Map<Long, String> accountMap = resolveAccountMap(accountIds);
        if (accountMap.isEmpty()) {
            addRunLog(config.getId(), "采集异常", "未找到可用账号", "warning");
            return 0;
        }
        List<String> normalizedUrls = groupUrls.stream()
                .map(this::normalizeGroupMonitorUrl)
                .filter(StrUtil::isNotBlank)
                .distinct()
                .collect(Collectors.toList());
        if (CollUtil.isEmpty(normalizedUrls)) {
            return 0;
        }
        List<String> pendingUrls = normalizedUrls.stream()
                .filter(groupUrl -> forceCreate || collectQueueService.tryMarkCreated(config.getId(), "group_post", groupUrl))
                .collect(Collectors.toList());
        if (CollUtil.isEmpty(pendingUrls)) {
            return 0;
        }
        List<Long> accountIdLongs = new ArrayList<>(accountMap.keySet());
        int expectedTotal = pendingUrls.size() * GROUP_POST_COLLECT_SAFETY_LIMIT;
        String searchUrls = String.join("\n", pendingUrls);
        FbCollectDO task = createCollectTask(POST_COLLECT_TASK_TYPE, searchUrls, 2,
                "AI群帖获客:" + config.getAgentName() + ":" + pendingUrls.size() + "个群",
                accountIdLongs.subList(0, Math.min(accountIdLongs.size(), pendingUrls.size())),
                accountMap);
        updateCollectTaskExpected(task.getId(), GROUP_POST_COLLECT_SAFETY_LIMIT, expectedTotal, pendingUrls.size());
        createDiscoveryLog(config.getId(), "群帖采集", task.getId(), "group_post");

        int created = 0;
        for (int i = 0; i < pendingUrls.size(); i++) {
            String groupUrl = pendingUrls.get(i);
            Long accountId = accountIdLongs.get(i % accountIdLongs.size());
            FbCollectDetailDO detail = createCollectDetail(task.getId(), accountId, accountMap.get(accountId), groupUrl, GROUP_POST_COLLECT_SAFETY_LIMIT);
            if (enqueueForVuePoller) {
                collectQueueService.push(detail.getId(), detail.getFbAccount());
            }
            addLaunchDetail(launchDetails, task, detail, accountId);
            created++;
        }
        return created;
    }

    private int createGroupCommentPostCollectTasks(FbAiAgentConfigDO config, List<String> groupUrls, List<String> accountIds,
                                                   List<FbAiAgentDispatchRespVO.CollectDetail> launchDetails,
                                                   boolean enqueueForVuePoller, boolean forceCreate) {
        if (CollUtil.isEmpty(groupUrls) || CollUtil.isEmpty(accountIds)) {
            addRunLog(config.getId(), "采集异常", "群组或账号池为空", "warning");
            return 0;
        }
        Map<Long, String> accountMap = resolveAccountMap(accountIds);
        if (accountMap.isEmpty()) {
            addRunLog(config.getId(), "采集异常", "未找到可用账号", "warning");
            return 0;
        }
        List<String> normalizedUrls = groupUrls.stream()
                .map(this::normalizeGroupMonitorUrl)
                .filter(StrUtil::isNotBlank)
                .distinct()
                .collect(Collectors.toList());
        if (CollUtil.isEmpty(normalizedUrls)) {
            return 0;
        }
        List<String> pendingUrls = normalizedUrls.stream()
                .filter(groupUrl -> forceCreate || collectQueueService.tryMarkCreated(config.getId(), "group_comment_post", groupUrl))
                .collect(Collectors.toList());
        if (CollUtil.isEmpty(pendingUrls)) {
            return 0;
        }
        List<Long> accountIdLongs = new ArrayList<>(accountMap.keySet());
        int expectedTotal = pendingUrls.size() * GROUP_POST_COLLECT_SAFETY_LIMIT;
        String searchUrls = String.join("\n", pendingUrls);
        FbCollectDO task = createCollectTask(POST_COLLECT_TASK_TYPE, searchUrls, 2,
                "AI群帖评论截流-帖子采集:" + config.getAgentName() + ":" + pendingUrls.size() + "个群",
                accountIdLongs.subList(0, Math.min(accountIdLongs.size(), pendingUrls.size())),
                accountMap);
        updateCollectTaskExpected(task.getId(), GROUP_POST_COLLECT_SAFETY_LIMIT, expectedTotal, pendingUrls.size());
        createDiscoveryLog(config.getId(), "群帖采集", task.getId(), "group_comment_post");

        int created = 0;
        for (int i = 0; i < pendingUrls.size(); i++) {
            String groupUrl = pendingUrls.get(i);
            Long accountId = accountIdLongs.get(i % accountIdLongs.size());
            FbCollectDetailDO detail = createCollectDetail(task.getId(), accountId, accountMap.get(accountId), groupUrl, GROUP_POST_COLLECT_SAFETY_LIMIT);
            if (enqueueForVuePoller) {
                collectQueueService.push(detail.getId(), detail.getFbAccount());
            }
            addLaunchDetail(launchDetails, task, detail, accountId);
            created++;
        }
        return created;
    }

    private int createGroupCommentCollectTasks(FbAiAgentConfigDO config, List<String> accountIds, List<Long> scopedPostIds,
                                               boolean enqueueForVuePoller) {
        if (CollUtil.isEmpty(accountIds) || CollUtil.isEmpty(scopedPostIds)) {
            return 0;
        }
        Map<Long, String> accountMap = resolveAccountMap(accountIds);
        if (accountMap.isEmpty()) {
            addRunLog(config.getId(), "采集异常", "未找到可用账号", "warning");
            return 0;
        }
        int threshold = resolveTouchScoreThreshold(config);
        List<FbCollectPostDO> posts = collectPostMapper.selectList(new LambdaQueryWrapper<FbCollectPostDO>()
                .in(FbCollectPostDO::getId, scopedPostIds)
                .ge(FbCollectPostDO::getProductRelevanceScore, threshold)
                .isNotNull(FbCollectPostDO::getUrl)
                .orderByDesc(FbCollectPostDO::getProductRelevanceScore)
                .orderByDesc(FbCollectPostDO::getId));
        if (CollUtil.isEmpty(posts)) {
            return 0;
        }
        List<FbCollectPostDO> pendingPosts = posts.stream()
                .filter(post -> StrUtil.isNotBlank(post.getUrl()))
                .filter(post -> isCommentablePostUrl(post.getUrl()))
                .filter(post -> collectQueueService.tryMarkCreated(config.getId(), "group_comment_comment", post.getUrl()))
                .collect(Collectors.toList());
        if (CollUtil.isEmpty(pendingPosts)) {
            return 0;
        }
        List<Long> accountIdLongs = new ArrayList<>(accountMap.keySet());
        List<Long> taskAccountIds = accountIdLongs.subList(0, Math.min(accountIdLongs.size(), pendingPosts.size()));
        String searchUrls = pendingPosts.stream().map(FbCollectPostDO::getUrl).collect(Collectors.joining("\n"));
        FbCollectDO task = createCollectTask(COMMENT_LIKE_COLLECT_TASK_TYPE, searchUrls, 0,
                "AI群帖评论截流-评论采集:" + config.getAgentName() + ":" + pendingPosts.size() + "个帖子",
                taskAccountIds, accountMap);
        updateCollectTaskExpected(task.getId(), 100, pendingPosts.size() * 100, pendingPosts.size());
        createDiscoveryLog(config.getId(), "评论采集", task.getId(), "group_comment");

        int created = 0;
        for (FbCollectPostDO post : pendingPosts) {
            Long accountId = taskAccountIds.get(created % taskAccountIds.size());
            FbCollectDetailDO detail = createCollectDetail(task.getId(), accountId, accountMap.get(accountId), post.getUrl(), 100);
            detail.setSourceUserId(post.getId());
            collectDetailMapper.updateById(detail);
            if (enqueueForVuePoller) {
                collectQueueService.push(detail.getId(), detail.getFbAccount());
            }
            created++;
        }
        return created;
    }

    private int createCompetitorPostCollectTasks(FbAiAgentConfigDO config, List<String> pageUrls, List<String> accountIds,
                                                 List<FbAiAgentDispatchRespVO.CollectDetail> launchDetails,
                                                 boolean enqueueForVuePoller) {
        if (CollUtil.isEmpty(pageUrls) || CollUtil.isEmpty(accountIds)) {
            addRunLog(config.getId(), "采集异常", "竞品主页或账号池为空", "warning");
            return 0;
        }
        Map<Long, String> accountMap = resolveAccountMap(accountIds);
        if (accountMap.isEmpty()) {
            addRunLog(config.getId(), "采集异常", "未找到可用账号", "warning");
            return 0;
        }
        List<String> normalizedUrls = pageUrls.stream()
                .map(this::normalizeCompetitorPageUrl)
                .filter(StrUtil::isNotBlank)
                .distinct()
                .collect(Collectors.toList());
        if (CollUtil.isEmpty(normalizedUrls)) {
            return 0;
        }
        List<Long> accountIdLongs = new ArrayList<>(accountMap.keySet());
        int expectedTotal = normalizedUrls.size() * GROUP_POST_COLLECT_SAFETY_LIMIT;
        String searchUrls = String.join("\n", normalizedUrls);
        FbCollectDO task = createCollectTask(POST_COLLECT_TASK_TYPE, searchUrls, 2,
                "AI竞品监控-帖子采集:" + config.getAgentName() + ":" + normalizedUrls.size() + "个主页",
                accountIdLongs.subList(0, Math.min(accountIdLongs.size(), normalizedUrls.size())),
                accountMap);
        updateCollectTaskExpected(task.getId(), GROUP_POST_COLLECT_SAFETY_LIMIT, expectedTotal, normalizedUrls.size());
        createDiscoveryLog(config.getId(), "主页帖子采集", task.getId(), "competitor_post");

        int created = 0;
        for (int i = 0; i < normalizedUrls.size(); i++) {
            String pageUrl = normalizedUrls.get(i);
            if (!collectQueueService.tryMarkCreated(config.getId(), "competitor_post", pageUrl)) {
                continue;
            }
            Long accountId = accountIdLongs.get(i % accountIdLongs.size());
            FbCollectDetailDO detail = createCollectDetail(task.getId(), accountId, accountMap.get(accountId), pageUrl, GROUP_POST_COLLECT_SAFETY_LIMIT);
            if (enqueueForVuePoller) {
                collectQueueService.push(detail.getId(), detail.getFbAccount());
            }
            addLaunchDetail(launchDetails, task, detail, accountId);
            created++;
        }
        return created;
    }

    private int createCompetitorCommentCollectTasks(FbAiAgentConfigDO config, List<String> accountIds, List<Long> scopedPostIds,
                                                    boolean enqueueForVuePoller) {
        if (CollUtil.isEmpty(accountIds) || CollUtil.isEmpty(scopedPostIds)) {
            return 0;
        }
        Map<Long, String> accountMap = resolveAccountMap(accountIds);
        if (accountMap.isEmpty()) {
            addRunLog(config.getId(), "采集异常", "未找到可用账号", "warning");
            return 0;
        }
        List<FbCollectPostDO> posts = collectPostMapper.selectList(new LambdaQueryWrapper<FbCollectPostDO>()
                .in(FbCollectPostDO::getId, scopedPostIds)
                .isNotNull(FbCollectPostDO::getUrl)
                .orderByDesc(FbCollectPostDO::getId));
        if (CollUtil.isEmpty(posts)) {
            return 0;
        }
        List<FbCollectPostDO> pendingPosts = posts.stream()
                .filter(post -> StrUtil.isNotBlank(post.getUrl()))
                .filter(post -> isCommentablePostUrl(post.getUrl()))
                .filter(post -> collectQueueService.tryMarkCreated(config.getId(), "competitor_comment", post.getUrl()))
                .collect(Collectors.toList());
        if (CollUtil.isEmpty(pendingPosts)) {
            return 0;
        }
        List<Long> accountIdLongs = new ArrayList<>(accountMap.keySet());
        List<Long> taskAccountIds = accountIdLongs.subList(0, Math.min(accountIdLongs.size(), pendingPosts.size()));
        String searchUrls = pendingPosts.stream().map(FbCollectPostDO::getUrl).collect(Collectors.joining("\n"));
        FbCollectDO task = createCollectTask(COMMENT_LIKE_COLLECT_TASK_TYPE, searchUrls, 0,
                "AI竞品监控-评论采集:" + config.getAgentName() + ":" + pendingPosts.size() + "个帖子",
                taskAccountIds, accountMap);
        updateCollectTaskExpected(task.getId(), 100, pendingPosts.size() * 100, pendingPosts.size());
        createDiscoveryLog(config.getId(), "评论采集", task.getId(), "competitor_comment");

        int created = 0;
        for (FbCollectPostDO post : pendingPosts) {
            Long accountId = taskAccountIds.get(created % taskAccountIds.size());
            FbCollectDetailDO detail = createCollectDetail(task.getId(), accountId, accountMap.get(accountId), post.getUrl(), 100);
            detail.setSourceUserId(post.getId());
            collectDetailMapper.updateById(detail);
            if (enqueueForVuePoller) {
                collectQueueService.push(detail.getId(), detail.getFbAccount());
            }
            created++;
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

    private int resolveTargetCustomerCount(FbAiAgentConfigDO config) {
        Integer value = config == null ? null : config.getTargetCustomerCount();
        return value != null && value > 0 ? value : DEFAULT_COLLECT_EXPECTED_COUNT;
    }

    /** Agent每轮执行前统一解析账号池，AUTO忽略旧的账号列表，MANUAL保留用户选择。 */
    private List<String> resolveAgentAccountIds(FbAiAgentConfigDO config, int targetCount) {
        List<String> requested = parseCsvStringList(config.getAccountIds());
        List<Long> requestedIds = requested.stream().map(Long::valueOf).collect(Collectors.toList());
        String mode = StrUtil.isBlank(config.getAccountSelectionMode())
                ? (CollUtil.isEmpty(requested) ? "AUTO" : "MANUAL") : config.getAccountSelectionMode();
        List<Long> selected = accountAllocationService.selectAccounts(
                mode, requestedIds, Math.max(1, targetCount), "agent", Collections.singletonList("collect"));
        return selected.stream().map(String::valueOf).collect(Collectors.toList());
    }

    /**
     * 触达阶段重新选择账号，不沿用采集阶段账号池。
     * AUTO 按本批实际触达数量决定最多参与的账号数，MANUAL 仍限制在用户选择的账号范围内。
     */
    private List<String> resolveAgentTouchAccountIds(FbAiAgentConfigDO config, String actionType, int targetCount) {
        if (config == null || targetCount <= 0) {
            return Collections.emptyList();
        }
        List<String> requested = parseCsvStringList(config.getAccountIds());
        List<Long> requestedIds = requested.stream().map(Long::valueOf).collect(Collectors.toList());
        String mode = StrUtil.isBlank(config.getAccountSelectionMode())
                ? (CollUtil.isEmpty(requested) ? "AUTO" : "MANUAL") : config.getAccountSelectionMode();
        List<Long> selected = accountAllocationService.selectAccounts(
                mode, requestedIds, targetCount, "agent", Collections.singletonList(actionType));
        return selected.stream().map(String::valueOf).collect(Collectors.toList());
    }

    private int distributeExpectedCount(int total, int buckets, int index) {
        if (buckets <= 0) {
            return total;
        }
        int base = Math.max(total / buckets, 1);
        int remainder = Math.max(total % buckets, 0);
        return base + (index < remainder ? 1 : 0);
    }

    private void updateCollectTaskExpected(Long taskId, Integer expectedCount, Integer totalExpectedCount, Integer urlCount) {
        FbCollectDO updateObj = new FbCollectDO();
        updateObj.setId(taskId);
        updateObj.setExpectedCount(expectedCount);
        updateObj.setTotalExpectedCount(totalExpectedCount);
        updateObj.setUrlCount(urlCount);
        collectMapper.updateById(updateObj);
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

    private void createDiscoveryLog(Long agentConfigId, String keyword, Long collectTaskId, String sourceType) {
        FbAiAgentDiscoveryLogDO logDO = new FbAiAgentDiscoveryLogDO();
        logDO.setAgentConfigId(agentConfigId);
        logDO.setKeyword(keyword);
        logDO.setSourceType(StrUtil.blankToDefault(sourceType, "page"));
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
            Long accountId = accountIdLongs.get(created % accountIdLongs.size());
            task.setTotalExpectedCount(DEFAULT_COLLECT_EXPECTED_COUNT);
            task.setTotalCollectedCount(0);
            task.setAccountCount(1);
            task.setUrlCount(1);
            task.setFbAccount(accountMap.get(accountId));
            collectMapper.insert(task);

            FbCollectDetailDO detail = new FbCollectDetailDO();
            detail.setTaskId(task.getId());
            detail.setFbAccount(StrUtil.blankToDefault(accountMap.get(accountId), "account_" + accountId));
            detail.setSearchUrl(groupUrl);
            detail.setExpectedCount(DEFAULT_COLLECT_EXPECTED_COUNT);
            detail.setCollectedCount(0);
            detail.setStatus(0);
            collectDetailMapper.insert(detail);
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
                                    List<String> targetCountries, List<String> targetLanguages,
                                    List<Long> scopedLeadIds) {
        List<Long> discoveryTaskIds = getAgentDiscoveryTaskIds(config.getId());
        if (CollUtil.isEmpty(discoveryTaskIds)) {
            return 0;
        }
        List<Long> leadIds = CollUtil.isNotEmpty(scopedLeadIds) ? scopedLeadIds : getAgentLeadIds(config.getId());
        if (CollUtil.isEmpty(leadIds)) {
            return 0;
        }
        List<FbCollectUserDO> users = collectUserMapper.selectList(new LambdaQueryWrapper<FbCollectUserDO>()
                .in(FbCollectUserDO::getId, leadIds)
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
        return users.size();
    }

    private int analyzePendingPosts(FbAiAgentConfigDO config, List<Long> scopedPostIds) {
        List<Long> discoveryTaskIds = getAgentDiscoveryTaskIds(config.getId());
        if (CollUtil.isEmpty(discoveryTaskIds)) {
            return 0;
        }
        List<Long> postIds = CollUtil.isNotEmpty(scopedPostIds) ? scopedPostIds : getAgentPostLeadIds(config.getId());
        if (CollUtil.isEmpty(postIds)) {
            return 0;
        }
        List<FbCollectPostDO> posts = collectPostMapper.selectList(new LambdaQueryWrapper<FbCollectPostDO>()
                .in(FbCollectPostDO::getId, postIds)
                .and(wrapper -> wrapper.isNull(FbCollectPostDO::getLastAiAnalyzeTime)
                        .or().isNull(FbCollectPostDO::getProductRelevanceScore))
                .orderByAsc(FbCollectPostDO::getId)
                .last("LIMIT " + MAX_ANALYZE_PER_RUN));
        if (CollUtil.isEmpty(posts)) {
            return 0;
        }
        Map<Long, LeadAnalysisResult> workflowResults = analyzePostsByWorkflow(config, posts);
        for (FbCollectPostDO post : posts) {
            LeadAnalysisResult result = workflowResults.get(post.getId());
            if (result == null) {
                result = buildAiMissingResult("post_lead");
            }
            FbCollectPostDO updateObj = new FbCollectPostDO();
            updateObj.setId(post.getId());
            fillPostAnalysis(updateObj, result);
            collectPostMapper.updateById(updateObj);
        }
        refreshDiscoveryStats(config.getId());
        return posts.size();
    }

    private int analyzeGroupCommentPosts(FbAiAgentConfigDO config, List<Long> scopedPostIds) {
        if (CollUtil.isEmpty(scopedPostIds)) {
            return 0;
        }
        List<FbCollectPostDO> posts = collectPostMapper.selectList(new LambdaQueryWrapper<FbCollectPostDO>()
                .in(FbCollectPostDO::getId, scopedPostIds)
                .and(wrapper -> wrapper.isNull(FbCollectPostDO::getLastAiAnalyzeTime)
                        .or().isNull(FbCollectPostDO::getProductRelevanceScore))
                .orderByAsc(FbCollectPostDO::getId)
                .last("LIMIT " + MAX_ANALYZE_PER_RUN));
        if (CollUtil.isEmpty(posts)) {
            return 0;
        }
        Map<Long, LeadAnalysisResult> workflowResults = analyzePostsByWorkflow(
                config, posts, DEFAULT_GROUP_COMMENT_POST_FILTER_WORKFLOW_CODE, "group_comment_post");
        for (FbCollectPostDO post : posts) {
            LeadAnalysisResult result = workflowResults.get(post.getId());
            if (result == null) {
                result = buildAiMissingResult("group_comment_post");
            }
            FbCollectPostDO updateObj = new FbCollectPostDO();
            updateObj.setId(post.getId());
            fillPostAnalysis(updateObj, result);
            collectPostMapper.updateById(updateObj);
        }
        refreshDiscoveryStats(config.getId());
        return posts.size();
    }

    private int analyzePendingCommentUsers(FbAiAgentConfigDO config, List<Long> scopedLeadIds) {
        return analyzePendingCommentUsers(config, scopedLeadIds, DEFAULT_GROUP_COMMENT_ANALYZE_WORKFLOW_CODE, "comment_lead");
    }

    private int analyzePendingCompetitorCommentUsers(FbAiAgentConfigDO config, List<Long> scopedLeadIds) {
        return analyzePendingCommentUsers(config, scopedLeadIds, DEFAULT_COMPETITOR_COMMENT_ANALYZE_WORKFLOW_CODE, "competitor_comment_lead");
    }

    private int analyzePendingCommentUsers(FbAiAgentConfigDO config, List<Long> scopedLeadIds, String workflowCode, String leadType) {
        if (CollUtil.isEmpty(scopedLeadIds)) {
            return 0;
        }
        List<FbCollectUserDO> users = collectUserMapper.selectList(new LambdaQueryWrapper<FbCollectUserDO>()
                .in(FbCollectUserDO::getId, scopedLeadIds)
                .and(wrapper -> wrapper.isNull(FbCollectUserDO::getLastAiAnalyzeTime)
                        .or().isNull(FbCollectUserDO::getProductRelevanceScore))
                .orderByAsc(FbCollectUserDO::getId)
                .last("LIMIT " + MAX_ANALYZE_PER_RUN));
        if (CollUtil.isEmpty(users)) {
            return 0;
        }
        Map<Long, LeadAnalysisResult> workflowResults = analyzeCommentUsersByWorkflow(config, users, workflowCode, leadType);
        for (FbCollectUserDO user : users) {
            LeadAnalysisResult result = workflowResults.get(user.getId());
            if (result == null) {
                result = buildAiMissingResult(leadType);
            }
            FbCollectUserDO updateObj = new FbCollectUserDO();
            updateObj.setId(user.getId());
            fillUserAnalysis(updateObj, result);
            collectUserMapper.updateById(updateObj);
        }
        refreshDiscoveryStats(config.getId());
        return users.size();
    }

    private int queueHighIntentTouches(FbAiAgentConfigDO config, List<String> accountIds, List<Long> scopedLeadIds) {
        List<Long> discoveryTaskIds = getAgentDiscoveryTaskIds(config.getId());
        if (CollUtil.isEmpty(discoveryTaskIds)) {
            return 0;
        }
        List<Long> leadIds = CollUtil.isNotEmpty(scopedLeadIds) ? scopedLeadIds : getAgentLeadIds(config.getId());
        if (CollUtil.isEmpty(leadIds)) {
            return 0;
        }
        int queued = 0;
        if (Boolean.TRUE.equals(config.getAutoCommentEnabled())) {
            int remaining = Math.min(MAX_TOUCH_QUEUE_PER_RUN - queued, remainingDailyTouchLimit(config.getDailyCommentLimit(), "comment"));
            queued += queuePageCommentTouches(config, accountIds, leadIds, remaining);
        }
        if (queued < MAX_TOUCH_QUEUE_PER_RUN && Boolean.TRUE.equals(config.getAutoDmEnabled())) {
            int remaining = Math.min(MAX_TOUCH_QUEUE_PER_RUN - queued, remainingDailyTouchLimit(config.getDailyDmLimit(), "dm"));
            queued += queueUserDmTouches(config, accountIds, leadIds, remaining);
        }
        return queued;
    }

    private int queuePostHighIntentTouches(FbAiAgentConfigDO config, List<String> accountIds, List<Long> scopedPostIds) {
        List<Long> postIds = CollUtil.isNotEmpty(scopedPostIds) ? scopedPostIds : getAgentPostLeadIds(config.getId());
        if (CollUtil.isEmpty(postIds)) {
            return 0;
        }
        int queued = 0;
        if (Boolean.TRUE.equals(config.getAutoCommentEnabled())) {
            int remaining = Math.min(MAX_TOUCH_QUEUE_PER_RUN - queued, remainingDailyTouchLimit(config.getDailyCommentLimit(), "comment"));
            queued += queuePostCommentTouches(config, accountIds, postIds, remaining);
        }
        if (queued < MAX_TOUCH_QUEUE_PER_RUN && Boolean.TRUE.equals(config.getAutoDmEnabled())) {
            int remaining = Math.min(MAX_TOUCH_QUEUE_PER_RUN - queued, remainingDailyTouchLimit(config.getDailyDmLimit(), "dm"));
            queued += queuePostDmTouches(config, accountIds, postIds, remaining);
        }
        return queued;
    }

    private TouchActivateResult activateDueTouchRecords(FbAiAgentConfigDO config, List<Long> scopedLeadIds) {
        LambdaQueryWrapper<FbAiTouchRecordDO> wrapper = new LambdaQueryWrapper<FbAiTouchRecordDO>()
                .eq(FbAiTouchRecordDO::getAgentConfigId, config.getId())
                .eq(FbAiTouchRecordDO::getStatus, 0)
                .le(FbAiTouchRecordDO::getScheduledTime, LocalDateTime.now())
                .orderByAsc(FbAiTouchRecordDO::getScheduledTime);
        if (CollUtil.isNotEmpty(scopedLeadIds)) {
            wrapper.in(FbAiTouchRecordDO::getLeadId, scopedLeadIds);
        }
        List<FbAiTouchRecordDO> records = touchRecordMapper.selectList(wrapper.last("LIMIT " + MAX_TOUCH_QUEUE_PER_RUN));
        if (CollUtil.isEmpty(records)) {
            return new TouchActivateResult();
        }
        TouchActivateResult result = new TouchActivateResult();
        List<FbAiTouchRecordDO> rawDmRecords = records.stream()
                .filter(record -> "dm".equals(record.getTouchType()))
                .collect(Collectors.toList());
        List<FbAiTouchRecordDO> dmRecords = rawDmRecords.stream()
                .filter(record -> StrUtil.isNotBlank(record.getTargetUserId()))
                .filter(record -> StrUtil.isNotBlank(record.getGeneratedContent()))
                .filter(record -> StrUtil.isNotBlank(record.getAccountId()))
                .collect(Collectors.collectingAndThen(
                        Collectors.toMap(FbAiTouchRecordDO::getTargetUserId, Function.identity(), (a, b) -> a, LinkedHashMap::new),
                        map -> new ArrayList<>(map.values())));
        for (FbAiTouchRecordDO record : rawDmRecords) {
            if (!dmRecords.contains(record)) {
                updateTouchRecordResult(record.getId(), 3, "AI私信目标、话术或账号为空");
                result.failed++;
            }
        }
        if (CollUtil.isNotEmpty(dmRecords)) {
            try {
                Long taskId = createDmOperationTask(config, dmRecords);
                for (FbAiTouchRecordDO record : dmRecords) {
                    markTouchRecordRunning(record.getId(), taskId, null);
                    markLeadTouched(record);
                }
                result.dmTaskCount++;
                result.dmDetailCount += dmRecords.size();
            } catch (Exception ex) {
                log.warn("AI私信触达记录转执行任务失败, count={}, reason={}", dmRecords.size(), ex.getMessage(), ex);
                result.failed += dmRecords.size();
                for (FbAiTouchRecordDO record : dmRecords) {
                    updateTouchRecordResult(record.getId(), 3, ex.getMessage());
                }
            }
        }
        for (FbAiTouchRecordDO record : records) {
            if ("dm".equals(record.getTouchType())) {
                continue;
            }
            try {
                if ("comment".equals(record.getTouchType())) {
                    Long taskId = createCommentOperationTask(record);
                    markTouchRecordRunning(record.getId(), taskId, null);
                    markLeadTouched(record);
                    result.commentTaskCount++;
                    result.commentDetailCount++;
                }
            } catch (Exception ex) {
                log.warn("AI触达记录转执行任务失败, recordId={}, reason={}", record.getId(), ex.getMessage(), ex);
                result.failed++;
                updateTouchRecordResult(record.getId(), 3, ex.getMessage());
            }
        }
        return result;
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

    private Long createDmOperationTask(FbAiAgentConfigDO config, List<FbAiTouchRecordDO> records) {
        if (CollUtil.isEmpty(records)) {
            throw new IllegalArgumentException("AI私信触达记录为空");
        }
        List<Integer> delayRange = parseJsonIntegerList(config.getReplyDelayRange());
        int minDelay = CollUtil.isNotEmpty(delayRange) ? delayRange.get(0) : 180;
        int maxDelay = delayRange.size() > 1 ? delayRange.get(1) : 600;
        if (maxDelay < minDelay) {
            maxDelay = minDelay;
        }

        List<String> targetUserIds = records.stream()
                .map(FbAiTouchRecordDO::getTargetUserId)
                .collect(Collectors.toList());
        List<String> scripts = records.stream()
                .map(FbAiTouchRecordDO::getGeneratedContent)
                .collect(Collectors.toList());
        List<String> accountIds = records.stream()
                .map(FbAiTouchRecordDO::getAccountId)
                .distinct()
                .collect(Collectors.toList());
        if (targetUserIds.isEmpty() || scripts.isEmpty() || accountIds.isEmpty()) {
            throw new IllegalArgumentException("AI私信目标、话术或账号为空");
        }

        FbDmTaskDO task = new FbDmTaskDO();
        task.setTaskName("AI自动私信-" + config.getAgentName());
        task.setTargetUserIds(JSONUtil.toJsonStr(targetUserIds));
        task.setScripts(JSONUtil.toJsonStr(scripts));
        task.setScriptType(1);
        task.setAppendRandomEmoji(false);
        task.setAccountIds(JSONUtil.toJsonStr(accountIds));
        task.setMinIntervalSeconds(minDelay);
        task.setMaxIntervalSeconds(maxDelay);
        task.setStatus(0);
        task.setTotalCount(records.size());
        task.setCompletedCount(0);
        task.setFailedCount(0);
        task.setRemark("AI_AGENT_TOUCH_BATCH:" + config.getId() + ":" +
                records.stream().map(record -> String.valueOf(record.getId())).collect(Collectors.joining(",")));
        dmTaskMapper.insert(task);

        List<FbDmTaskDetailDO> details = new ArrayList<>(records.size());
        for (FbAiTouchRecordDO record : records) {
            FbDmTaskDetailDO detail = new FbDmTaskDetailDO();
            detail.setTaskId(task.getId());
            detail.setAccountId(record.getAccountId());
            detail.setTargetUserId(record.getTargetUserId());
            detail.setScriptContent(record.getGeneratedContent());
            detail.setStatus(0);
            details.add(detail);
        }
        if (CollUtil.isEmpty(details)) {
            throw new IllegalArgumentException("AI私信任务明细为空");
        }
        dmTaskDetailMapper.insertBatch(details);
        for (int i = 0; i < details.size(); i++) {
            collectQueueService.push("dm", details.get(i).getId(), records.get(i).getFbAccount());
        }
        if (!Objects.equals(task.getTotalCount(), details.size())) {
            FbDmTaskDO updateObj = new FbDmTaskDO();
            updateObj.setId(task.getId());
            updateObj.setTotalCount(details.size());
            dmTaskMapper.updateById(updateObj);
        }
        return task.getId();
    }

    private void markTouchRecordRunning(Long recordId, Long taskId, Long detailId) {
        FbAiTouchRecordDO updateObj = new FbAiTouchRecordDO();
        updateObj.setId(recordId);
        updateObj.setStatus(1);
        updateObj.setOperationTaskId(taskId);
        updateObj.setOperationDetailId(detailId);
        touchRecordMapper.updateById(updateObj);
    }

    private int queueUserDmTouches(FbAiAgentConfigDO config, List<String> accountIds, List<Long> leadIds, int limit) {
        if (limit <= 0) {
            return 0;
        }
        int minScore = resolveTouchScoreThreshold(config);
        List<FbCollectUserDO> users = collectUserMapper.selectList(new LambdaQueryWrapper<FbCollectUserDO>()
                .in(FbCollectUserDO::getId, leadIds)
                .ge(FbCollectUserDO::getProductRelevanceScore, minScore)
                .orderByDesc(FbCollectUserDO::getProductRelevanceScore)
                .orderByDesc(FbCollectUserDO::getId)
                .last("LIMIT " + limit));
        if (CollUtil.isEmpty(users)) {
            return 0;
        }
        List<String> touchAccountIds = resolveAgentTouchAccountIds(config, "dm", users.size());
        if (CollUtil.isEmpty(touchAccountIds)) {
            return 0;
        }
        int queued = 0;
        Set<String> queuedTargetUserIds = new HashSet<>();
        int missingTarget = 0;
        int duplicateInBatch = 0;
        int existingLeadTouch = 0;
        int existingTargetTouch = 0;
        for (FbCollectUserDO user : users) {
            if (StrUtil.isBlank(user.getFbUserId())) {
                missingTarget++;
                continue;
            }
            if (!queuedTargetUserIds.add(user.getFbUserId())) {
                duplicateInBatch++;
                continue;
            }
            if (existsTouchRecord("user", user.getId(), "dm")) {
                existingLeadTouch++;
                continue;
            }
            if (existsTouchRecordByTargetUserId(user.getFbUserId(), "dm")) {
                existingTargetTouch++;
                continue;
            }
            String accountId = pickAccount(touchAccountIds, queued);
            FbAiTouchRecordDO record = buildTouchRecord(config, "user", user.getId(), user.getUrl(),
                    user.getFbUserId(), accountId, "dm", buildDmContent(config, user));
            createTouchRecord(record);
            queued++;
        }
        log.debug("AI私信触达排队完成, agentId={}, queued={}, missingTarget={}, duplicateInBatch={}, existingLeadTouch={}, existingTargetTouch={}",
                config.getId(), queued, missingTarget, duplicateInBatch, existingLeadTouch, existingTargetTouch);
        return queued;
    }

    private int queuePageCommentTouches(FbAiAgentConfigDO config, List<String> accountIds, List<Long> leadIds, int limit) {
        if (limit <= 0) {
            return 0;
        }
        int minScore = resolveTouchScoreThreshold(config);
        List<FbCollectUserDO> users = collectUserMapper.selectList(new LambdaQueryWrapper<FbCollectUserDO>()
                .in(FbCollectUserDO::getId, leadIds)
                .ge(FbCollectUserDO::getProductRelevanceScore, minScore)
                .orderByDesc(FbCollectUserDO::getProductRelevanceScore)
                .orderByDesc(FbCollectUserDO::getId)
                .last("LIMIT " + limit));
        if (CollUtil.isEmpty(users)) {
            return 0;
        }
        List<String> touchAccountIds = resolveAgentTouchAccountIds(config, "comment", users.size());
        if (CollUtil.isEmpty(touchAccountIds)) {
            return 0;
        }
        int queued = 0;
        for (FbCollectUserDO user : users) {
            if (StrUtil.isBlank(user.getUrl()) || !isCommentablePostUrl(user.getUrl()) || existsTouchRecord("user", user.getId(), "comment")) {
                continue;
            }
            String accountId = pickAccount(touchAccountIds, queued);
            FbAiTouchRecordDO record = buildTouchRecord(config, "user", user.getId(), user.getUrl(),
                    user.getFbUserId(), accountId, "comment", buildCommentContent(config, user));
            createTouchRecord(record);
            queued++;
        }
        return queued;
    }

    private int queuePostCommentTouches(FbAiAgentConfigDO config, List<String> accountIds, List<Long> postIds, int limit) {
        if (limit <= 0) {
            return 0;
        }
        int minScore = resolveTouchScoreThreshold(config);
        List<FbCollectPostDO> posts = collectPostMapper.selectList(new LambdaQueryWrapper<FbCollectPostDO>()
                .in(FbCollectPostDO::getId, postIds)
                .ge(FbCollectPostDO::getProductRelevanceScore, minScore)
                .isNotNull(FbCollectPostDO::getUrl)
                .orderByDesc(FbCollectPostDO::getProductRelevanceScore)
                .orderByDesc(FbCollectPostDO::getId)
                .last("LIMIT " + limit));
        if (CollUtil.isEmpty(posts)) {
            return 0;
        }
        List<String> touchAccountIds = resolveAgentTouchAccountIds(config, "comment", posts.size());
        if (CollUtil.isEmpty(touchAccountIds)) {
            return 0;
        }
        int queued = 0;
        for (FbCollectPostDO post : posts) {
            if (StrUtil.isBlank(post.getUrl()) || !isCommentablePostUrl(post.getUrl()) || existsTouchRecord("post", post.getId(), "comment")) {
                continue;
            }
            String accountId = pickAccount(touchAccountIds, queued);
            FbAiTouchRecordDO record = buildTouchRecord(config, "post", post.getId(), post.getUrl(),
                    post.getPostAuthorId(), accountId, "comment", buildCommentContent(config, post));
            createTouchRecord(record);
            queued++;
        }
        return queued;
    }

    private int queuePostDmTouches(FbAiAgentConfigDO config, List<String> accountIds, List<Long> postIds, int limit) {
        if (limit <= 0) {
            return 0;
        }
        int minScore = resolveTouchScoreThreshold(config);
        List<FbCollectPostDO> posts = collectPostMapper.selectList(new LambdaQueryWrapper<FbCollectPostDO>()
                .in(FbCollectPostDO::getId, postIds)
                .ge(FbCollectPostDO::getProductRelevanceScore, minScore)
                .isNotNull(FbCollectPostDO::getPostAuthorId)
                .orderByDesc(FbCollectPostDO::getProductRelevanceScore)
                .orderByDesc(FbCollectPostDO::getId)
                .last("LIMIT " + limit));
        if (CollUtil.isEmpty(posts)) {
            return 0;
        }
        List<String> touchAccountIds = resolveAgentTouchAccountIds(config, "dm", posts.size());
        if (CollUtil.isEmpty(touchAccountIds)) {
            return 0;
        }
        int queued = 0;
        Set<String> queuedTargetUserIds = new HashSet<>();
        for (FbCollectPostDO post : posts) {
            if (StrUtil.isBlank(post.getPostAuthorId())) {
                continue;
            }
            if (!queuedTargetUserIds.add(post.getPostAuthorId())) {
                continue;
            }
            if (existsTouchRecord("post", post.getId(), "dm") || existsTouchRecordByTargetUserId(post.getPostAuthorId(), "dm")) {
                continue;
            }
            String accountId = pickAccount(touchAccountIds, queued);
            FbAiTouchRecordDO record = buildTouchRecord(config, "post", post.getId(), post.getPostAuthorUrl(),
                    post.getPostAuthorId(), accountId, "dm", buildDmContent(config, post));
            createTouchRecord(record);
            queued++;
        }
        return queued;
    }

    private int queueCommentLeadDmTouches(FbAiAgentConfigDO config, List<String> accountIds, List<Long> leadIds) {
        if (CollUtil.isEmpty(leadIds) || !Boolean.TRUE.equals(config.getAutoDmEnabled())) {
            return 0;
        }
        int remaining = Math.min(MAX_TOUCH_QUEUE_PER_RUN, remainingDailyTouchLimit(config.getDailyDmLimit(), "dm"));
        if (remaining <= 0) {
            return 0;
        }
        int minScore = resolveTouchScoreThreshold(config);
        List<FbCollectUserDO> users = collectUserMapper.selectList(new LambdaQueryWrapper<FbCollectUserDO>()
                .in(FbCollectUserDO::getId, leadIds)
                .ge(FbCollectUserDO::getProductRelevanceScore, minScore)
                .isNotNull(FbCollectUserDO::getFbUserId)
                .orderByDesc(FbCollectUserDO::getProductRelevanceScore)
                .orderByDesc(FbCollectUserDO::getId)
                .last("LIMIT " + remaining));
        if (CollUtil.isEmpty(users)) {
            return 0;
        }
        List<String> touchAccountIds = resolveAgentTouchAccountIds(config, "dm", users.size());
        if (CollUtil.isEmpty(touchAccountIds)) {
            return 0;
        }
        int queued = 0;
        Set<String> queuedTargetUserIds = new HashSet<>();
        for (FbCollectUserDO user : users) {
            if (StrUtil.isBlank(user.getFbUserId())) {
                continue;
            }
            if (!queuedTargetUserIds.add(user.getFbUserId())) {
                continue;
            }
            if (existsTouchRecord("comment", user.getId(), "dm") || existsTouchRecordByTargetUserId(user.getFbUserId(), "dm")) {
                continue;
            }
            String accountId = pickAccount(touchAccountIds, queued);
            FbAiTouchRecordDO record = buildTouchRecord(config, "comment", user.getId(), user.getUrl(),
                    user.getFbUserId(), accountId, "dm", buildDmContent(config, user));
            createTouchRecord(record);
            queued++;
        }
        return queued;
    }

    private int resolveTouchScoreThreshold(FbAiAgentConfigDO config) {
        return Optional.ofNullable(config.getTouchScoreThreshold()).orElse(95);
    }

    private long countQualifiedLeads(List<Long> leadIds, int threshold) {
        if (CollUtil.isEmpty(leadIds)) {
            return 0;
        }
        Long count = collectUserMapper.selectCount(new LambdaQueryWrapper<FbCollectUserDO>()
                .in(FbCollectUserDO::getId, leadIds)
                .ge(FbCollectUserDO::getProductRelevanceScore, threshold));
        return count == null ? 0 : count;
    }

    /**
     * 运行日志同时显示配置的触达门槛和本批线索实际最高评分，避免把二者混为一谈。
     */
    private String formatUserAnalysisThreshold(List<Long> leadIds, int threshold) {
        int highestScore = 0;
        if (CollUtil.isNotEmpty(leadIds)) {
            List<FbCollectUserDO> users = collectUserMapper.selectList(new LambdaQueryWrapper<FbCollectUserDO>()
                    .in(FbCollectUserDO::getId, leadIds)
                    .select(FbCollectUserDO::getProductRelevanceScore));
            highestScore = users.stream()
                    .map(FbCollectUserDO::getProductRelevanceScore)
                    .filter(Objects::nonNull)
                    .mapToInt(Integer::intValue)
                    .max()
                    .orElse(0);
        }
        return formatAnalysisThreshold(threshold, highestScore);
    }

    private String formatPostAnalysisThreshold(List<Long> postIds, int threshold) {
        int highestScore = 0;
        if (CollUtil.isNotEmpty(postIds)) {
            List<FbCollectPostDO> posts = collectPostMapper.selectList(new LambdaQueryWrapper<FbCollectPostDO>()
                    .in(FbCollectPostDO::getId, postIds)
                    .select(FbCollectPostDO::getProductRelevanceScore));
            highestScore = posts.stream()
                    .map(FbCollectPostDO::getProductRelevanceScore)
                    .filter(Objects::nonNull)
                    .mapToInt(Integer::intValue)
                    .max()
                    .orElse(0);
        }
        return formatAnalysisThreshold(threshold, highestScore);
    }

    private String formatAnalysisThreshold(int threshold, int highestScore) {
        String thresholdText = mapScoreToIntent(threshold) + "/" + threshold;
        if (highestScore <= 0) {
            return thresholdText;
        }
        return thresholdText + "，最高意向" + mapScoreToIntent(highestScore) + "/" + highestScore;
    }

    private long countQualifiedPostLeads(List<Long> postIds, int threshold) {
        if (CollUtil.isEmpty(postIds)) {
            return 0;
        }
        Long count = collectPostMapper.selectCount(new LambdaQueryWrapper<FbCollectPostDO>()
                .in(FbCollectPostDO::getId, postIds)
                .ge(FbCollectPostDO::getProductRelevanceScore, threshold));
        return count == null ? 0 : count;
    }

    private long countQualifiedMissingTargetUsers(List<Long> leadIds, int threshold) {
        if (CollUtil.isEmpty(leadIds)) {
            return 0;
        }
        Long count = collectUserMapper.selectCount(new LambdaQueryWrapper<FbCollectUserDO>()
                .in(FbCollectUserDO::getId, leadIds)
                .ge(FbCollectUserDO::getProductRelevanceScore, threshold)
                .and(wrapper -> wrapper.isNull(FbCollectUserDO::getFbUserId).or().eq(FbCollectUserDO::getFbUserId, "")));
        return count == null ? 0 : count;
    }

    private long countQualifiedExistingTargetTouches(List<Long> leadIds, int threshold) {
        if (CollUtil.isEmpty(leadIds)) {
            return 0;
        }
        List<FbCollectUserDO> users = collectUserMapper.selectList(new LambdaQueryWrapper<FbCollectUserDO>()
                .in(FbCollectUserDO::getId, leadIds)
                .ge(FbCollectUserDO::getProductRelevanceScore, threshold)
                .isNotNull(FbCollectUserDO::getFbUserId)
                .select(FbCollectUserDO::getFbUserId));
        if (CollUtil.isEmpty(users)) {
            return 0;
        }
        long count = 0;
        Set<String> targetIds = users.stream()
                .map(FbCollectUserDO::getFbUserId)
                .filter(StrUtil::isNotBlank)
                .collect(Collectors.toCollection(LinkedHashSet::new));
        for (String targetId : targetIds) {
            if (existsTouchRecordByTargetUserId(targetId, "dm")) {
                count++;
            }
        }
        return count;
    }

    private void addTouchSummaryLog(Long agentConfigId, int queuedTouches, TouchActivateResult activatedTouches,
                                    long qualifiedUsers, long missingTargetUsers, long existingTargetTouches) {
        if (queuedTouches <= 0 && activatedTouches.totalDetails() <= 0) {
            List<String> reasons = new ArrayList<>();
            if (missingTargetUsers > 0) {
                reasons.add("缺Facebook用户ID" + missingTargetUsers + "个");
            }
            if (existingTargetTouches > 0) {
                reasons.add("目标已存在触达记录" + existingTargetTouches + "个");
            }
            String reason = "达标" + qualifiedUsers + "个" +
                    (CollUtil.isEmpty(reasons) ? "" : "，" + String.join("，", reasons)) +
                    "，未生成触达任务";
            addRunLog(agentConfigId, "触达跳过", reason, qualifiedUsers > 0 ? "warning" : "info");
            return;
        }
        addRunLog(agentConfigId, "触达完成", String.format("触达%s条，私信任务%s个，私信%s条，评论任务%s个",
                queuedTouches, activatedTouches.dmTaskCount, activatedTouches.dmDetailCount, activatedTouches.commentTaskCount),
                activatedTouches.failed > 0 ? "warning" : "success");
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
        record.setScheduledTime(LocalDateTime.now());
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

    private boolean existsTouchRecordByTargetUserId(String targetUserId, String touchType) {
        if (StrUtil.isBlank(targetUserId)) {
            return false;
        }
        Long count = touchRecordMapper.selectCount(new LambdaQueryWrapper<FbAiTouchRecordDO>()
                .eq(FbAiTouchRecordDO::getTargetUserId, targetUserId)
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
        return buildSearchTopUrl(keyword, false);
    }

    private String buildSearchTopUrl(String keyword, boolean latestPosts) {
        String url = "https://www.facebook.com/search/top?q=" + URLEncoder.encode(keyword, StandardCharsets.UTF_8);
        return latestPosts ? url + "&filters=" + RECENT_POSTS_FILTER : url;
    }

    private String buildSearchPagesUrl(String keyword) {
        return "https://www.facebook.com/search/pages?q=" + URLEncoder.encode(keyword, StandardCharsets.UTF_8);
    }

    /**
     * 构造 AI 主页/帖子获客的搜索地址。用户提供链接时只替换 q 参数，避免破坏
     * Facebook 搜索链接中的 filters、location 等筛选条件。
     */
    private String buildKeywordSearchUrl(FbAiAgentConfigDO config, String keyword,
                                         boolean pages, boolean latestPosts) {
        if (config != null && "link".equalsIgnoreCase(StrUtil.trim(config.getSearchMode()))
                && StrUtil.isNotBlank(config.getSearchUrlTemplate())) {
            return replaceSearchQueryParameter(config.getSearchUrlTemplate().trim(), "q", keyword);
        }
        return pages ? buildSearchPagesUrl(keyword) : buildSearchTopUrl(keyword, latestPosts);
    }

    private String replaceSearchQueryParameter(String template, String parameter, String value) {
        int queryStart = template.indexOf('?');
        if (queryStart < 0) {
            return template;
        }
        int fragmentStart = template.indexOf('#', queryStart);
        int queryEnd = fragmentStart >= 0 ? fragmentStart : template.length();
        String query = template.substring(queryStart + 1, queryEnd);
        String encodedValue = URLEncoder.encode(value, StandardCharsets.UTF_8);
        String[] parts = query.split("&", -1);
        boolean replaced = false;
        for (int i = 0; i < parts.length; i++) {
            int equals = parts[i].indexOf('=');
            String name = equals >= 0 ? parts[i].substring(0, equals) : parts[i];
            if (parameter.equalsIgnoreCase(name)) {
                parts[i] = name + "=" + encodedValue;
                replaced = true;
                break;
            }
        }
        if (!replaced) {
            return template;
        }
        return template.substring(0, queryStart + 1) + String.join("&", parts)
                + template.substring(queryEnd);
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

    private String normalizeCompetitorPageUrl(String pageUrl) {
        if (StrUtil.isBlank(pageUrl)) {
            return "";
        }
        String raw = pageUrl.trim();
        if (!raw.startsWith("http://") && !raw.startsWith("https://")) {
            raw = "https://www.facebook.com/" + raw;
        }
        int hashIndex = raw.indexOf('#');
        if (hashIndex >= 0) {
            raw = raw.substring(0, hashIndex);
        }
        return raw;
    }

    private String buildCommentContent(FbAiAgentConfigDO config, FbCollectPostDO post) {
        String aiMessage = extractAiSummaryMessage(post.getAiSummary(), "comment_message");
        if (StrUtil.isNotBlank(aiMessage)) {
            return aiMessage;
        }
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

    private String buildDmContent(FbAiAgentConfigDO config, FbCollectPostDO post) {
        String aiMessage = extractAiSummaryMessage(post.getAiSummary(), "dm_message");
        if (StrUtil.isNotBlank(aiMessage)) {
            return aiMessage;
        }
        String name = StrUtil.blankToDefault(post.getPostUser(), "there");
        return "Hi " + name + ", noticed your post and thought our product info might be useful. Happy to share details if needed.";
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
        if (AGENT_TYPE_GROUP_POST.equals(config.getAgentType()) || AGENT_TYPE_POST_LEAD.equals(config.getAgentType())) {
            List<Long> postIds = getAgentPostLeadIds(config.getId());
            if (CollUtil.isEmpty(postIds)) {
                config.setLeadCount(0L);
                config.setPendingCount(0L);
                return;
            }
            List<FbCollectPostDO> posts = collectPostMapper.selectList(new LambdaQueryWrapper<FbCollectPostDO>()
                    .in(FbCollectPostDO::getId, postIds));
            long leadCount = posts.stream()
                    .filter(item -> item.getProductRelevanceScore() != null)
                    .count();
            long pendingCount = posts.stream()
                    .filter(item -> item.getProductRelevanceScore() != null)
                    .filter(item -> StrUtil.equalsAny(StrUtil.blankToDefault(item.getTouchStatus(), "not_touched"),
                            "not_touched", "pending"))
                    .count();
            config.setLeadCount(leadCount);
            config.setPendingCount(pendingCount);
            return;
        }
        List<Long> leadIds = getAgentLeadIds(config.getId());
        if (CollUtil.isEmpty(leadIds)) {
            config.setLeadCount(0L);
            config.setPendingCount(0L);
            return;
        }
        List<FbCollectUserDO> users = collectUserMapper.selectList(new LambdaQueryWrapper<FbCollectUserDO>()
                .in(FbCollectUserDO::getId, leadIds));
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

    private Map<Long, LeadAnalysisResult> analyzePostsByWorkflow(FbAiAgentConfigDO config, List<FbCollectPostDO> posts) {
        String workflowCode = AGENT_TYPE_POST_LEAD.equals(config.getAgentType())
                ? DEFAULT_POST_LEAD_ANALYZE_WORKFLOW_CODE : DEFAULT_GROUP_POST_ANALYZE_WORKFLOW_CODE;
        return analyzePostsByWorkflow(config, posts, workflowCode, "post_lead");
    }

    private Map<Long, LeadAnalysisResult> analyzePostsByWorkflow(FbAiAgentConfigDO config, List<FbCollectPostDO> posts,
                                                                 String workflowCode, String leadType) {
        if (CollUtil.isEmpty(posts)) {
            return Collections.emptyMap();
        }
        Map<Long, LeadAnalysisResult> resultMap = new HashMap<>();
        for (int fromIndex = 0; fromIndex < posts.size(); fromIndex += AI_ANALYZE_BATCH_SIZE) {
            List<FbCollectPostDO> batch = posts.subList(fromIndex, Math.min(fromIndex + AI_ANALYZE_BATCH_SIZE, posts.size()));
            Map<String, Object> params = new LinkedHashMap<>();
            params.put("exportProduct", resolveExportProduct(config, Collections.emptyList()));
            params.put("persona", StrUtil.blankToDefault(config.getPersonaType(), "professional_sales"));
            params.put("needComment", Boolean.TRUE.equals(config.getAutoCommentEnabled()));
            params.put("needDm", Boolean.TRUE.equals(config.getAutoDmEnabled()));
            params.put("touchScoreThreshold", resolveTouchScoreThreshold(config));
            params.put("posts", batch.stream()
                    .map(post -> AGENT_TYPE_POST_LEAD.equals(config.getAgentType())
                            ? buildPostLeadPayload(post) : buildPostPayload(post))
                    .collect(Collectors.toList()));
            Object rawResult = invokeDefaultAiWorkflow(workflowCode, params);
            Map<Long, LeadAnalysisResult> parsed = parseLeadWorkflowResults(rawResult, config.getTouchScoreThreshold(), leadType);
            log.info("AI群帖分析批次完成, agentId={}, fromIndex={}, batchSize={}, parsedCount={}",
                    config.getId(), fromIndex, batch.size(), parsed.size());
            resultMap.putAll(parsed);
        }
        return resultMap;
    }

    private Map<Long, LeadAnalysisResult> analyzeCommentUsersByWorkflow(FbAiAgentConfigDO config, List<FbCollectUserDO> users) {
        return analyzeCommentUsersByWorkflow(config, users, DEFAULT_GROUP_COMMENT_ANALYZE_WORKFLOW_CODE, "comment_lead");
    }

    private Map<Long, LeadAnalysisResult> analyzeCommentUsersByWorkflow(FbAiAgentConfigDO config, List<FbCollectUserDO> users,
                                                                        String workflowCode, String leadType) {
        if (CollUtil.isEmpty(users)) {
            return Collections.emptyMap();
        }
        Map<Long, FbCollectPostDO> postMap = loadSourcePostMap(users);
        Map<Long, LeadAnalysisResult> resultMap = new HashMap<>();
        for (int fromIndex = 0; fromIndex < users.size(); fromIndex += AI_ANALYZE_BATCH_SIZE) {
            List<FbCollectUserDO> batch = users.subList(fromIndex, Math.min(fromIndex + AI_ANALYZE_BATCH_SIZE, users.size()));
            Map<String, Object> params = new LinkedHashMap<>();
            params.put("exportProduct", resolveExportProduct(config, Collections.emptyList()));
            params.put("persona", StrUtil.blankToDefault(config.getPersonaType(), "professional_sales"));
            params.put("needDm", Boolean.TRUE.equals(config.getAutoDmEnabled()));
            params.put("touchScoreThreshold", resolveTouchScoreThreshold(config));
            params.put("comments", batch.stream()
                    .map(user -> buildCommentLeadPayload(user, postMap.get(user.getSourcePostId())))
                    .collect(Collectors.toList()));
            Object rawResult = invokeDefaultAiWorkflow(workflowCode, params);
            Map<Long, LeadAnalysisResult> parsed = parseLeadWorkflowResults(rawResult, config.getTouchScoreThreshold(), leadType);
            log.info("AI群帖评论分析批次完成, agentId={}, fromIndex={}, batchSize={}, parsedCount={}",
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

    private Map<String, Object> buildPostPayload(FbCollectPostDO post) {
        Map<String, Object> item = new LinkedHashMap<>();
        item.put("id", post.getId());
        item.put("postUser", post.getPostUser());
        item.put("groupName", post.getGroupName());
        item.put("postContent", post.getPostContent());
        item.put("postCreateTime", post.getPostCreateTime());
        return item;
    }

    private Map<String, Object> buildPostLeadPayload(FbCollectPostDO post) {
        Map<String, Object> item = new LinkedHashMap<>();
        item.put("id", post.getId());
        item.put("postContent", post.getPostContent());
        return item;
    }

    private Map<String, Object> buildCommentLeadPayload(FbCollectUserDO user, FbCollectPostDO post) {
        Map<String, Object> item = new LinkedHashMap<>();
        item.put("id", user.getId());
        item.put("commentUser", user.getUserName());
        item.put("commentUserId", user.getFbUserId());
        item.put("commentContent", user.getCommentContent());
        item.put("sourcePostUrl", StrUtil.blankToDefault(user.getSourcePostUrl(), post == null ? "" : post.getUrl()));
        item.put("postUser", post == null ? "" : post.getPostUser());
        item.put("groupName", post == null ? "" : post.getGroupName());
        item.put("postContent", post == null ? "" : post.getPostContent());
        item.put("postCreateTime", post == null ? null : post.getPostCreateTime());
        return item;
    }

    private Map<Long, FbCollectPostDO> loadSourcePostMap(List<FbCollectUserDO> users) {
        List<Long> postIds = users.stream()
                .map(FbCollectUserDO::getSourcePostId)
                .filter(Objects::nonNull)
                .distinct()
                .collect(Collectors.toList());
        if (CollUtil.isEmpty(postIds)) {
            return Collections.emptyMap();
        }
        return collectPostMapper.selectBatchIds(postIds).stream()
                .collect(Collectors.toMap(FbCollectPostDO::getId, Function.identity(), (a, b) -> a));
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
        return parseLeadWorkflowResults(rawResult, threshold, "page_lead");
    }

    private Map<Long, LeadAnalysisResult> parseLeadWorkflowResults(Object rawResult, Integer threshold, String leadType) {
        if (rawResult == null) {
            log.info("AI分析结果为空, leadType={}", leadType);
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
            result.leadType = leadType;
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
        log.info("AI分析解析完成, leadType={}, rawRows={}, validRows={}, ids={}",
                leadType, rows.size(), resultMap.size(), resultMap.keySet());
        return resultMap;
    }

    private LeadAnalysisResult buildAiMissingResult() {
        return buildAiMissingResult("page_lead");
    }

    private LeadAnalysisResult buildAiMissingResult(String leadType) {
        LeadAnalysisResult result = new LeadAnalysisResult();
        result.intentCode = "D";
        result.productRelevanceScore = mapIntentToScore(result.intentCode);
        result.intentLevel = buildIntentLevelByIntent(result.intentCode);
        result.leadType = leadType;
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

    private boolean isIntentReachThreshold(String intent, Integer touchScoreThreshold) {
        return mapIntentToScore(intent) >= Optional.ofNullable(touchScoreThreshold).orElse(95);
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

    private List<Long> getAgentLeadIds(Long agentConfigId) {
        List<Long> taskIds = getAgentDiscoveryTaskIds(agentConfigId);
        if (CollUtil.isEmpty(taskIds)) {
            return Collections.emptyList();
        }
        Set<Long> leadIds = new LinkedHashSet<>();
        List<FbCollectUserDO> users = collectUserMapper.selectList(new LambdaQueryWrapper<FbCollectUserDO>()
                .in(FbCollectUserDO::getTaskId, taskIds)
                .select(FbCollectUserDO::getId));
        if (CollUtil.isNotEmpty(users)) {
            users.stream().map(FbCollectUserDO::getId).filter(Objects::nonNull).forEach(leadIds::add);
        }
        List<FbCollectDetailDO> details = collectDetailMapper.selectList(new LambdaQueryWrapper<FbCollectDetailDO>()
                .in(FbCollectDetailDO::getTaskId, taskIds)
                .isNotNull(FbCollectDetailDO::getSourceUserId)
                .select(FbCollectDetailDO::getSourceUserId));
        if (CollUtil.isNotEmpty(details)) {
            details.stream().map(FbCollectDetailDO::getSourceUserId).filter(Objects::nonNull).forEach(leadIds::add);
        }
        return new ArrayList<>(leadIds);
    }

    private List<Long> getAgentPostLeadIds(Long agentConfigId) {
        List<Long> taskIds = getAgentDiscoveryTaskIds(agentConfigId);
        if (CollUtil.isEmpty(taskIds)) {
            return Collections.emptyList();
        }
        return collectPostMapper.selectList(new LambdaQueryWrapper<FbCollectPostDO>()
                        .in(FbCollectPostDO::getTaskId, taskIds)
                        .select(FbCollectPostDO::getId))
                .stream()
                .map(FbCollectPostDO::getId)
                .filter(Objects::nonNull)
                .distinct()
                .collect(Collectors.toList());
    }

    private List<Long> getCollectTaskLeadIds(Long collectTaskId) {
        if (collectTaskId == null) {
            return Collections.emptyList();
        }
        Set<Long> leadIds = new LinkedHashSet<>();
        List<FbCollectUserDO> users = collectUserMapper.selectList(new LambdaQueryWrapper<FbCollectUserDO>()
                .eq(FbCollectUserDO::getTaskId, collectTaskId)
                .select(FbCollectUserDO::getId));
        if (CollUtil.isNotEmpty(users)) {
            users.stream().map(FbCollectUserDO::getId).filter(Objects::nonNull).forEach(leadIds::add);
        }
        List<FbCollectDetailDO> details = collectDetailMapper.selectList(new LambdaQueryWrapper<FbCollectDetailDO>()
                .eq(FbCollectDetailDO::getTaskId, collectTaskId)
                .isNotNull(FbCollectDetailDO::getSourceUserId)
                .select(FbCollectDetailDO::getSourceUserId));
        if (CollUtil.isNotEmpty(details)) {
            details.stream().map(FbCollectDetailDO::getSourceUserId).filter(Objects::nonNull).forEach(leadIds::add);
        }
        return new ArrayList<>(leadIds);
    }

    private List<Long> getCollectTaskPostIds(Long collectTaskId) {
        if (collectTaskId == null) {
            return Collections.emptyList();
        }
        return collectPostMapper.selectList(new LambdaQueryWrapper<FbCollectPostDO>()
                        .eq(FbCollectPostDO::getTaskId, collectTaskId)
                        .select(FbCollectPostDO::getId))
                .stream()
                .map(FbCollectPostDO::getId)
                .filter(Objects::nonNull)
                .distinct()
                .collect(Collectors.toList());
    }

    private FbAiAgentConfigDO findAgentConfigByCollectTaskId(Long collectTaskId) {
        FbAiAgentDiscoveryLogDO logDO = discoveryLogMapper.selectOne(new LambdaQueryWrapper<FbAiAgentDiscoveryLogDO>()
                .eq(FbAiAgentDiscoveryLogDO::getCollectTaskId, collectTaskId)
                .last("LIMIT 1"));
        if (logDO != null && logDO.getAgentConfigId() != null) {
            return agentConfigMapper.selectById(logDO.getAgentConfigId());
        }
        FbCollectDO task = collectMapper.selectById(collectTaskId);
        if (task == null || StrUtil.isBlank(task.getRemark())) {
            return null;
        }
        String agentName = parseAgentNameFromCollectRemark(task.getRemark());
        if (StrUtil.isBlank(agentName)) {
            return null;
        }
        return agentConfigMapper.selectOne(new LambdaQueryWrapper<FbAiAgentConfigDO>()
                .eq(FbAiAgentConfigDO::getAgentName, agentName)
                .in(FbAiAgentConfigDO::getAgentType, Arrays.asList(AGENT_TYPE_PAGE_LEAD, AGENT_TYPE_POST_LEAD, AGENT_TYPE_GROUP_POST, AGENT_TYPE_GROUP_COMMENT, AGENT_TYPE_COMPETITOR_BUYER))
                .last("LIMIT 1"));
    }

    private String parseAgentNameFromCollectRemark(String remark) {
        if (StrUtil.isBlank(remark)) {
            return "";
        }
        if (remark.startsWith("AI主页深度采集:")) {
            String value = remark.substring("AI主页深度采集:".length());
            int index = value.lastIndexOf(":");
            return index > 0 ? value.substring(0, index) : value;
        }
        if (remark.startsWith("AI主页获客:")) {
            String value = remark.substring("AI主页获客:".length());
            int index = value.lastIndexOf(":");
            return index > 0 ? value.substring(0, index) : value;
        }
        if (remark.startsWith("AI帖子获客:")) {
            String value = remark.substring("AI帖子获客:".length());
            int index = value.lastIndexOf(":");
            return index > 0 ? value.substring(0, index) : value;
        }
        if (remark.startsWith("AI群帖获客:")) {
            String value = remark.substring("AI群帖获客:".length());
            int index = value.lastIndexOf(":");
            return index > 0 ? value.substring(0, index) : value;
        }
        if (remark.startsWith("AI群帖评论截流-帖子采集:")) {
            String value = remark.substring("AI群帖评论截流-帖子采集:".length());
            int index = value.lastIndexOf(":");
            return index > 0 ? value.substring(0, index) : value;
        }
        if (remark.startsWith("AI群帖评论截流-评论采集:")) {
            String value = remark.substring("AI群帖评论截流-评论采集:".length());
            int index = value.lastIndexOf(":");
            return index > 0 ? value.substring(0, index) : value;
        }
        if (remark.startsWith("AI竞品监控-帖子采集:")) {
            String value = remark.substring("AI竞品监控-帖子采集:".length());
            int index = value.lastIndexOf(":");
            return index > 0 ? value.substring(0, index) : value;
        }
        if (remark.startsWith("AI竞品监控-评论采集:")) {
            String value = remark.substring("AI竞品监控-评论采集:".length());
            int index = value.lastIndexOf(":");
            return index > 0 ? value.substring(0, index) : value;
        }
        return "";
    }

    private void refreshDiscoveryStats(Long agentConfigId) {
        List<FbAiAgentDiscoveryLogDO> logs = discoveryLogMapper.selectList(new LambdaQueryWrapper<FbAiAgentDiscoveryLogDO>()
                .eq(FbAiAgentDiscoveryLogDO::getAgentConfigId, agentConfigId));
        if (CollUtil.isEmpty(logs)) {
            return;
        }
        FbAiAgentConfigDO config = agentConfigMapper.selectById(agentConfigId);
        int threshold = config == null ? 95 : resolveTouchScoreThreshold(config);
        for (FbAiAgentDiscoveryLogDO logDO : logs) {
            if (logDO.getCollectTaskId() == null) {
                continue;
            }
            if ("group_post".equals(logDO.getSourceType()) || "post_lead".equals(logDO.getSourceType())
                    || "group_comment_post".equals(logDO.getSourceType()) || "competitor_post".equals(logDO.getSourceType())) {
                List<FbCollectPostDO> posts = collectPostMapper.selectList(new LambdaQueryWrapper<FbCollectPostDO>()
                        .eq(FbCollectPostDO::getTaskId, logDO.getCollectTaskId()));
                FbAiAgentDiscoveryLogDO updateObj = new FbAiAgentDiscoveryLogDO();
                updateObj.setId(logDO.getId());
                updateObj.setDiscoveredCount(posts.size());
                updateObj.setPageCollectCount(posts.size());
                long analyzed = posts.stream().filter(item -> item.getLastAiAnalyzeTime() != null).count();
                long qualified = posts.stream()
                        .filter(item -> Optional.ofNullable(item.getProductRelevanceScore()).orElse(0) >= threshold)
                        .count();
                updateObj.setAiAnalyzeCount((int) analyzed);
                updateObj.setHighIntentCount((int) qualified);
                updateObj.setFilteredCount((int) Math.max(posts.size() - qualified, 0));
                updateObj.setFinalLeadCount((int) qualified);
                discoveryLogMapper.updateById(updateObj);
                continue;
            }
            List<FbCollectUserDO> users = collectUserMapper.selectList(new LambdaQueryWrapper<FbCollectUserDO>()
                    .eq(FbCollectUserDO::getTaskId, logDO.getCollectTaskId()));
            FbAiAgentDiscoveryLogDO updateObj = new FbAiAgentDiscoveryLogDO();
            updateObj.setId(logDO.getId());
            updateObj.setDiscoveredCount(users.size());
            updateObj.setPageCollectCount(users.size());
            long analyzed = users.stream().filter(item -> item.getLastAiAnalyzeTime() != null).count();
            long qualified = users.stream()
                    .filter(item -> Optional.ofNullable(item.getProductRelevanceScore()).orElse(0) >= threshold)
                    .count();
            updateObj.setAiAnalyzeCount((int) analyzed);
            updateObj.setHighIntentCount((int) qualified);
            updateObj.setFilteredCount((int) Math.max(users.size() - qualified, 0));
            updateObj.setFinalLeadCount((int) qualified);
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

    /**
     * 采集接口先把本轮保存摘要写入明细 errorMessage，Agent 在采集完成回调中再读取并转成用户可见日志。
     * 这样定时执行也能保留重复数量，不依赖用户当时是否打开采集页面。
     */
    private CollectionSaveSummary getCollectionSaveSummary(Long taskId, int fallbackNewCount) {
        CollectionSaveSummary summary = new CollectionSaveSummary();
        List<FbCollectDetailDO> details = collectDetailMapper.selectListByTaskId(taskId);
        if (CollUtil.isEmpty(details)) {
            summary.receivedCount = fallbackNewCount;
            summary.newCount = fallbackNewCount;
            return summary;
        }

        boolean matched = false;
        for (FbCollectDetailDO detail : details) {
            String message = detail.getErrorMessage();
            if (StrUtil.isBlank(message)) {
                continue;
            }
            Matcher matcher = COLLECTION_SAVE_SUMMARY_PATTERN.matcher(message);
            if (!matcher.find()) {
                continue;
            }
            matched = true;
            summary.receivedCount += Integer.parseInt(matcher.group(1));
            summary.newCount += Integer.parseInt(matcher.group(2));
            summary.duplicateCount += Integer.parseInt(matcher.group(3));
        }

        // 兼容旧明细或非帖子采集明细没有保存摘要的情况。
        if (!matched) {
            summary.receivedCount = fallbackNewCount;
            summary.newCount = fallbackNewCount;
        }
        return summary;
    }

    private static final class CollectionSaveSummary {
        private int receivedCount;
        private int newCount;
        private int duplicateCount;

        private String toLogContent(String suffix) {
            return String.format("本轮采集：接收%s条，新增保存%s条，重复跳过%s条；%s",
                    receivedCount, newCount, duplicateCount, suffix);
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

    private boolean isSupportedAgentType(String agentType) {
        return AGENT_TYPE_PAGE_LEAD.equals(agentType) || AGENT_TYPE_POST_LEAD.equals(agentType) || AGENT_TYPE_GROUP_POST.equals(agentType)
                || AGENT_TYPE_GROUP_COMMENT.equals(agentType) || AGENT_TYPE_COMPETITOR_BUYER.equals(agentType);
    }

    private boolean isGroupMonitorAgent(String agentType) {
        return AGENT_TYPE_GROUP_POST.equals(agentType) || AGENT_TYPE_GROUP_COMMENT.equals(agentType);
    }

    private String getAgentTypeLabel(String agentType) {
        if (AGENT_TYPE_GROUP_COMMENT.equals(agentType)) {
            return "AI群帖评论截流Agent";
        }
        if (AGENT_TYPE_COMPETITOR_BUYER.equals(agentType)) {
            return "AI竞品监控Agent";
        }
        if (AGENT_TYPE_GROUP_POST.equals(agentType)) {
            return "AI群帖获客Agent";
        }
        if (AGENT_TYPE_POST_LEAD.equals(agentType)) {
            return "AI帖子获客Agent";
        }
        return "AI主页获客Agent";
    }

    private List<String> resolveCompetitorPageUrls(FbAiAgentConfigDO config) {
        List<String> urls = new ArrayList<>();
        urls.addAll(parseCsvStringList(config.getMonitorGroupIds()));
        JSONObject competitorConfig = getCompetitorConfig(config);
        Object manualUrls = competitorConfig == null ? null : competitorConfig.get("manualPageUrls");
        if (manualUrls instanceof Collection<?>) {
            ((Collection<?>) manualUrls).stream().map(String::valueOf).forEach(urls::add);
        } else if (manualUrls instanceof CharSequence) {
            Arrays.stream(String.valueOf(manualUrls).split("\\r?\\n|,"))
                    .map(String::trim)
                    .filter(StrUtil::isNotBlank)
                    .forEach(urls::add);
        }
        return urls.stream()
                .map(this::normalizeCompetitorPageUrl)
                .filter(StrUtil::isNotBlank)
                .distinct()
                .collect(Collectors.toList());
    }

    private int resolveCompetitorRecentDays(FbAiAgentConfigDO config) {
        JSONObject competitorConfig = getCompetitorConfig(config);
        Integer recentDays = competitorConfig == null ? null : competitorConfig.getInt("recentDays");
        return recentDays != null && recentDays > 0 ? recentDays : defaultRecentDays(config);
    }

    private JSONObject getCompetitorConfig(FbAiAgentConfigDO config) {
        if (config == null || StrUtil.isBlank(config.getPersonaConfig())) {
            return null;
        }
        try {
            JSONObject persona = JSONUtil.parseObj(config.getPersonaConfig());
            Object competitorConfig = persona.get("competitorConfig");
            if (competitorConfig instanceof JSONObject) {
                return (JSONObject) competitorConfig;
            }
            if (competitorConfig instanceof Map) {
                return JSONUtil.parseObj(competitorConfig);
            }
            if (competitorConfig instanceof CharSequence && JSONUtil.isTypeJSON(String.valueOf(competitorConfig))) {
                return JSONUtil.parseObj(String.valueOf(competitorConfig));
            }
        } catch (Exception ignored) {
            // ignore invalid persona config
        }
        return null;
    }

    private List<String> resolveGroupPostUrls(FbAiAgentConfigDO config) {
        List<String> urls = new ArrayList<>();
        urls.addAll(parseCsvStringList(config.getMonitorGroupIds()));
        JSONObject groupConfig = getGroupPostConfig(config);
        Object manualUrls = groupConfig == null ? null : groupConfig.get("manualGroupUrls");
        if (manualUrls instanceof Collection<?>) {
            ((Collection<?>) manualUrls).stream().map(String::valueOf).forEach(urls::add);
        } else if (manualUrls instanceof CharSequence) {
            Arrays.stream(String.valueOf(manualUrls).split("\\r?\\n|,"))
                    .map(String::trim)
                    .filter(StrUtil::isNotBlank)
                    .forEach(urls::add);
        }
        return urls.stream()
                .map(this::normalizeGroupMonitorUrl)
                .filter(StrUtil::isNotBlank)
                .distinct()
                .collect(Collectors.toList());
    }

    private int resolveGroupPostRecentDays(FbAiAgentConfigDO config) {
        JSONObject groupConfig = getGroupPostConfig(config);
        Integer recentDays = groupConfig == null ? null : groupConfig.getInt("recentDays");
        return recentDays != null && recentDays > 0 ? recentDays : defaultRecentDays(config);
    }

    private int defaultRecentDays(FbAiAgentConfigDO config) {
        return config == null ? 1 : Math.max(parseIntervalDays(config.getExecuteFrequency()), 1);
    }

    private boolean isPostLeadLatestPosts(FbAiAgentConfigDO config) {
        if (config == null || StrUtil.isBlank(config.getPersonaConfig())) {
            return false;
        }
        try {
            JSONObject persona = JSONUtil.parseObj(config.getPersonaConfig());
            JSONObject postConfig = persona.getJSONObject("postLeadConfig");
            return postConfig != null && Boolean.TRUE.equals(postConfig.getBool("latestPosts"));
        } catch (Exception ignored) {
            return false;
        }
    }

    private JSONObject getGroupPostConfig(FbAiAgentConfigDO config) {
        if (config == null || StrUtil.isBlank(config.getPersonaConfig())) {
            return null;
        }
        try {
            JSONObject persona = JSONUtil.parseObj(config.getPersonaConfig());
            Object groupPostConfig = persona.get("groupPostConfig");
            if (groupPostConfig instanceof JSONObject) {
                return (JSONObject) groupPostConfig;
            }
            if (groupPostConfig instanceof Map) {
                return JSONUtil.parseObj(groupPostConfig);
            }
            if (groupPostConfig instanceof CharSequence && JSONUtil.isTypeJSON(String.valueOf(groupPostConfig))) {
                return JSONUtil.parseObj(String.valueOf(groupPostConfig));
            }
        } catch (Exception ignored) {
            // ignore invalid persona config
        }
        return null;
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
            reqVO.setExecuteFrequency("1");
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
        if (!isSupportedAgentType(reqVO.getAgentType())) {
            throw exception0(2_011_000_001, "当前版本仅支持AI主页获客、AI帖子获客、AI群帖获客、AI群帖评论截流和AI竞品监控");
        }
        if (parseIntervalDays(reqVO.getExecuteFrequency()) < 1) {
            throw exception0(2_011_000_009, "执行间隔只能配置为1到7天");
        }
        if (AGENT_TYPE_COMPETITOR_BUYER.equals(reqVO.getAgentType())) {
            if (CollUtil.isEmpty(resolveCompetitorPageUrls(BeanUtils.toBean(reqVO, FbAiAgentConfigDO.class)))) {
                throw exception0(2_011_000_008, "启用AI竞品监控前请配置竞品主页");
            }
            if (isManualAccountSelection(reqVO) && StrUtil.isBlank(reqVO.getAccountIds())) {
                throw exception0(2_011_000_003, "启用Agent前请选择执行账号池");
            }
            if (StrUtil.isBlank(reqVO.getExportProduct())) {
                throw exception0(2_011_000_006, "启用Agent前请配置主营/出口产品");
            }
            if (!isValidExecuteTime(reqVO.getExecuteTime())) {
                throw exception0(2_011_000_007, "执行时间格式不正确，请使用 HH:mm");
            }
            return;
        }
        if (isGroupMonitorAgent(reqVO.getAgentType())) {
            if (StrUtil.isBlank(reqVO.getMonitorGroupIds()) && CollUtil.isEmpty(resolveGroupPostUrls(BeanUtils.toBean(reqVO, FbAiAgentConfigDO.class)))) {
                throw exception0(2_011_000_008, "启用群组型Agent前请配置监控群组");
            }
            if (isManualAccountSelection(reqVO) && StrUtil.isBlank(reqVO.getAccountIds())) {
                throw exception0(2_011_000_003, "启用Agent前请选择执行账号池");
            }
            if (StrUtil.isBlank(reqVO.getExportProduct())) {
                throw exception0(2_011_000_006, "启用Agent前请配置主营/出口产品");
            }
            if (!isValidExecuteTime(reqVO.getExecuteTime())) {
                throw exception0(2_011_000_007, "执行时间格式不正确，请使用 HH:mm");
            }
            return;
        }
        if (StrUtil.isBlank(reqVO.getSeedKeywords()) && StrUtil.isBlank(reqVO.getKeywordPool())) {
            throw exception0(2_011_000_002, "启用Agent前请配置关键词");
        }
        if ("link".equalsIgnoreCase(reqVO.getSearchMode())
                && !isGroupMonitorAgent(reqVO.getAgentType())
                && !hasSearchKeywordParameter(reqVO.getSearchUrlTemplate())) {
            throw exception0(2_011_000_011, "链接搜索模式下请填写包含 q 参数的 Facebook 搜索结果链接");
        }
        if (isManualAccountSelection(reqVO) && StrUtil.isBlank(reqVO.getAccountIds())) {
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

    private boolean isManualAccountSelection(FbAiAgentConfigSaveReqVO reqVO) {
        return reqVO == null || !"AUTO".equalsIgnoreCase(reqVO.getAccountSelectionMode());
    }

    private boolean isValidExecuteTime(String executeTime) {
        try {
            LocalTime.parse(executeTime);
            return true;
        } catch (Exception ex) {
            return false;
        }
    }

    private boolean hasSearchKeywordParameter(String searchUrl) {
        if (StrUtil.isBlank(searchUrl)) {
            return false;
        }
        try {
            URI uri = URI.create(searchUrl.trim());
            String host = uri.getHost();
            if (host == null || !("facebook.com".equalsIgnoreCase(host)
                    || host.toLowerCase(Locale.ROOT).endsWith(".facebook.com"))) {
                return false;
            }
            String rawQuery = uri.getRawQuery();
            if (rawQuery == null) {
                return false;
            }
            return Arrays.stream(rawQuery.split("&"))
                    .map(item -> item.split("=", 2)[0])
                    .anyMatch("q"::equalsIgnoreCase);
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

    private static class TouchActivateResult {
        private int dmTaskCount;
        private int dmDetailCount;
        private int commentTaskCount;
        private int commentDetailCount;
        private int failed;

        private int totalDetails() {
            return dmDetailCount + commentDetailCount;
        }
    }

    @AllArgsConstructor
    private static class AgentDueResult {
        private boolean due;
        private String reason;
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
