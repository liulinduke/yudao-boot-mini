package cn.iocoder.yudao.module.facebook.service.collectdetail;

import cn.hutool.core.collection.CollUtil;
import cn.hutool.core.util.StrUtil;
import cn.iocoder.yudao.module.facebook.controller.admin.collectdetail.vo.FbCollectPendingDetailRespVO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.account.FbAccountDO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.collect.FbCollectDO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.collectdetail.FbCollectDetailDO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.agent.FbAiAgentConfigDO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.agent.FbAiAgentDiscoveryLogDO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.dmtask.FbDmTaskDO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.dmtask.FbDmTaskDetailDO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.operation.FbOperationTaskDO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.operation.FbOperationTaskDetailDO;
import cn.iocoder.yudao.module.facebook.dal.mysql.account.FbAccountMapper;
import cn.iocoder.yudao.module.facebook.dal.mysql.agent.FbAiAgentConfigMapper;
import cn.iocoder.yudao.module.facebook.dal.mysql.agent.FbAiAgentDiscoveryLogMapper;
import cn.iocoder.yudao.module.facebook.dal.mysql.collect.FbCollectMapper;
import cn.iocoder.yudao.module.facebook.dal.mysql.collectdetail.FbCollectDetailMapper;
import cn.iocoder.yudao.module.facebook.dal.mysql.dmtask.FbDmTaskDetailMapper;
import cn.iocoder.yudao.module.facebook.dal.mysql.dmtask.FbDmTaskMapper;
import cn.iocoder.yudao.module.facebook.dal.mysql.operation.FbOperationTaskDetailMapper;
import cn.iocoder.yudao.module.facebook.dal.mysql.operation.FbOperationTaskMapper;
import cn.iocoder.yudao.module.facebook.service.agent.FbAccountTaskQueueItem;
import cn.iocoder.yudao.module.facebook.service.agent.FbAiAgentCollectQueueService;
import cn.iocoder.yudao.module.facebook.service.account.FbAccountActionStatService;
import com.baomidou.mybatisplus.core.conditions.query.LambdaQueryWrapper;
import org.springframework.stereotype.Service;
import org.springframework.validation.annotation.Validated;
import org.springframework.data.redis.core.StringRedisTemplate;

import jakarta.annotation.Resource;
import java.time.LocalDateTime;
import java.util.*;
import java.util.function.Function;
import java.util.stream.Collectors;

/**
 * FB采集任务明细 Service 实现类
 *
 * @author jacky
 */
@Service
@Validated
public class FbCollectDetailServiceImpl implements FbCollectDetailService {

    private static final String COMMENT_LIKE_CONFIG_KEY_PREFIX = "facebook:collect:comment-like-config:";

    @Resource
    private FbCollectDetailMapper fbCollectDetailMapper;
    @Resource
    private FbCollectMapper fbCollectMapper;
    @Resource
    private FbAccountMapper fbAccountMapper;
    @Resource
    private FbAiAgentCollectQueueService aiAgentCollectQueueService;
    @Resource
    private FbAccountActionStatService actionStatService;
    @Resource
    private FbDmTaskMapper dmTaskMapper;
    @Resource
    private FbDmTaskDetailMapper dmTaskDetailMapper;
    @Resource
    private FbOperationTaskMapper operationTaskMapper;
    @Resource
    private FbOperationTaskDetailMapper operationTaskDetailMapper;
    @Resource
    private FbAiAgentDiscoveryLogMapper discoveryLogMapper;
    @Resource
    private FbAiAgentConfigMapper agentConfigMapper;

    @Resource
    private StringRedisTemplate stringRedisTemplate;
    @Override
    public List<FbCollectDetailDO> getPendingDetailsByAccount(String fbAccount, Long taskId) {
        LambdaQueryWrapper<FbCollectDetailDO> wrapper = new LambdaQueryWrapper<FbCollectDetailDO>()
                .eq(FbCollectDetailDO::getFbAccount, fbAccount)
                .eq(FbCollectDetailDO::getStatus, 0);
        if (taskId != null) {
            wrapper.eq(FbCollectDetailDO::getTaskId, taskId);
        }
        wrapper.orderByAsc(FbCollectDetailDO::getId).last("LIMIT 1");
        return fbCollectDetailMapper.selectList(wrapper);
    }

    @Override
    public FbCollectDetailDO getDetail(Long id) {
        return fbCollectDetailMapper.selectById(id);
    }

