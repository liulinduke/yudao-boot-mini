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
import cn.iocoder.yudao.module.facebook.dal.dataobject.fbcollectpost.FbCollectPostDO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.operation.FbOperationTaskDO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.operation.FbOperationTaskDetailDO;
import cn.iocoder.yudao.module.facebook.dal.mysql.account.FbAccountMapper;
import cn.iocoder.yudao.module.facebook.dal.mysql.agent.FbAiAgentConfigMapper;
import cn.iocoder.yudao.module.facebook.dal.mysql.agent.FbAiAgentDiscoveryLogMapper;
import cn.iocoder.yudao.module.facebook.dal.mysql.collect.FbCollectMapper;
import cn.iocoder.yudao.module.facebook.dal.mysql.collectdetail.FbCollectDetailMapper;
import cn.iocoder.yudao.module.facebook.dal.mysql.dmtask.FbDmTaskDetailMapper;
import cn.iocoder.yudao.module.facebook.dal.mysql.dmtask.FbDmTaskMapper;
import cn.iocoder.yudao.module.facebook.dal.mysql.fbcollectpost.FbCollectPostMapper;
import cn.iocoder.yudao.module.facebook.dal.mysql.operation.FbOperationTaskDetailMapper;
import cn.iocoder.yudao.module.facebook.dal.mysql.operation.FbOperationTaskMapper;
import cn.iocoder.yudao.module.facebook.service.agent.FbAccountTaskQueueItem;
import cn.iocoder.yudao.module.facebook.service.agent.FbAiAgentCollectQueueService;
import com.baomidou.mybatisplus.core.conditions.query.LambdaQueryWrapper;
import org.springframework.stereotype.Service;
import org.springframework.validation.annotation.Validated;

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

    @Resource
    private FbCollectDetailMapper fbCollectDetailMapper;
    @Resource
    private FbCollectMapper fbCollectMapper;
    @Resource
    private FbAccountMapper fbAccountMapper;
    @Resource
    private FbAiAgentCollectQueueService aiAgentCollectQueueService;
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
    private FbCollectPostMapper collectPostMapper;

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

            FbCollectPendingDetailRespVO item = new FbCollectPendingDetailRespVO();
            item.setSourceType("operation");
            item.setTaskId(detail.getTaskId());
            item.setDetailId(detail.getId());
            item.setAccountId(detail.getAccountId());
            item.setFbAccount(StrUtil.blankToDefault(detail.getFbAccount(), queueItem.getFbAccount()));
            item.setCookie(account == null ? null : account.getCookie());
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
    }

    private FbCollectPendingDetailRespVO buildPendingDetailResp(FbCollectDetailDO detail, FbCollectDO task, FbAccountDO account) {
        FbCollectPendingDetailRespVO item = new FbCollectPendingDetailRespVO();
        item.setTaskId(detail.getTaskId());
        item.setDetailId(detail.getId());
        item.setFbAccount(detail.getFbAccount());
        item.setCookie(account == null ? null : account.getCookie());
        item.setSearchUrl(detail.getSearchUrl());
        item.setSourceUserId(detail.getSourceUserId());
        item.setExpectedCount(detail.getExpectedCount());
        item.setTaskType(task == null ? 1 : task.getTaskType());
        return item;
    }

    private String buildCollectRuntimeConfig(FbCollectDetailDO detail, FbCollectDO task) {
        if (task == null || !Integer.valueOf(2).equals(task.getTaskType())
                || StrUtil.isBlank(task.getRemark()) || !task.getRemark().startsWith("AI群帖获客:")) {
            return detail.getSourceUserId() == null ? null : cn.hutool.json.JSONUtil.createObj()
                    .set("sourceUserId", String.valueOf(detail.getSourceUserId()))
                    .toString();
        }
        FbAiAgentDiscoveryLogDO logDO = discoveryLogMapper.selectOne(new LambdaQueryWrapper<FbAiAgentDiscoveryLogDO>()
                .eq(FbAiAgentDiscoveryLogDO::getCollectTaskId, task.getId())
                .last("LIMIT 1"));
        FbAiAgentConfigDO config = logDO == null ? null : agentConfigMapper.selectById(logDO.getAgentConfigId());
        cn.hutool.json.JSONObject runtimeConfig = cn.hutool.json.JSONUtil.createObj()
                .set("source", "ai_group_post")
                .set("agentConfigId", config == null ? null : String.valueOf(config.getId()))
                .set("recentDays", resolveGroupPostRecentDays(config))
                .set("knownPostKeys", loadKnownPostKeys(config == null ? null : config.getId()));
        return runtimeConfig.toString();
    }

    private int resolveGroupPostRecentDays(FbAiAgentConfigDO config) {
        if (config == null || StrUtil.isBlank(config.getPersonaConfig())) {
            return 3;
        }
        try {
            cn.hutool.json.JSONObject persona = cn.hutool.json.JSONUtil.parseObj(config.getPersonaConfig());
            Object groupPostConfig = persona.get("groupPostConfig");
            cn.hutool.json.JSONObject groupConfig = groupPostConfig instanceof cn.hutool.json.JSONObject
                    ? (cn.hutool.json.JSONObject) groupPostConfig
                    : cn.hutool.json.JSONUtil.parseObj(groupPostConfig);
            Integer recentDays = groupConfig.getInt("recentDays");
            return recentDays != null && recentDays > 0 ? recentDays : 3;
        } catch (Exception ignored) {
            return 3;
        }
    }

    private List<String> loadKnownPostKeys(Long agentConfigId) {
        if (agentConfigId == null) {
            return List.of();
        }
        List<Long> taskIds = discoveryLogMapper.selectList(new LambdaQueryWrapper<FbAiAgentDiscoveryLogDO>()
                        .eq(FbAiAgentDiscoveryLogDO::getAgentConfigId, agentConfigId)
                        .eq(FbAiAgentDiscoveryLogDO::getSourceType, "group_post")
                        .select(FbAiAgentDiscoveryLogDO::getCollectTaskId))
                .stream()
                .map(FbAiAgentDiscoveryLogDO::getCollectTaskId)
                .filter(Objects::nonNull)
                .collect(Collectors.toList());
        if (CollUtil.isEmpty(taskIds)) {
            return List.of();
        }
        Set<String> keys = new LinkedHashSet<>();
        collectPostMapper.selectList(new LambdaQueryWrapper<FbCollectPostDO>()
                        .in(FbCollectPostDO::getTaskId, taskIds)
                        .select(FbCollectPostDO::getItemId, FbCollectPostDO::getUrl))
                .forEach(post -> {
                    if (StrUtil.isNotBlank(post.getItemId())) {
                        keys.add(post.getItemId());
                    }
                    if (StrUtil.isNotBlank(post.getUrl())) {
                        keys.add(post.getUrl());
                    }
                });
        return new ArrayList<>(keys);
    }
}