    @Override
    public List<FbCollectDetailDO> getDetailListByTaskId(Long taskId) {
        return fbCollectDetailMapper.selectList(
            new LambdaQueryWrapper<FbCollectDetailDO>()
                .eq(FbCollectDetailDO::getTaskId, taskId)
                .orderByAsc(FbCollectDetailDO::getId)
        );
    }

    @Override
    public List<FbCollectPendingDetailRespVO> claimPendingDetails(Integer limit, List<String> excludeAccounts) {
        List<FbAccountTaskQueueItem> queueItems = aiAgentCollectQueueService.popItems(limit, normalizeAccounts(excludeAccounts).stream().collect(Collectors.toList()));
        if (CollUtil.isEmpty(queueItems)) {
            return List.of();
        }
        List<FbCollectPendingDetailRespVO> result = new ArrayList<>();
        List<FbAccountTaskQueueItem> collectItems = queueItems.stream()
                .filter(item -> !"dm".equals(item.getSourceType()) && !"operation".equals(item.getSourceType()))
                .collect(Collectors.toList());
        if (CollUtil.isNotEmpty(collectItems)) {
            result.addAll(buildCollectQueueItems(collectItems));
        }
        List<FbAccountTaskQueueItem> dmItems = queueItems.stream()
                .filter(item -> "dm".equals(item.getSourceType()))
                .collect(Collectors.toList());
        if (CollUtil.isNotEmpty(dmItems)) {
            result.addAll(buildDmQueueItems(dmItems));
        }
        List<FbAccountTaskQueueItem> operationItems = queueItems.stream()
                .filter(item -> "operation".equals(item.getSourceType()))
                .collect(Collectors.toList());
        if (CollUtil.isNotEmpty(operationItems)) {
            result.addAll(buildOperationQueueItems(operationItems));
        }
        return result;
    }

    private List<FbCollectPendingDetailRespVO> buildCollectQueueItems(List<FbAccountTaskQueueItem> queueItems) {
        List<Long> detailIds = queueItems.stream()
                .map(FbAccountTaskQueueItem::getDetailId)
                .collect(Collectors.toList());
        Map<Long, FbCollectDetailDO> detailMap = fbCollectDetailMapper.selectBatchIds(detailIds).stream()
                .filter(detail -> detail != null && Objects.equals(detail.getStatus(), 0))
                .collect(Collectors.toMap(FbCollectDetailDO::getId, Function.identity(), (a, b) -> a));
        List<FbCollectDetailDO> details = new ArrayList<>();
        for (FbAccountTaskQueueItem queueItem : queueItems) {
            FbCollectDetailDO detail = detailMap.get(queueItem.getDetailId());
            if (detail == null) {
                aiAgentCollectQueueService.releaseRunning(queueItem.getFbAccount());
                continue;
            }
            details.add(detail);
        }
        if (CollUtil.isEmpty(details)) {
            return List.of();
        }

        List<Long> taskIds = details.stream()
                .map(FbCollectDetailDO::getTaskId)
                .filter(Objects::nonNull)
                .distinct()
                .collect(Collectors.toList());
        Map<Long, FbCollectDO> taskMap = fbCollectMapper.selectBatchIds(taskIds).stream()
                .collect(Collectors.toMap(FbCollectDO::getId, Function.identity(), (a, b) -> a));

        List<String> fbAccounts = details.stream()
                .map(FbCollectDetailDO::getFbAccount)
                .filter(Objects::nonNull)
                .distinct()
                .collect(Collectors.toList());
        Map<String, FbAccountDO> accountMap = fbAccountMapper.selectList(new LambdaQueryWrapper<FbAccountDO>()
                        .in(FbAccountDO::getFbAccount, fbAccounts))
                .stream()
                .collect(Collectors.toMap(FbAccountDO::getFbAccount, Function.identity(), (a, b) -> a));

        List<FbCollectPendingDetailRespVO> result = new ArrayList<>();
        LocalDateTime now = LocalDateTime.now();
        for (FbCollectDetailDO detail : details) {
            FbCollectDetailDO updateObj = new FbCollectDetailDO();
            updateObj.setId(detail.getId());
            updateObj.setStatus(1);
            updateObj.setStartTime(now);
            fbCollectDetailMapper.updateById(updateObj);

            FbCollectDO task = taskMap.get(detail.getTaskId());
            FbAccountDO account = accountMap.get(detail.getFbAccount());
            if (account != null) actionStatService.markStarted(account.getId(), "collect");
            FbCollectPendingDetailRespVO item = buildPendingDetailResp(detail, task, account);
            item.setSourceType("collect");
            item.setActionConfig(buildCollectRuntimeConfig(detail, task));
            result.add(item);
        }
        return result;
    }

    private List<FbCollectPendingDetailRespVO> buildDmQueueItems(List<FbAccountTaskQueueItem> queueItems) {
        List<Long> detailIds = queueItems.stream().map(FbAccountTaskQueueItem::getDetailId).collect(Collectors.toList());
        Map<Long, FbDmTaskDetailDO> detailMap = dmTaskDetailMapper.selectBatchIds(detailIds).stream()
                .filter(detail -> detail != null && Objects.equals(detail.getStatus(), 0))
                .collect(Collectors.toMap(FbDmTaskDetailDO::getId, Function.identity(), (a, b) -> a));
        if (detailMap.isEmpty()) {
            return List.of();
        }
        List<Long> taskIds = detailMap.values().stream()
                .map(FbDmTaskDetailDO::getTaskId)
                .filter(Objects::nonNull)
                .distinct()
                .collect(Collectors.toList());
        Map<Long, FbDmTaskDO> taskMap = dmTaskMapper.selectBatchIds(taskIds).stream()
                .collect(Collectors.toMap(FbDmTaskDO::getId, Function.identity(), (a, b) -> a));
        List<String> accountIds = detailMap.values().stream()
                .map(FbDmTaskDetailDO::getAccountId)
                .filter(Objects::nonNull)
                .distinct()
                .collect(Collectors.toList());
        List<Long> accountIdLongs = accountIds.stream()
                .map(this::parseLongOrNull)
                .filter(Objects::nonNull)
                .collect(Collectors.toList());
        Map<String, FbAccountDO> accountMap = CollUtil.isEmpty(accountIdLongs) ? Collections.emptyMap()
                : fbAccountMapper.selectBatchIds(accountIdLongs)
                .stream()
                .collect(Collectors.toMap(account -> String.valueOf(account.getId()), Function.identity(), (a, b) -> a));
        List<FbCollectPendingDetailRespVO> result = new ArrayList<>();
        for (FbAccountTaskQueueItem queueItem : queueItems) {
            FbDmTaskDetailDO detail = detailMap.get(queueItem.getDetailId());
            if (detail == null) {
                aiAgentCollectQueueService.releaseRunning(queueItem.getFbAccount());
                continue;
            }
            FbDmTaskDO task = taskMap.get(detail.getTaskId());
            FbAccountDO account = accountMap.get(detail.getAccountId());
            FbCollectPendingDetailRespVO item = new FbCollectPendingDetailRespVO();
            item.setSourceType("dm");
            item.setTaskId(detail.getTaskId());
            item.setDetailId(detail.getId());
            item.setAccountId(detail.getAccountId());
            item.setFbAccount(account == null ? queueItem.getFbAccount() : account.getFbAccount());
            item.setCookie(account == null ? null : account.getCookie());
            item.setPassword(account == null ? null : account.getPassword());
            item.setTfa(account == null ? null : account.getTfa());
            item.setTaskType(14);
            item.setTargetUserId(detail.getTargetUserId());
            item.setScriptContent(detail.getScriptContent());
            item.setMinIntervalSeconds(task == null ? 4 : task.getMinIntervalSeconds());
            item.setMaxIntervalSeconds(task == null ? 10 : task.getMaxIntervalSeconds());
            result.add(item);
        }
        return result;
    }

    private List<FbCollectPendingDetailRespVO> buildOperationQueueItems(List<FbAccountTaskQueueItem> queueItems) {
        List<Long> detailIds = queueItems.stream().map(FbAccountTaskQueueItem::getDetailId).collect(Collectors.toList());
        Map<Long, FbOperationTaskDetailDO> detailMap = operationTaskDetailMapper.selectBatchIds(detailIds).stream()
                .filter(detail -> detail != null && Objects.equals(detail.getStatus(), 0))
                .collect(Collectors.toMap(FbOperationTaskDetailDO::getId, Function.identity(), (a, b) -> a));
        if (detailMap.isEmpty()) {
            queueItems.forEach(item -> aiAgentCollectQueueService.releaseRunning(item.getFbAccount()));
            return List.of();
        }
        List<Long> taskIds = detailMap.values().stream()
                .map(FbOperationTaskDetailDO::getTaskId)
                .filter(Objects::nonNull)
                .distinct()
                .collect(Collectors.toList());
        Map<Long, FbOperationTaskDO> taskMap = operationTaskMapper.selectBatchIds(taskIds).stream()
                .collect(Collectors.toMap(FbOperationTaskDO::getId, Function.identity(), (a, b) -> a));
        List<Long> accountIds = detailMap.values().stream()
                .map(FbOperationTaskDetailDO::getAccountId)
                .filter(Objects::nonNull)
                .map(this::parseLongOrNull)
                .filter(Objects::nonNull)
                .distinct()
                .collect(Collectors.toList());
        Map<String, FbAccountDO> accountMap = CollUtil.isEmpty(accountIds) ? Collections.emptyMap()
                : fbAccountMapper.selectBatchIds(accountIds).stream()
                .collect(Collectors.toMap(account -> String.valueOf(account.getId()), Function.identity(), (a, b) -> a));

        List<FbCollectPendingDetailRespVO> result = new ArrayList<>();
        LocalDateTime now = LocalDateTime.now();
        for (FbAccountTaskQueueItem queueItem : queueItems) {
            FbOperationTaskDetailDO detail = detailMap.get(queueItem.getDetailId());
            if (detail == null) {
                aiAgentCollectQueueService.releaseRunning(queueItem.getFbAccount());
                continue;
            }
            FbOperationTaskDO task = taskMap.get(detail.getTaskId());
            FbAccountDO account = accountMap.get(detail.getAccountId());

            FbOperationTaskDetailDO updateObj = new FbOperationTaskDetailDO();
            updateObj.setId(detail.getId());
            updateObj.setStatus(1);
            updateObj.setStartTime(now);
            operationTaskDetailMapper.updateById(updateObj);

            // 主任务的开始时间以第一条明细真正被领取执行为准。
            if (task != null && task.getStartTime() == null) {
                FbOperationTaskDO taskUpdate = new FbOperationTaskDO();
                taskUpdate.setId(task.getId());
                taskUpdate.setStartTime(now);
                if (Objects.equals(task.getStatus(), 0)) {
                    taskUpdate.setStatus(1);
                }
                operationTaskMapper.updateById(taskUpdate);
            }

            FbCollectPendingDetailRespVO item = new FbCollectPendingDetailRespVO();
            item.setSourceType("operation");
            item.setTaskId(detail.getTaskId());
            item.setDetailId(detail.getId());
            item.setAccountId(detail.getAccountId());
            item.setFbAccount(StrUtil.blankToDefault(detail.getFbAccount(), queueItem.getFbAccount()));
            item.setCookie(account == null ? null : account.getCookie());
            item.setPassword(account == null ? null : account.getPassword());
            item.setTfa(account == null ? null : account.getTfa());
            item.setTaskType(task == null ? 10 : task.getTaskType());
            String startUrl = resolveOperationStartUrl(detail, task);
            item.setSearchUrl(startUrl);
            item.setExpectedCount(detail.getExpectedCount() == null ? 1 : detail.getExpectedCount());
            item.setActionConfig(buildOperationRuntimeConfig(detail, task, startUrl));
            result.add(item);
        }
        return result;
    }

    private String buildOperationRuntimeConfig(FbOperationTaskDetailDO detail, FbOperationTaskDO task, String startUrl) {
        Integer taskType = task == null ? null : task.getTaskType();
        String actionConfig = StrUtil.blankToDefault(detail.getActionConfig(), task == null ? null : task.getActionConfig());
        if (Integer.valueOf(9).equals(taskType) || Integer.valueOf(13).equals(taskType)) {
            return StrUtil.blankToDefault(actionConfig, "{}");
        }
        cn.hutool.json.JSONObject runtimeConfig = cn.hutool.json.JSONUtil.createObj();
        runtimeConfig.set("taskId", detail.getTaskId() == null ? null : String.valueOf(detail.getTaskId()));
        runtimeConfig.set("detailId", detail.getId() == null ? null : String.valueOf(detail.getId()));
        runtimeConfig.set("postUrl", StrUtil.blankToDefault(detail.getPostUrl(), startUrl));
        runtimeConfig.set("targetUrl", StrUtil.blankToDefault(detail.getPostUrl(), startUrl));
        if (StrUtil.isBlank(actionConfig)) {
            runtimeConfig.set("actionConfig", cn.hutool.json.JSONUtil.createObj());
            return runtimeConfig.toString();
        }
        try {
            runtimeConfig.set("actionConfig", cn.hutool.json.JSONUtil.parseObj(actionConfig));
        } catch (Exception ignored) {
            runtimeConfig.set("actionConfig", actionConfig);
        }
        return runtimeConfig.toString();
    }

    private String resolveOperationStartUrl(FbOperationTaskDetailDO detail, FbOperationTaskDO task) {
        if (StrUtil.isNotBlank(detail.getPostUrl())) {
            return detail.getPostUrl();
        }
        if (StrUtil.isNotBlank(detail.getTargetUrls())) {
            return detail.getTargetUrls().split("\\r?\\n")[0].trim();
        }
        if (StrUtil.isNotBlank(detail.getActionConfig())) {
            String url = parseOperationUrl(detail.getActionConfig());
            if (StrUtil.isNotBlank(url)) {
                return url;
            }
        }
        return task == null ? null : parseOperationUrl(task.getActionConfig());
    }

    private String parseOperationUrl(String actionConfig) {
        if (StrUtil.isBlank(actionConfig)) {
            return null;
        }
        try {
            cn.hutool.json.JSONObject config = cn.hutool.json.JSONUtil.parseObj(actionConfig);
            String url = StrUtil.blankToDefault(config.getStr("postUrl"), config.getStr("targetUrl"));
            if (StrUtil.isNotBlank(url)) {
                return url;
            }
            cn.hutool.json.JSONArray postUrls = config.getJSONArray("postUrls");
            if (postUrls != null && !postUrls.isEmpty()) {
                return String.valueOf(postUrls.get(0));
            }
            cn.hutool.json.JSONArray groups = config.getJSONArray("groups");
            if (groups != null && !groups.isEmpty()) {
                Object first = groups.get(0);
                if (first instanceof cn.hutool.json.JSONObject) {
                    return ((cn.hutool.json.JSONObject) first).getStr("groupUrl");
                }
            }
            cn.hutool.json.JSONArray selectedGroups = config.getJSONArray("selectedGroups");
            String selectedGroupUrl = parseFirstGroupUrl(selectedGroups);
            if (StrUtil.isNotBlank(selectedGroupUrl)) {
                return selectedGroupUrl;
            }
            cn.hutool.json.JSONArray selectedUnjoinedGroups = config.getJSONArray("selectedUnjoinedGroups");
            String selectedUnjoinedGroupUrl = parseFirstGroupUrl(selectedUnjoinedGroups);
            if (StrUtil.isNotBlank(selectedUnjoinedGroupUrl)) {
                return selectedUnjoinedGroupUrl;
            }
        } catch (Exception ignored) {
            // ignore invalid operation config
        }
        return null;
    }

    private String parseFirstGroupUrl(cn.hutool.json.JSONArray groups) {
        if (groups == null || groups.isEmpty()) {
            return null;
        }
        Object first = groups.get(0);
        if (first instanceof cn.hutool.json.JSONObject) {
            cn.hutool.json.JSONObject group = (cn.hutool.json.JSONObject) first;
            return StrUtil.blankToDefault(group.getStr("groupUrl"), group.getStr("url"));
        }
        return null;
    }

    private Long parseLongOrNull(String value) {
        try {
            return value == null ? null : Long.valueOf(value.trim());
        } catch (Exception ex) {
            return null;
        }
    }

    private Set<String> normalizeAccounts(List<String> accounts) {
        if (CollUtil.isEmpty(accounts)) {
            return Set.of();
        }
        Set<String> result = new HashSet<>();
        for (String account : accounts) {
            if (account == null) {
                continue;
            }
            for (String item : account.split(",")) {
                String value = item.trim();
                if (!value.isEmpty()) {
                    result.add(value);
                }
            }
        }
        return result;
    }

    @Override
    public FbCollectPendingDetailRespVO claimNextDetail(String fbAccount, Long taskId) {
        List<FbCollectDetailDO> details = getPendingDetailsByAccount(fbAccount, taskId);
        if (CollUtil.isEmpty(details)) {
            return null;
        }
        FbCollectDetailDO detail = details.get(0);
        FbCollectDetailDO updateObj = new FbCollectDetailDO();
        updateObj.setId(detail.getId());
        updateObj.setStatus(1);
        updateObj.setStartTime(LocalDateTime.now());
        fbCollectDetailMapper.updateById(updateObj);
        aiAgentCollectQueueService.remove(detail.getId(), detail.getFbAccount());

        FbCollectDO task = fbCollectMapper.selectById(detail.getTaskId());
        FbAccountDO account = fbAccountMapper.selectOne(new LambdaQueryWrapper<FbAccountDO>()
                .eq(FbAccountDO::getFbAccount, detail.getFbAccount())
                .last("LIMIT 1"));
        if (account != null) actionStatService.markStarted(account.getId(), "collect");
        FbCollectPendingDetailRespVO item = buildPendingDetailResp(detail, task, account);
        item.setActionConfig(buildCollectRuntimeConfig(detail, task));
        return item;
    }

    @Override
    public void markDetailFailed(Long detailId, String errorMessage) {
        if (detailId == null) {
            return;
        }
        FbCollectDetailDO detail = fbCollectDetailMapper.selectById(detailId);
        if (detail == null || Objects.equals(detail.getStatus(), 2) || Objects.equals(detail.getStatus(), 3)) {
            return;
        }
        FbCollectDetailDO updateObj = new FbCollectDetailDO();
        updateObj.setId(detailId);
        updateObj.setStatus(3);
        updateObj.setErrorMessage(StrUtil.blankToDefault(errorMessage, "采集执行超时"));
        updateObj.setEndTime(LocalDateTime.now());
        fbCollectDetailMapper.updateById(updateObj);
        aiAgentCollectQueueService.releaseRunning(detail.getFbAccount());
        updateTaskStatusAfterDetailFailure(detail.getTaskId(), updateObj.getErrorMessage());
    }

    /**
     * 明细失败后同步聚合主任务状态，避免所有明细失败时主任务仍显示为采集中。
     */
    private void updateTaskStatusAfterDetailFailure(Long taskId, String errorMessage) {
        if (taskId == null) {
            return;
        }
        List<FbCollectDetailDO> details = fbCollectDetailMapper.selectListByTaskId(taskId);
        if (details.isEmpty()) {
            return;
        }

        long unfinishedCount = details.stream()
                .filter(detail -> Objects.equals(detail.getStatus(), 0) || Objects.equals(detail.getStatus(), 1))
                .count();
        long failedCount = details.stream()
                .filter(detail -> Objects.equals(detail.getStatus(), 3))
                .count();
        int totalExpected = details.stream()
                .map(FbCollectDetailDO::getExpectedCount)
                .filter(Objects::nonNull)
                .mapToInt(Integer::intValue)
                .sum();
        int totalCollected = details.stream()
                .map(FbCollectDetailDO::getCollectedCount)
                .filter(Objects::nonNull)
                .mapToInt(Integer::intValue)
                .sum();

        FbCollectDO task = new FbCollectDO();
        task.setId(taskId);
        task.setTotalExpectedCount(totalExpected);
        task.setTotalCollectedCount(totalCollected);
        if (unfinishedCount > 0) {
            task.setStatus(1); // 仍有明细待执行或执行中
        } else {
            task.setStatus(failedCount == details.size() ? 3 : 2);
            task.setEndTime(LocalDateTime.now());
            if (failedCount == details.size()) {
                task.setErrorMessage(StrUtil.blankToDefault(errorMessage, "所有采集明细均执行失败"));
            }
        }
        fbCollectMapper.updateById(task);
    }

    private FbCollectPendingDetailRespVO buildPendingDetailResp(FbCollectDetailDO detail, FbCollectDO task, FbAccountDO account) {
        FbCollectPendingDetailRespVO item = new FbCollectPendingDetailRespVO();
        item.setTaskId(detail.getTaskId());
        item.setDetailId(detail.getId());
        item.setFbAccount(detail.getFbAccount());
        item.setCookie(account == null ? null : account.getCookie());
        item.setPassword(account == null ? null : account.getPassword());
        item.setTfa(account == null ? null : account.getTfa());
        // 同行采集一条明细可能包含多个关系页，首次导航使用第一个 URL，
        // 完整关系 URL 列表通过 actionConfig 下发给 WPF。
        item.setSearchUrl(Integer.valueOf(8).equals(task == null ? null : task.getTaskType())
                ? firstRelationUrl(detail.getSearchUrl()) : detail.getSearchUrl());
        item.setSourceUserId(detail.getSourceUserId());
        item.setExpectedCount(detail.getExpectedCount());
        item.setTaskType(task == null ? 1 : task.getTaskType());
        return item;
    }

    private String buildCollectRuntimeConfig(FbCollectDetailDO detail, FbCollectDO task) {
        if (task != null && Integer.valueOf(8).equals(task.getTaskType())
                && StrUtil.contains(detail.getSearchUrl(), "||")) {
            cn.hutool.json.JSONArray relationUrls = cn.hutool.json.JSONUtil.createArray();
            for (String url : detail.getSearchUrl().split("\\|\\|")) {
                if (StrUtil.isNotBlank(url)) {
                    relationUrls.add(url.trim());
                }
            }
            return cn.hutool.json.JSONUtil.createObj()
                    .set("relationUrls", relationUrls)
                    .toString();
        }
        if (task != null && Integer.valueOf(11).equals(task.getTaskType())
                && (StrUtil.startWith(task.getRemark(), "AI群帖评论截流-评论采集:")
                || StrUtil.startWith(task.getRemark(), "AI竞品监控-评论采集:"))) {
            boolean competitorComment = StrUtil.startWith(task.getRemark(), "AI竞品监控-评论采集:");
            return cn.hutool.json.JSONUtil.createObj()
                    .set("source", competitorComment ? "ai_competitor_comment" : "ai_group_comment")
                    .set("sourcePostId", detail.getSourceUserId() == null ? null : String.valueOf(detail.getSourceUserId()))
                    .set("sourcePostUrl", detail.getSearchUrl())
                    .set("collectComment", true)
                    .set("collectLike", false)
                    .set("commentExpectedCount", detail.getExpectedCount() == null ? 100 : detail.getExpectedCount())
                    .set("likeExpectedCount", 0)
                    .toString();
        }
        if (task != null && Integer.valueOf(11).equals(task.getTaskType())) {
            // 新任务配置保存在 Redis，避免污染用户填写的备注；保留旧备注配置兼容历史任务。
            cn.hutool.json.JSONObject runtimeConfig = cn.hutool.json.JSONUtil.createObj()
                    .set("source", "post_comment")
                    .set("sourcePostId", detail.getSourceUserId() == null ? null : String.valueOf(detail.getSourceUserId()))
                    .set("sourcePostUrl", detail.getSearchUrl())
                    .set("collectComment", true)
                    .set("collectLike", true)
                    .set("commentExpectedCount", detail.getExpectedCount() == null ? 100 : detail.getExpectedCount())
                    .set("likeExpectedCount", 100);
            String redisConfig = stringRedisTemplate.opsForValue()
                    .get(COMMENT_LIKE_CONFIG_KEY_PREFIX + task.getId());
            if (StrUtil.isNotBlank(redisConfig)) {
                try {
                    cn.hutool.json.JSONObject savedConfig = cn.hutool.json.JSONUtil.parseObj(redisConfig);
                    savedConfig.forEach(runtimeConfig::set);
                    return runtimeConfig.toString();
                } catch (Exception ignored) {
                    // Redis 配置损坏时继续尝试历史备注配置。
                }
            }
            String remark = task.getRemark();
            int markerIndex = remark == null ? -1 : remark.indexOf("__CONFIG__:");
            if (markerIndex >= 0) {
                String configText = remark.substring(markerIndex + "__CONFIG__:".length()).trim();
                try {
                    cn.hutool.json.JSONObject savedConfig = cn.hutool.json.JSONUtil.parseObj(configText);
                    savedConfig.forEach(runtimeConfig::set);
                } catch (Exception ignored) {
                    // 配置损坏时保留上面的兼容默认值，不影响任务下发。
                }
            }
            return runtimeConfig.toString();
        }
        if (task != null && Integer.valueOf(2).equals(task.getTaskType())
                && StrUtil.containsIgnoreCase(detail.getSearchUrl(), "/search/top")
                && (StrUtil.isBlank(task.getRemark())
                || (!task.getRemark().startsWith("AI群帖获客:")
                && !task.getRemark().startsWith("AI帖子获客:")
                && !task.getRemark().startsWith("AI群帖评论截流-帖子采集:")
                && !task.getRemark().startsWith("AI竞品监控-帖子采集:")))) {
            return cn.hutool.json.JSONUtil.createObj()
                    .set("source", "post_search")
                    .set("latestPosts", StrUtil.contains(detail.getSearchUrl(), "filters="))
                    .set("sourceUserId", detail.getSourceUserId() == null ? null : String.valueOf(detail.getSourceUserId()))
                    .toString();
        }
        if (task == null || !Integer.valueOf(2).equals(task.getTaskType())
                || StrUtil.isBlank(task.getRemark())
                || (!task.getRemark().startsWith("AI群帖获客:")
                && !task.getRemark().startsWith("AI帖子获客:")
                && !task.getRemark().startsWith("AI群帖评论截流-帖子采集:")
                && !task.getRemark().startsWith("AI竞品监控-帖子采集:"))) {
            return detail.getSourceUserId() == null ? null : cn.hutool.json.JSONUtil.createObj()
                    .set("sourceUserId", String.valueOf(detail.getSourceUserId()))
                    .toString();
        }
        FbAiAgentDiscoveryLogDO logDO = discoveryLogMapper.selectOne(new LambdaQueryWrapper<FbAiAgentDiscoveryLogDO>()
                .eq(FbAiAgentDiscoveryLogDO::getCollectTaskId, task.getId())
                .last("LIMIT 1"));
        FbAiAgentConfigDO config = logDO == null ? null : agentConfigMapper.selectById(logDO.getAgentConfigId());
        boolean postLeadCollect = task.getRemark().startsWith("AI帖子获客:");
        boolean groupCommentPostCollect = task.getRemark().startsWith("AI群帖评论截流-帖子采集:");
        boolean competitorPostCollect = task.getRemark().startsWith("AI竞品监控-帖子采集:");
        cn.hutool.json.JSONObject runtimeConfig = cn.hutool.json.JSONUtil.createObj()
                .set("source", postLeadCollect ? "ai_post_lead" : (competitorPostCollect ? "ai_competitor_post" : (groupCommentPostCollect ? "ai_group_comment_post" : "ai_group_post")))
                .set("agentConfigId", config == null ? null : String.valueOf(config.getId()))
                .set("latestPosts", postLeadCollect && isPostLeadLatestPosts(config))
                .set("recentDays", competitorPostCollect ? resolveCompetitorRecentDays(config) : resolveGroupPostRecentDays(config))
                .set("maxPostsPerGroup", 1000)
                .set("maxPostsPerPage", 1000)
                .set("maxScrolls", 240);
        return runtimeConfig.toString();
    }

    private String firstRelationUrl(String searchUrl) {
        if (StrUtil.isBlank(searchUrl)) {
            return searchUrl;
        }
        return searchUrl.split("\\|\\|", 2)[0].trim();
    }

    private int resolveGroupPostRecentDays(FbAiAgentConfigDO config) {
        if (config == null || StrUtil.isBlank(config.getPersonaConfig())) {
            return defaultRecentDays(config);
        }
        try {
            cn.hutool.json.JSONObject persona = cn.hutool.json.JSONUtil.parseObj(config.getPersonaConfig());
            Object groupPostConfig = persona.get("groupPostConfig");
            cn.hutool.json.JSONObject groupConfig = groupPostConfig instanceof cn.hutool.json.JSONObject
                    ? (cn.hutool.json.JSONObject) groupPostConfig
                    : cn.hutool.json.JSONUtil.parseObj(groupPostConfig);
            Integer recentDays = groupConfig.getInt("recentDays");
            return recentDays != null && recentDays > 0 ? recentDays : defaultRecentDays(config);
        } catch (Exception ignored) {
            return defaultRecentDays(config);
        }
    }

    private int resolveCompetitorRecentDays(FbAiAgentConfigDO config) {
        if (config == null || StrUtil.isBlank(config.getPersonaConfig())) {
            return defaultRecentDays(config);
        }
        try {
            cn.hutool.json.JSONObject persona = cn.hutool.json.JSONUtil.parseObj(config.getPersonaConfig());
            Object competitorConfig = persona.get("competitorConfig");
            cn.hutool.json.JSONObject competitor = competitorConfig instanceof cn.hutool.json.JSONObject
                    ? (cn.hutool.json.JSONObject) competitorConfig
                    : cn.hutool.json.JSONUtil.parseObj(competitorConfig);
            Integer recentDays = competitor.getInt("recentDays");
            return recentDays != null && recentDays > 0 ? recentDays : defaultRecentDays(config);
        } catch (Exception ignored) {
            return defaultRecentDays(config);
        }
    }

    private int defaultRecentDays(FbAiAgentConfigDO config) {
        if (config == null || StrUtil.isBlank(config.getExecuteFrequency()) || "daily".equals(config.getExecuteFrequency())) {
            return 1;
        }
        try {
            int intervalDays = Integer.parseInt(config.getExecuteFrequency());
            return intervalDays >= 1 && intervalDays <= 7 ? intervalDays : 1;
        } catch (NumberFormatException ex) {
            return 1;
        }
    }

    private boolean isPostLeadLatestPosts(FbAiAgentConfigDO config) {
        if (config == null || StrUtil.isBlank(config.getPersonaConfig())) {
            return false;
        }
        try {
            cn.hutool.json.JSONObject persona = cn.hutool.json.JSONUtil.parseObj(config.getPersonaConfig());
            cn.hutool.json.JSONObject postConfig = persona.getJSONObject("postLeadConfig");
            return postConfig != null && Boolean.TRUE.equals(postConfig.getBool("latestPosts"));
        } catch (Exception ignored) {
            return false;
        }
    }

}
