package cn.iocoder.yudao.module.facebook.service.operation;

import cn.hutool.core.collection.CollUtil;
import cn.hutool.core.util.StrUtil;
import cn.hutool.json.JSONArray;
import cn.hutool.json.JSONObject;
import cn.hutool.json.JSONUtil;
import cn.iocoder.yudao.framework.common.pojo.PageResult;
import cn.iocoder.yudao.framework.common.util.object.BeanUtils;
import cn.iocoder.yudao.module.facebook.enums.OperationTypeEnum;
import cn.iocoder.yudao.module.facebook.controller.admin.operation.vo.*;
import cn.iocoder.yudao.module.facebook.dal.dataobject.operation.FbOperationAddGroupResultDO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.operation.FbOperationTaskDO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.operation.FbOperationTaskDetailDO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.dmtask.FbDmTaskDO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.dmtask.FbDmTaskDetailDO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.operation.FbRepostResultDO;
import cn.iocoder.yudao.module.facebook.dal.mysql.dmtask.FbDmTaskDetailMapper;
import cn.iocoder.yudao.module.facebook.dal.mysql.dmtask.FbDmTaskMapper;
import cn.iocoder.yudao.module.facebook.dal.mysql.operation.FbOperationAddGroupResultMapper;
import cn.iocoder.yudao.module.facebook.dal.mysql.operation.FbOperationTaskDetailMapper;
import cn.iocoder.yudao.module.facebook.dal.mysql.operation.FbOperationTaskMapper;
import cn.iocoder.yudao.module.facebook.dal.mysql.operation.FbRepostResultMapper;
import cn.iocoder.yudao.module.facebook.dal.dataobject.account.FbAccountDO;
import cn.iocoder.yudao.module.facebook.dal.mysql.account.FbAccountMapper;
import cn.iocoder.yudao.framework.mybatis.core.query.LambdaQueryWrapperX;
import cn.iocoder.yudao.module.facebook.service.agent.FbAiAgentCollectQueueService;
import cn.iocoder.yudao.module.facebook.service.dailylimit.FacebookDailyLimitService;
import cn.iocoder.yudao.module.facebook.service.account.FbAccountTaskAllocationService;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;
import org.springframework.validation.annotation.Validated;

import jakarta.annotation.Resource;

import java.time.LocalDateTime;
import java.time.OffsetDateTime;
import java.time.format.DateTimeFormatter;
import java.util.ArrayList;
import java.util.Collections;
import java.util.Comparator;
import java.util.LinkedHashMap;
import java.util.LinkedHashSet;
import java.util.List;
import java.util.Map;
import java.util.Set;
import java.util.concurrent.ThreadLocalRandom;
import java.util.stream.Collectors;

import static cn.iocoder.yudao.framework.common.exception.util.ServiceExceptionUtil.exception;
import static cn.iocoder.yudao.framework.common.exception.util.ServiceExceptionUtil.invalidParamException;
import static cn.iocoder.yudao.module.facebook.enums.ErrorCodeConstants.*;

/**
 * 运营任务 Service 实现类
 *
 * @author 芋道源码
 */
@Service
@Validated
public class FbOperationTaskServiceImpl implements FbOperationTaskService {

    private static final int DM_TASK_TYPE = 14;
    private static final int REPOST_TASK_TYPE = 10;
    private static final int POST_COMMENT_TASK_TYPE = 15;
    private static final int FOLLOW_TASK_TYPE = 16;
    private static final int FOLLOW_ACTION_TYPE = 7;

    @Resource
    private FbOperationTaskMapper operationTaskMapper;

    @Resource
    private FbDmTaskMapper dmTaskMapper;

    @Resource
    private FbDmTaskDetailMapper dmTaskDetailMapper;

    @Resource
    private FbOperationTaskDetailMapper operationTaskDetailMapper;

    @Resource
    private FbOperationAddGroupResultMapper addGroupResultMapper;

    @Resource
    private FbRepostResultMapper repostResultMapper;

    @Resource
    private FbAccountMapper fbAccountMapper;

    @Resource
    private FacebookDailyLimitService dailyLimitService;
    @Resource
    private FbAiAgentCollectQueueService accountTaskQueueService;

    @Resource
    private FbAccountTaskAllocationService accountAllocationService;

    @Override
    @Transactional(rollbackFor = Exception.class)
    public Long createOperationTask(FbOperationTaskSaveReqVO createReqVO) {
        if (createReqVO.getTaskType() != null && createReqVO.getTaskType() == POST_COMMENT_TASK_TYPE) {
            return createPostCommentTask(createReqVO);
        }
        if (createReqVO.getTaskType() != null && createReqVO.getTaskType() == FOLLOW_TASK_TYPE) {
            return createFollowTask(createReqVO);
        }
        // 1. 创建主任务
        FbOperationTaskDO task = BeanUtils.toBean(createReqVO, FbOperationTaskDO.class);
        task.setStatus(0); // 待执行
        task.setActualCount(0);
        List<String> requestedAccountIds = normalizeAccountIds(createReqVO.getAccountIds());
        List<Long> requestedIds = requestedAccountIds.stream().map(Long::valueOf).collect(Collectors.toList());
        List<Long> selectedIds = accountAllocationService.selectAccounts(
                createReqVO.getAccountSelectionMode(), requestedIds,
                Math.max(1, createReqVO.getExpectedCount()), "operation",
                resolveActionTypes(createReqVO.getTaskType(), createReqVO.getActionConfig()));
        if (CollUtil.isEmpty(selectedIds)) {
            throw invalidParamException("没有可用的Facebook账号，请检查账号状态或每日额度");
        }
        List<String> normalizedAccountIds = selectedIds.stream().map(String::valueOf).collect(Collectors.toList());
        task.setAccountIds(String.join(",", normalizedAccountIds));
        task.setAccountSelectionMode(createReqVO.getAccountSelectionMode());
        operationTaskMapper.insert(task);

        // 2. 规范化账号ID并查询账号信息映射
        List<Long> accountIdLongs = normalizedAccountIds.stream()
                .map(Long::valueOf)
                .collect(Collectors.toList());
        List<FbAccountDO> accountList = fbAccountMapper.selectBatchIds(accountIdLongs);
        Map<Long, String> accountIdToFbAccountMap = accountList.stream()
                .collect(Collectors.toMap(FbAccountDO::getId, FbAccountDO::getFbAccount, (a, b) -> a));

        List<JSONObject> addGroupAllocations = Integer.valueOf(9).equals(task.getTaskType())
                ? allocateAddGroupTargets(createReqVO.getActionConfig(), normalizedAccountIds.size())
                : Collections.emptyList();
        List<JSONObject> groupPublishAllocations = Integer.valueOf(13).equals(task.getTaskType())
                ? allocateGroupPublishTargets(createReqVO.getActionConfig(), normalizedAccountIds.size())
                : Collections.emptyList();

        // 3. 为每个账号创建明细
        List<FbOperationTaskDetailDO> details = new ArrayList<>();
        for (int i = 0; i < normalizedAccountIds.size(); i++) {
            String accountIdStr = normalizedAccountIds.get(i);
            Long accountId = Long.valueOf(accountIdStr);
            String fbAccount = accountIdToFbAccountMap.get(accountId);
            if (StrUtil.isBlank(fbAccount)) {
                FbAccountDO account = fbAccountMapper.selectById(accountId);
                fbAccount = account != null ? StrUtil.nullToEmpty(account.getFbAccount()) : "";
            }
            JSONObject detailActionConfig = Integer.valueOf(9).equals(task.getTaskType()) && i < addGroupAllocations.size()
                    ? addGroupAllocations.get(i)
                    : Integer.valueOf(13).equals(task.getTaskType()) && i < groupPublishAllocations.size()
                    ? groupPublishAllocations.get(i)
                    : parseActionConfig(createReqVO.getActionConfig());
            int detailExpectedCount = Integer.valueOf(9).equals(task.getTaskType())
                    ? detailActionConfig.getJSONArray("groups") == null ? 0 : detailActionConfig.getJSONArray("groups").size()
                    : Integer.valueOf(13).equals(task.getTaskType())
                    ? calculateGroupPublishExpectedCount(detailActionConfig)
                    : createReqVO.getExpectedCount();
            if ((Integer.valueOf(9).equals(task.getTaskType()) || Integer.valueOf(13).equals(task.getTaskType()))
                    && detailExpectedCount <= 0) {
                continue;
            }
            FbOperationTaskDetailDO detail = new FbOperationTaskDetailDO();
            detail.setTaskId(task.getId());
            detail.setAccountId(accountIdStr);
            detail.setFbAccount(fbAccount);
            detail.setTargetUrls(createReqVO.getTargetUrls());
            detail.setTargetGroupIds(createReqVO.getTargetGroupIds());
            detail.setPostUrl(createReqVO.getPostUrl());
            detail.setActionConfig(detailActionConfig.toString());
            detail.setCommentScript(createReqVO.getCommentScript());
            detail.setScriptLibraryId(createReqVO.getScriptLibraryId());
            detail.setExpectedCount(detailExpectedCount);
            detail.setActualCount(0);
            detail.setStatus(0); // 待执行
            operationTaskDetailMapper.insert(detail);
            details.add(detail);
        }
        pushOperationDetailsToAccountQueue(details);

        return task.getId();
    }

    private Long createFollowTask(FbOperationTaskSaveReqVO createReqVO) {
        List<String> requestedAccountIds = normalizeAccountIds(createReqVO.getAccountIds());

        JSONObject actionConfig = parseActionConfig(createReqVO.getActionConfig());
        List<Integer> selectedActions = parseActionList(actionConfig);
        if (CollUtil.isEmpty(selectedActions)) {
            selectedActions = Collections.singletonList(FOLLOW_ACTION_TYPE);
        }
        if (selectedActions.size() != 1 || !selectedActions.contains(FOLLOW_ACTION_TYPE)) {
            throw invalidParamException("刷粉任务仅支持关注");
        }

        String targetUrl = resolveFollowTargetUrl(createReqVO, actionConfig);
        if (StrUtil.isBlank(targetUrl)) {
            throw invalidParamException("目标主页链接不能为空");
        }

        List<Long> selectedIds = accountAllocationService.selectAccounts(
                createReqVO.getAccountSelectionMode(), requestedAccountIds.stream().map(Long::valueOf).collect(Collectors.toList()),
                Math.max(1, createReqVO.getExpectedCount()), "operation", Collections.singletonList("follow"));
        List<String> normalizedAccountIds = selectedIds.stream().map(String::valueOf).collect(Collectors.toList());
        if (CollUtil.isEmpty(normalizedAccountIds)) {
            throw invalidParamException("所选账号没有可用的关注额度");
        }
        if (!targetUrl.matches("(?i)^https?://(www\\.)?facebook\\.com/.+")) {
            throw invalidParamException("请输入有效的 Facebook 主页链接");
        }

        List<Long> accountIdLongs = normalizedAccountIds.stream().map(Long::valueOf).collect(Collectors.toList());
        List<FbAccountDO> accountList = fbAccountMapper.selectBatchIds(accountIdLongs);
        Map<Long, String> accountIdToFbAccountMap = accountList.stream()
                .collect(Collectors.toMap(FbAccountDO::getId, FbAccountDO::getFbAccount, (a, b) -> a));

        List<String> executableAccountIds = normalizedAccountIds.stream()
                .filter(accountId -> dailyLimitService.getRemainingCount(accountId, OperationTypeEnum.FOLLOW) > 0)
                .collect(Collectors.toList());
        if (CollUtil.isEmpty(executableAccountIds)) {
            throw invalidParamException("所选账号今日关注额度不足，无法创建刷粉任务");
        }

        JSONObject taskConfig = JSONUtil.parseObj(actionConfig.toString());
        taskConfig.set("actions", selectedActions);
        taskConfig.set("targetUrl", targetUrl);
        taskConfig.set("postUrl", targetUrl);

        FbOperationTaskDO task = BeanUtils.toBean(createReqVO, FbOperationTaskDO.class);
        task.setTaskType(FOLLOW_TASK_TYPE);
        task.setStatus(0);
        task.setActualCount(0);
        task.setAccountSelectionMode(createReqVO.getAccountSelectionMode());
        task.setExpectedCount(executableAccountIds.size());
        task.setAccountIds(String.join(",", executableAccountIds));
        task.setActionConfig(taskConfig.toString());
        operationTaskMapper.insert(task);

        for (String accountIdStr : executableAccountIds) {
            Long accountId = Long.valueOf(accountIdStr);
            String fbAccount = accountIdToFbAccountMap.get(accountId);
            if (StrUtil.isBlank(fbAccount)) {
                FbAccountDO account = fbAccountMapper.selectById(accountId);
                fbAccount = account != null ? StrUtil.nullToEmpty(account.getFbAccount()) : "";
            }

            FbOperationTaskDetailDO detail = new FbOperationTaskDetailDO();
            detail.setTaskId(task.getId());
            detail.setAccountId(accountIdStr);
            detail.setFbAccount(fbAccount);
            detail.setTargetUrls(targetUrl);
            detail.setPostUrl(targetUrl);
            detail.setActionConfig(taskConfig.toString());
            detail.setExpectedCount(1);
            detail.setActualCount(0);
            detail.setStatus(0);
            operationTaskDetailMapper.insert(detail);
            pushOperationDetailToAccountQueue(detail);
        }

        return task.getId();
    }

    private Long createPostCommentTask(FbOperationTaskSaveReqVO createReqVO) {
        List<String> requestedAccountIds = normalizeAccountIds(createReqVO.getAccountIds());

        JSONObject actionConfig = parseActionConfig(createReqVO.getActionConfig());
        List<Integer> selectedActions = parseActionList(actionConfig);
        if (CollUtil.isEmpty(selectedActions)) {
            throw invalidParamException("至少选择一个执行项");
        }
        if (selectedActions.stream().anyMatch(action -> action != 1 && action != 6)) {
            throw invalidParamException("帖子评论任务仅支持点赞和评论");
        }

        List<String> normalizedPostUrls = normalizePostUrls(createReqVO.getPostUrls(), actionConfig);
        if (CollUtil.isEmpty(normalizedPostUrls)) {
            throw invalidParamException("至少提供一个帖子链接");
        }

        List<String> commentScripts = parseScriptList(actionConfig, "commentScripts");
        boolean appendRandomEmoji = actionConfig.getBool("commentAppendRandomEmoji", false);
        if (selectedActions.contains(6) && CollUtil.isEmpty(commentScripts)) {
            throw invalidParamException("已勾选评论，评论话术不能为空");
        }

        List<Long> selectedIds = accountAllocationService.selectAccounts(
                createReqVO.getAccountSelectionMode(), requestedAccountIds.stream().map(Long::valueOf).collect(Collectors.toList()),
                normalizedPostUrls.size(), "operation", selectedActions.contains(6)
                        ? Collections.singletonList("comment") : Collections.emptyList());
        List<String> normalizedAccountIds = selectedIds.stream().map(String::valueOf).collect(Collectors.toList());
        if (CollUtil.isEmpty(normalizedAccountIds)) {
            throw invalidParamException("没有可用的Facebook账号，请检查账号状态或每日额度");
        }

        List<Long> accountIdLongs = normalizedAccountIds.stream().map(Long::valueOf).collect(Collectors.toList());
        List<FbAccountDO> accountList = fbAccountMapper.selectBatchIds(accountIdLongs);
        Map<Long, String> accountIdToFbAccountMap = accountList.stream()
                .collect(Collectors.toMap(FbAccountDO::getId, FbAccountDO::getFbAccount, (a, b) -> a));

        List<AccountAllocationState> accountStates = normalizedAccountIds.stream()
                .map(accountId -> {
                    Long accountIdLong = Long.valueOf(accountId);
                    String fbAccount = accountIdToFbAccountMap.get(accountIdLong);
                    if (StrUtil.isBlank(fbAccount)) {
                        FbAccountDO account = fbAccountMapper.selectById(accountIdLong);
                        fbAccount = account != null ? StrUtil.nullToEmpty(account.getFbAccount()) : "";
                    }
                    int commentRemaining = selectedActions.contains(6)
                            ? dailyLimitService.getRemainingCount(accountId, OperationTypeEnum.COMMENT)
                            : 0;
                    return new AccountAllocationState(accountId, fbAccount, commentRemaining);
                })
                .collect(Collectors.toList());

        Map<String, DetailAllocation> allocations = new LinkedHashMap<>();
        int assignedCommentPosts = 0;
        if (selectedActions.contains(6)) {
            List<AccountAllocationState> commentAccounts = accountStates.stream()
                    .filter(account -> account.commentRemaining > 0)
                    .collect(Collectors.toList());
            if (CollUtil.isEmpty(commentAccounts)) {
                throw invalidParamException("所选账号今日评论额度不足，无法创建帖子评论任务");
            }
            for (String postUrl : normalizedPostUrls) {
                AccountAllocationState account = pickNextCommentAccount(commentAccounts);
                if (account == null) {
                    break;
                }
                DetailAllocation allocation = getOrCreateAllocation(allocations, account, postUrl);
                allocation.actions.add(6);
                if (StrUtil.isBlank(allocation.commentScript)) {
                    allocation.commentScript = buildFinalCommentText(commentScripts, appendRandomEmoji);
                }
                account.commentAssigned++;
                assignedCommentPosts++;
            }
            if (assignedCommentPosts == 0) {
                throw invalidParamException("所选账号今日评论额度不足，无法分配任何评论帖子");
            }
        }

        if (selectedActions.contains(1)) {
            for (String postUrl : normalizedPostUrls) {
                DetailAllocation existing = findAllocationByPost(allocations, postUrl);
                if (existing != null) {
                    existing.actions.add(1);
                    continue;
                }
                AccountAllocationState likeAccount = pickNextLikeAccount(accountStates);
                DetailAllocation likeAllocation = getOrCreateAllocation(allocations, likeAccount, postUrl);
                likeAllocation.actions.add(1);
                likeAccount.likeAssigned++;
            }
        }

        if (allocations.isEmpty()) {
            throw invalidParamException("未生成任何可执行明细，请检查帖子和账号配置");
        }

        int expectedCount = allocations.values().stream().mapToInt(item -> item.actions.size()).sum();
        JSONObject taskConfig = JSONUtil.parseObj(actionConfig.toString());
        taskConfig.set("actions", selectedActions);
        taskConfig.set("postUrls", normalizedPostUrls);
        taskConfig.set("assignedCommentPostCount", assignedCommentPosts);
        taskConfig.set("totalPostCount", normalizedPostUrls.size());

        FbOperationTaskDO task = BeanUtils.toBean(createReqVO, FbOperationTaskDO.class);
        task.setTaskType(POST_COMMENT_TASK_TYPE);
        task.setStatus(0);
        task.setActualCount(0);
        task.setAccountSelectionMode(createReqVO.getAccountSelectionMode());
        task.setExpectedCount(expectedCount);
        task.setAccountIds(String.join(",", normalizedAccountIds));
        task.setActionConfig(taskConfig.toString());
        operationTaskMapper.insert(task);

        for (DetailAllocation allocation : allocations.values()) {
            JSONObject detailConfig = JSONUtil.parseObj(taskConfig.toString());
            detailConfig.set("actions", new ArrayList<>(allocation.actions));
            detailConfig.set("postUrls", Collections.singletonList(allocation.postUrl));
            if (StrUtil.isNotBlank(allocation.commentScript)) {
                detailConfig.set("finalCommentText", allocation.commentScript);
            }

            FbOperationTaskDetailDO detail = new FbOperationTaskDetailDO();
            detail.setTaskId(task.getId());
            detail.setAccountId(allocation.accountId);
            detail.setFbAccount(allocation.fbAccount);
            detail.setPostUrl(allocation.postUrl);
            detail.setActionConfig(detailConfig.toString());
            detail.setCommentScript(allocation.commentScript);
            detail.setExpectedCount(allocation.actions.size());
            detail.setActualCount(0);
            detail.setStatus(0);
            operationTaskDetailMapper.insert(detail);
            pushOperationDetailToAccountQueue(detail);
        }

        return task.getId();
    }

    @Override
    @Transactional(rollbackFor = Exception.class)
    public void updateOperationTask(FbOperationTaskSaveReqVO updateReqVO) {
        // 校验存在
        validateOperationTaskExists(updateReqVO.getId());

        // 更新主任务
        FbOperationTaskDO updateObj = BeanUtils.toBean(updateReqVO, FbOperationTaskDO.class);
        updateObj.setAccountIds(String.join(",", updateReqVO.getAccountIds()));
        operationTaskMapper.updateById(updateObj);
    }

    @Override
    @Transactional(rollbackFor = Exception.class)
    public void deleteOperationTask(Long id) {
        // 校验存在
        validateOperationTaskExists(id);

        // 删除主任务
        operationTaskMapper.deleteById(id);

        // 删除明细
        List<FbOperationTaskDetailDO> details = operationTaskDetailMapper.selectListByTaskId(id);
        if (CollUtil.isNotEmpty(details)) {
            List<Long> detailIds = details.stream().map(FbOperationTaskDetailDO::getId).collect(Collectors.toList());
            operationTaskDetailMapper.deleteBatchIds(detailIds);

            // 删除结果
            addGroupResultMapper.delete(new com.baomidou.mybatisplus.core.conditions.query.LambdaQueryWrapper<FbOperationAddGroupResultDO>()
                    .in(FbOperationAddGroupResultDO::getDetailId, detailIds));
            repostResultMapper.delete(new com.baomidou.mybatisplus.core.conditions.query.LambdaQueryWrapper<FbRepostResultDO>()
                    .in(FbRepostResultDO::getDetailId, detailIds));
        }
    }

    @Override
    public FbOperationTaskDetailRespVO getOperationTask(Long id) {
        FbOperationTaskDO task = operationTaskMapper.selectById(id);
        if (task != null) {
            return buildOperationTaskDetail(task);
        }
        FbDmTaskDO dmTask = dmTaskMapper.selectById(id);
        if (dmTask != null) {
            return buildDmTaskDetail(dmTask);
        }
        throw exception(OPERATION_TASK_NOT_EXISTS);
    }

    private FbOperationTaskDetailRespVO buildOperationTaskDetail(FbOperationTaskDO task) {
        FbOperationTaskDetailRespVO respVO = new FbOperationTaskDetailRespVO();
        FbOperationTaskRespVO taskVO = BeanUtils.toBean(task, FbOperationTaskRespVO.class);
        taskVO.setSourceType("operation");
        respVO.setTask(taskVO);

        List<FbOperationTaskDetailDO> details = operationTaskDetailMapper.selectListByTaskId(task.getId());
        List<FbOperationTaskDetailRespVO.FbOperationTaskDetailItemVO> detailItems =
                BeanUtils.toBean(details, FbOperationTaskDetailRespVO.FbOperationTaskDetailItemVO.class);
        enrichDetailFbAccount(detailItems);
        respVO.setDetails(detailItems);

        if (task.getTaskType() != null && task.getTaskType() == 9) {
            List<FbOperationAddGroupResultDO> results = addGroupResultMapper.selectListByTaskId(task.getId());
            respVO.setResults(BeanUtils.toBean(results, FbOperationAddGroupResultRespVO.class));
        }
        if (task.getTaskType() != null && (task.getTaskType() == REPOST_TASK_TYPE || task.getTaskType() == POST_COMMENT_TASK_TYPE || task.getTaskType() == FOLLOW_TASK_TYPE)) {
            List<FbRepostResultDO> repostResults = repostResultMapper.selectListByTaskId(task.getId());
            List<FbRepostResultRespVO> repostResultItems = BeanUtils.toBean(repostResults, FbRepostResultRespVO.class);
            enrichRepostResultFbAccount(repostResultItems);
            respVO.setRepostResults(repostResultItems);
        }
        if (task.getTaskType() != null && task.getTaskType() == 13) {
            List<FbOperationAddGroupResultDO> results = addGroupResultMapper.selectListByTaskId(task.getId());
            List<FbGroupPublishResultRespVO> groupPublishResultItems = BeanUtils.toBean(results, FbGroupPublishResultRespVO.class);
            enrichGroupPublishResultFbAccount(groupPublishResultItems);
            respVO.setGroupPublishResults(groupPublishResultItems);
        }
        return respVO;
    }

    private FbOperationTaskDetailRespVO buildDmTaskDetail(FbDmTaskDO dmTask) {
        FbOperationTaskDetailRespVO respVO = new FbOperationTaskDetailRespVO();
        respVO.setTask(convertDmTaskToOperation(dmTask));

        List<FbDmTaskDetailDO> dmDetails = dmTaskDetailMapper.selectListByTaskId(dmTask.getId());
        List<FbOperationTaskDetailRespVO.FbOperationTaskDetailItemVO> detailItems = dmDetails.stream()
                .map(this::convertDmDetailToOperation)
                .collect(Collectors.toList());
        enrichDetailFbAccount(detailItems);
        respVO.setDetails(detailItems);
        return respVO;
    }

    private FbOperationTaskDetailRespVO.FbOperationTaskDetailItemVO convertDmDetailToOperation(FbDmTaskDetailDO detail) {
        FbOperationTaskDetailRespVO.FbOperationTaskDetailItemVO item =
                new FbOperationTaskDetailRespVO.FbOperationTaskDetailItemVO();
        item.setId(detail.getId());
        item.setAccountId(detail.getAccountId());
        item.setTargetUserId(detail.getTargetUserId());
        item.setScriptContent(detail.getScriptContent());
        item.setErrorMsg(detail.getErrorMsg());
        item.setSendTime(detail.getSendTime());
        item.setCreateTime(detail.getCreateTime());
        item.setExpectedCount(1);
        item.setActualCount(detail.getStatus() != null && detail.getStatus() == 1 ? 1 : 0);
        item.setStatus(mapDmDetailStatusToOperation(detail.getStatus()));
        if (detail.getSendTime() != null) {
            item.setEndTime(detail.getSendTime());
        }
        return item;
    }

    private Integer mapDmDetailStatusToOperation(Integer dmStatus) {
        if (dmStatus == null) {
            return 0;
        }
        switch (dmStatus) {
            case 1:
                return 2;
            case 2:
                return 3;
            default:
                return 0;
        }
    }

    @Override
    public PageResult<FbOperationTaskRespVO> getOperationTaskPage(FbOperationTaskPageReqVO pageReqVO) {
        Integer taskType = pageReqVO.getTaskType();
        if (taskType != null && taskType == DM_TASK_TYPE) {
            return getDmTaskPageAsOperation(pageReqVO);
        }
        if (taskType != null) {
            return getOperationOnlyPage(pageReqVO);
        }
        return getMergedOperationTaskPage(pageReqVO);
    }

    private PageResult<FbOperationTaskRespVO> getOperationOnlyPage(FbOperationTaskPageReqVO pageReqVO) {
        PageResult<FbOperationTaskDO> pageResult = operationTaskMapper.selectPage(pageReqVO,
                new LambdaQueryWrapperX<FbOperationTaskDO>()
                        .eqIfPresent(FbOperationTaskDO::getTaskType, pageReqVO.getTaskType())
                        .eqIfPresent(FbOperationTaskDO::getStatus, pageReqVO.getStatus())
                        .betweenIfPresent(FbOperationTaskDO::getCreateTime, pageReqVO.getCreateTime())
                        .orderByDesc(FbOperationTaskDO::getId));
        PageResult<FbOperationTaskRespVO> result = BeanUtils.toBean(pageResult, FbOperationTaskRespVO.class);
        if (result.getList() != null) {
            result.getList().forEach(item -> item.setSourceType("operation"));
        }
        return result;
    }

    private PageResult<FbOperationTaskRespVO> getDmTaskPageAsOperation(FbOperationTaskPageReqVO pageReqVO) {
        PageResult<FbDmTaskDO> pageResult = dmTaskMapper.selectPage(pageReqVO,
                new LambdaQueryWrapperX<FbDmTaskDO>()
                        .eqIfPresent(FbDmTaskDO::getStatus, mapOperationStatusToDm(pageReqVO.getStatus()))
                        .betweenIfPresent(FbDmTaskDO::getCreateTime, pageReqVO.getCreateTime())
                        .orderByDesc(FbDmTaskDO::getId));
        List<FbOperationTaskRespVO> list = pageResult.getList().stream()
                .map(this::convertDmTaskToOperation)
                .collect(Collectors.toList());
        return new PageResult<>(list, pageResult.getTotal());
    }

    private PageResult<FbOperationTaskRespVO> getMergedOperationTaskPage(FbOperationTaskPageReqVO pageReqVO) {
        List<FbOperationTaskDO> operationTasks = operationTaskMapper.selectList(
                new LambdaQueryWrapperX<FbOperationTaskDO>()
                        .eqIfPresent(FbOperationTaskDO::getStatus, pageReqVO.getStatus())
                        .betweenIfPresent(FbOperationTaskDO::getCreateTime, pageReqVO.getCreateTime())
                        .orderByDesc(FbOperationTaskDO::getId));

        List<FbDmTaskDO> dmTasks = dmTaskMapper.selectList(
                new LambdaQueryWrapperX<FbDmTaskDO>()
                        .eqIfPresent(FbDmTaskDO::getStatus, mapOperationStatusToDm(pageReqVO.getStatus()))
                        .betweenIfPresent(FbDmTaskDO::getCreateTime, pageReqVO.getCreateTime())
                        .orderByDesc(FbDmTaskDO::getId));

        List<FbOperationTaskRespVO> merged = new ArrayList<>();
        operationTasks.forEach(task -> {
            FbOperationTaskRespVO vo = BeanUtils.toBean(task, FbOperationTaskRespVO.class);
            vo.setSourceType("operation");
            merged.add(vo);
        });
        dmTasks.forEach(task -> merged.add(convertDmTaskToOperation(task)));

        merged.sort(Comparator.comparing(FbOperationTaskRespVO::getCreateTime,
                Comparator.nullsLast(Comparator.reverseOrder())));

        int pageNo = pageReqVO.getPageNo();
        int pageSize = pageReqVO.getPageSize();
        int fromIndex = Math.max((pageNo - 1) * pageSize, 0);
        int toIndex = Math.min(fromIndex + pageSize, merged.size());
        List<FbOperationTaskRespVO> pageList = fromIndex >= merged.size()
                ? new ArrayList<>()
                : merged.subList(fromIndex, toIndex);
        return new PageResult<>(pageList, (long) merged.size());
    }

    private FbOperationTaskRespVO convertDmTaskToOperation(FbDmTaskDO dmTask) {
        FbOperationTaskRespVO vo = new FbOperationTaskRespVO();
        vo.setId(dmTask.getId());
        vo.setTaskType(DM_TASK_TYPE);
        vo.setTaskName(dmTask.getTaskName());
        vo.setStatus(mapDmStatusToOperation(dmTask.getStatus()));
        vo.setExpectedCount(dmTask.getTotalCount());
        vo.setActualCount(dmTask.getCompletedCount());
        vo.setAccountIds(dmTask.getAccountIds());
        vo.setRemark(dmTask.getRemark());
        vo.setStartTime(dmTask.getStartTime());
        vo.setEndTime(dmTask.getEndTime());
        vo.setCreateTime(dmTask.getCreateTime());
        vo.setSourceType("dm");
        return vo;
    }

    private Integer mapDmStatusToOperation(Integer dmStatus) {
        if (dmStatus == null) {
            return null;
        }
        switch (dmStatus) {
            case 3:
                return 4;
            case 4:
                return 3;
            default:
                return dmStatus;
        }
    }

    private Integer mapOperationStatusToDm(Integer operationStatus) {
        if (operationStatus == null) {
            return null;
        }
        switch (operationStatus) {
            case 3:
                return 4;
            case 4:
                return 3;
            default:
                return operationStatus;
        }
    }

    @Override
    @Transactional(rollbackFor = Exception.class)
    public void batchSaveAddGroupResult(FbOperationAddGroupResultBatchSaveReqVO batchSaveReqVO) {
        Long detailId = batchSaveReqVO.getDetailId();

        // 获取明细信息
        FbOperationTaskDetailDO detail = operationTaskDetailMapper.selectById(detailId);
        if (detail == null) {
            throw exception(OPERATION_TASK_DETAIL_NOT_EXISTS);
        }

        // 批量保存结果
        String detailFbAccount = StrUtil.blankToDefault(detail.getFbAccount(), resolveFbAccount(detail.getAccountId()));
        List<FbOperationAddGroupResultDO> results = batchSaveReqVO.getResults().stream()
                .map(item -> {
                    FbOperationAddGroupResultDO result = new FbOperationAddGroupResultDO();
                    result.setDetailId(detailId);
                    result.setTaskId(detail.getTaskId());
                    result.setAccountId(StrUtil.blankToDefault(item.getAccountId(), detail.getAccountId()));
                    result.setFbAccount(StrUtil.blankToDefault(item.getFbAccount(), detailFbAccount));
                    result.setTargetUrl(item.getTargetUrl());
                    result.setGroupId(item.getGroupId() != null ? String.valueOf(item.getGroupId()) : null);
                    result.setGroupName(item.getGroupName());
                    result.setGroupUrl(item.getGroupUrl());
                    result.setJoinStatus(item.getJoinStatus());
                    result.setFailReason(item.getFailReason());
                    result.setJoinTime(parseFlexibleDateTime(item.getJoinTime()));
                    result.setSyncTime(parseFlexibleDateTime(item.getSyncTime()));
                    return result;
                })
                .collect(Collectors.toList());

        addGroupResultMapper.delete(new com.baomidou.mybatisplus.core.conditions.query.LambdaQueryWrapper<FbOperationAddGroupResultDO>()
                .eq(FbOperationAddGroupResultDO::getDetailId, detailId));
        addGroupResultMapper.insertBatch(results);

        results.stream()
                .filter(r -> r.getJoinStatus() != null && (r.getJoinStatus() == 1 || r.getJoinStatus() == 3))
                .forEach(r -> dailyLimitService.useOnce(r.getAccountId(), OperationTypeEnum.JOIN_GROUP));

        // 更新明细的实际完成数量和状态
        int successCount = (int) results.stream()
                .filter(r -> r.getJoinStatus() != null && (r.getJoinStatus() == 1 || r.getJoinStatus() == 3)) // 1-成功 3-已加入/待审核
                .count();

        int expectedCount = results.size();
        detail.setExpectedCount(expectedCount);
        detail.setActualCount(successCount);
        detail.setStatus(successCount >= expectedCount ? 2 : 3); // 2-已完成 3-失败
        if (detail.getStartTime() == null) {
            detail.setStartTime(LocalDateTime.now());
        }
        detail.setEndTime(LocalDateTime.now());
        if (detail.getStatus() == 3) {
            detail.setErrorMsg(results.stream()
                    .filter(r -> r.getJoinStatus() != null && r.getJoinStatus() == 2 && r.getFailReason() != null)
                    .map(FbOperationAddGroupResultDO::getFailReason)
                    .findFirst()
                    .orElse("存在加组失败结果"));
        } else {
            detail.setErrorMsg(null);
        }
        operationTaskDetailMapper.updateById(detail);
        releaseOperationAccountRunning(detail);

        // 更新主任务的统计信息
        updateTaskStatistics(detail.getTaskId());
    }

    @Override
    public List<FbOperationTaskDetailItemRespVO> getPendingDetails(String accountId) {
        List<FbOperationTaskDetailDO> details = operationTaskDetailMapper.selectPendingDetailsByAccountId(accountId);
        return BeanUtils.toBean(details, FbOperationTaskDetailItemRespVO.class);
    }

    @Override
    @Transactional(rollbackFor = Exception.class)
    public void batchSaveRepostResult(FbRepostResultBatchSaveReqVO batchSaveReqVO) {
        Long detailId = batchSaveReqVO.getDetailId();

        // 获取明细信息
        FbOperationTaskDetailDO detail = operationTaskDetailMapper.selectById(detailId);
        if (detail == null) {
            throw exception(OPERATION_TASK_DETAIL_NOT_EXISTS);
        }

        // 批量保存转帖结果
        List<FbRepostResultDO> results = batchSaveReqVO.getResults().stream()
                .map(item -> {
                    FbRepostResultDO result = new FbRepostResultDO();
                    result.setDetailId(detailId);
                    result.setTaskId(detail.getTaskId());
                    result.setAccountId(item.getAccountId());
                    result.setFbAccount(item.getFbAccount());
                    result.setPostUrl(item.getPostUrl());
                    result.setActionType(item.getActionType());
                    result.setTargetType(item.getTargetType());
                    result.setTargetId(item.getTargetId());
                    result.setTargetName(item.getTargetName());
                    result.setTargetUrl(item.getTargetUrl());
                    result.setStatus(item.getStatus());
                    result.setFailReason(item.getFailReason());
                    result.setExecuteTime(item.getExecuteTime());
                    result.setRemark(item.getRemark());
                    return result;
                })
                .collect(Collectors.toList());

        repostResultMapper.insertBatch(results);

        results.stream()
                .filter(r -> r.getActionType() != null && r.getActionType() == FOLLOW_ACTION_TYPE)
                .filter(r -> r.getStatus() != null && (r.getStatus() == 1 || r.getStatus() == 3))
                .forEach(r -> dailyLimitService.useOnce(r.getAccountId(), OperationTypeEnum.FOLLOW));

        FbOperationTaskDO parentTask = operationTaskMapper.selectById(detail.getTaskId());
        Integer parentTaskType = parentTask == null ? null : parentTask.getTaskType();
        if (POST_COMMENT_TASK_TYPE == parentTaskType) {
            results.stream()
                    .filter(r -> r.getActionType() != null && r.getActionType() == 6)
                    .filter(r -> r.getStatus() != null && (r.getStatus() == 1 || r.getStatus() == 3))
                    .forEach(r -> dailyLimitService.useOnce(r.getAccountId(), OperationTypeEnum.COMMENT));
        } else if (REPOST_TASK_TYPE == parentTaskType) {
            results.stream()
                    .filter(r -> r.getStatus() != null && (r.getStatus() == 1 || r.getStatus() == 3))
                    .forEach(r -> dailyLimitService.useOnce(r.getAccountId(), OperationTypeEnum.REPOST));
        }

        // actualCount = 成功数（含待审核）；任务完结 = 全部执行项已回报（成败都算执行完）
        int successCount = (int) results.stream()
                .filter(r -> r.getStatus() != null && (r.getStatus() == 1 || r.getStatus() == 3))
                .count();
        int executedInBatch = (int) results.stream()
                .filter(r -> r.getStatus() != null && r.getStatus() != 0)
                .count();

        int prevActual = detail.getActualCount() != null ? detail.getActualCount() : 0;
        detail.setActualCount(prevActual + successCount);

        int expected = detail.getExpectedCount() != null ? detail.getExpectedCount() : 0;
        int totalReported = repostResultMapper.selectListByDetailId(detailId).size();
        boolean allExecuted = expected > 0
                && (totalReported >= expected || executedInBatch >= expected);
        if (allExecuted) {
            detail.setStatus(2); // 已完成（全部执行完毕，允许部分失败）
            detail.setEndTime(LocalDateTime.now());
            if (successCount < executedInBatch) {
                String failMsg = results.stream()
                        .filter(r -> r.getStatus() != null && r.getStatus() == 2 && StrUtil.isNotBlank(r.getFailReason()))
                        .map(FbRepostResultDO::getFailReason)
                        .findFirst()
                        .orElse("部分执行项失败");
                detail.setErrorMsg(failMsg);
            } else {
                detail.setErrorMsg(null);
            }
        } else if (detail.getStartTime() == null) {
            detail.setStatus(1); // 执行中
        }
        if (detail.getStartTime() == null) {
            detail.setStartTime(LocalDateTime.now());
        }
        operationTaskDetailMapper.updateById(detail);
        releaseOperationAccountRunning(detail);

        // 更新主任务的统计信息
        updateTaskStatistics(detail.getTaskId());
    }

    @Override
    @Transactional(rollbackFor = Exception.class)
    public void markDetailFailed(Long detailId, String errorMsg) {
        if (detailId == null) {
            return;
        }
        FbOperationTaskDetailDO detail = operationTaskDetailMapper.selectById(detailId);
        if (detail == null || Integer.valueOf(2).equals(detail.getStatus()) || Integer.valueOf(3).equals(detail.getStatus())) {
            return;
        }
        detail.setStatus(3);
        detail.setErrorMsg(StrUtil.blankToDefault(errorMsg, "运营执行超时"));
        detail.setEndTime(LocalDateTime.now());
        operationTaskDetailMapper.updateById(detail);
        releaseOperationAccountRunning(detail);
        updateTaskStatistics(detail.getTaskId());
    }

    /**
     * 补全明细中的 FB 账号（兼容历史数据）
     */
    private void enrichRepostResultFbAccount(List<FbRepostResultRespVO> repostResults) {
        if (CollUtil.isEmpty(repostResults)) {
            return;
        }
        for (FbRepostResultRespVO result : repostResults) {
            if (StrUtil.isNotBlank(result.getFbAccount()) || StrUtil.isBlank(result.getAccountId())) {
                continue;
            }
            result.setFbAccount(resolveFbAccount(result.getAccountId()));
        }
    }

    private void enrichGroupPublishResultFbAccount(List<FbGroupPublishResultRespVO> groupPublishResults) {
        if (CollUtil.isEmpty(groupPublishResults)) {
            return;
        }
        for (FbGroupPublishResultRespVO result : groupPublishResults) {
            if (StrUtil.isNotBlank(result.getFbAccount()) || StrUtil.isBlank(result.getAccountId())) {
                continue;
            }
            result.setFbAccount(resolveFbAccount(result.getAccountId()));
        }
    }

    private void enrichDetailFbAccount(List<FbOperationTaskDetailRespVO.FbOperationTaskDetailItemVO> detailItems) {
        if (CollUtil.isEmpty(detailItems)) {
            return;
        }
        for (FbOperationTaskDetailRespVO.FbOperationTaskDetailItemVO detailItem : detailItems) {
            if (StrUtil.isNotBlank(detailItem.getFbAccount()) || StrUtil.isBlank(detailItem.getAccountId())) {
                continue;
            }
            detailItem.setFbAccount(resolveFbAccount(detailItem.getAccountId()));
        }
    }

    /**
     * 根据账号主键解析 FB 账号
     */
    private String resolveFbAccount(String accountId) {
        if (StrUtil.isBlank(accountId)) {
            return "";
        }
        try {
            FbAccountDO account = fbAccountMapper.selectById(Long.valueOf(accountId.trim()));
            return account != null ? StrUtil.nullToEmpty(account.getFbAccount()) : "";
        } catch (NumberFormatException ex) {
            return "";
        }
    }

    /**
     * 解析 WPF 回传的 ISO 时间字符串
     */
    private LocalDateTime parseFlexibleDateTime(Object value) {
        if (value == null) {
            return null;
        }
        if (value instanceof LocalDateTime) {
            return (LocalDateTime) value;
        }
        String text = String.valueOf(value).trim();
        if (StrUtil.isBlank(text)) {
            return null;
        }
        try {
            return OffsetDateTime.parse(text).toLocalDateTime();
        } catch (Exception ignored) {
            // ignore
        }
        try {
            return LocalDateTime.parse(text, DateTimeFormatter.ISO_LOCAL_DATE_TIME);
        } catch (Exception ignored) {
            // ignore
        }
        try {
            String normalized = text.replace("Z", "").split("\\.")[0];
            return LocalDateTime.parse(normalized, DateTimeFormatter.ISO_LOCAL_DATE_TIME);
        } catch (Exception ignored) {
            // ignore
        }
        return LocalDateTime.now();
    }

    /**
     * 校验运营任务是否存在
     */
    private void validateOperationTaskExists(Long id) {
        if (operationTaskMapper.selectById(id) == null) {
            throw exception(OPERATION_TASK_NOT_EXISTS);
        }
    }

    /**
     * 更新任务统计信息
     */
    private void updateTaskStatistics(Long taskId) {
        // 获取所有明细
        List<FbOperationTaskDetailDO> details = operationTaskDetailMapper.selectListByTaskId(taskId);
        if (CollUtil.isEmpty(details)) {
            return;
        }

        // 计算总实际完成数量
        int totalActualCount = details.stream()
                .mapToInt(d -> d.getActualCount() != null ? d.getActualCount() : 0)
                .sum();

        // 判断任务状态
        int status;
        long completedCount = details.stream().filter(d -> d.getStatus() != null && d.getStatus() == 2).count();
        long failedCount = details.stream().filter(d -> d.getStatus() != null && d.getStatus() == 3).count();
        long finishedCount = completedCount + failedCount;

        if (finishedCount == details.size()) {
            status = 2; // 已完成（所有明细均已执行，允许部分失败）
        } else {
            status = 1; // 执行中
        }

        // 更新主任务
        FbOperationTaskDO task = new FbOperationTaskDO();
        task.setId(taskId);
        task.setActualCount(totalActualCount);
        task.setStatus(status);
        if (status == 2) {
            task.setEndTime(LocalDateTime.now());
        }
        operationTaskMapper.updateById(task);
    }

    private void pushOperationDetailsToAccountQueue(List<FbOperationTaskDetailDO> details) {
        if (CollUtil.isEmpty(details)) {
            return;
        }
        details.forEach(this::pushOperationDetailToAccountQueue);
    }

    private void pushOperationDetailToAccountQueue(FbOperationTaskDetailDO detail) {
        if (detail == null || detail.getId() == null) {
            return;
        }
        String fbAccount = StrUtil.blankToDefault(detail.getFbAccount(), resolveFbAccount(detail.getAccountId()));
        if (StrUtil.isBlank(fbAccount)) {
            return;
        }
        accountTaskQueueService.push("operation", detail.getId(), fbAccount);
    }

    private void releaseOperationAccountRunning(FbOperationTaskDetailDO detail) {
        if (detail == null) {
            return;
        }
        String fbAccount = StrUtil.blankToDefault(detail.getFbAccount(), resolveFbAccount(detail.getAccountId()));
        if (StrUtil.isNotBlank(fbAccount)) {
            accountTaskQueueService.releaseRunning(fbAccount);
        }
    }

    private List<JSONObject> allocateAddGroupTargets(String rawConfig, int accountCount) {
        if (accountCount <= 0) {
            return Collections.emptyList();
        }
        JSONObject config = parseActionConfig(rawConfig);
        JSONArray selectedGroups = config.getJSONArray("selectedGroups");
        if (selectedGroups == null || selectedGroups.isEmpty()) {
            selectedGroups = config.getJSONArray("groups");
        }
        if (selectedGroups == null || selectedGroups.isEmpty()) {
            return Collections.emptyList();
        }
        int usedAccountCount = Math.min(accountCount, selectedGroups.size());
        int groupsPerAccount = (int) Math.ceil(selectedGroups.size() * 1.0 / usedAccountCount);
        List<JSONObject> result = new ArrayList<>();
        for (int i = 0; i < usedAccountCount; i++) {
            int start = i * groupsPerAccount;
            int end = Math.min(start + groupsPerAccount, selectedGroups.size());
            JSONArray groups = JSONUtil.createArray();
            for (int index = start; index < end; index++) {
                Object rawGroup = selectedGroups.get(index);
                JSONObject group = rawGroup instanceof JSONObject
                        ? (JSONObject) rawGroup
                        : JSONUtil.parseObj(rawGroup);
                JSONObject normalized = JSONUtil.createObj();
                normalized.set("groupId", StrUtil.blankToDefault(group.getStr("groupId"), group.getStr("id")));
                normalized.set("groupName", StrUtil.blankToDefault(group.getStr("groupName"), group.getStr("name")));
                normalized.set("groupUrl", StrUtil.blankToDefault(group.getStr("groupUrl"), group.getStr("url")));
                groups.add(normalized);
            }
            JSONObject detailConfig = JSONUtil.createObj();
            detailConfig.set("groups", groups);
            result.add(detailConfig);
        }
        return result;
    }

    private List<JSONObject> allocateGroupPublishTargets(String rawConfig, int accountCount) {
        if (accountCount <= 0) {
            return Collections.emptyList();
        }
        JSONObject config = parseActionConfig(rawConfig);
        JSONArray joinedGroups = config.getJSONArray("selectedGroups");
        JSONArray unjoinedGroups = config.getJSONArray("selectedUnjoinedGroups");
        JSONArray targets = joinedGroups != null && !joinedGroups.isEmpty() ? joinedGroups : unjoinedGroups;
        String targetField = joinedGroups != null && !joinedGroups.isEmpty() ? "selectedGroups" : "selectedUnjoinedGroups";
        if (targets == null || targets.isEmpty()) {
            return Collections.emptyList();
        }
        int usedAccountCount = Math.min(accountCount, targets.size());
        int groupsPerAccount = (int) Math.ceil(targets.size() * 1.0 / usedAccountCount);
        List<JSONObject> result = new ArrayList<>();
        for (int i = 0; i < usedAccountCount; i++) {
            int start = i * groupsPerAccount;
            int end = Math.min(start + groupsPerAccount, targets.size());
            JSONObject detailConfig = JSONUtil.parseObj(config.toString());
            JSONArray groups = JSONUtil.createArray();
            for (int index = start; index < end; index++) {
                groups.add(targets.get(index));
            }
            detailConfig.set("selectedGroups", JSONUtil.createArray());
            detailConfig.set("selectedUnjoinedGroups", JSONUtil.createArray());
            detailConfig.set(targetField, groups);
            result.add(detailConfig);
        }
        return result;
    }

    private int calculateGroupPublishExpectedCount(JSONObject detailConfig) {
        JSONArray joinedGroups = detailConfig.getJSONArray("selectedGroups");
        if (joinedGroups != null && !joinedGroups.isEmpty()) {
            return joinedGroups.size();
        }
        JSONArray unjoinedGroups = detailConfig.getJSONArray("selectedUnjoinedGroups");
        return unjoinedGroups == null ? 0 : unjoinedGroups.size();
    }

    private List<String> normalizeAccountIds(List<String> accountIds) {
        if (CollUtil.isEmpty(accountIds)) {
            return Collections.emptyList();
        }
        return accountIds.stream()
                .filter(StrUtil::isNotBlank)
                .map(String::trim)
                .distinct()
                .collect(Collectors.toList());
    }

    private JSONObject parseActionConfig(String rawConfig) {
        if (StrUtil.isBlank(rawConfig)) {
            return JSONUtil.createObj();
        }
        try {
            return JSONUtil.parseObj(rawConfig);
        } catch (Exception ex) {
            throw invalidParamException("执行项配置格式不正确");
        }
    }

    private List<Integer> parseActionList(JSONObject config) {
        JSONArray actions = config.getJSONArray("actions");
        if (actions == null) {
            return Collections.emptyList();
        }
        return actions.stream()
                .map(item -> {
                    if (item instanceof Number) {
                        return ((Number) item).intValue();
                    }
                    return Integer.parseInt(String.valueOf(item));
                })
                .distinct()
                .collect(Collectors.toList());
    }

    private List<String> normalizePostUrls(List<String> postUrls, JSONObject actionConfig) {
        Set<String> normalized = new LinkedHashSet<>();
        if (CollUtil.isNotEmpty(postUrls)) {
            postUrls.forEach(postUrl -> addNormalizedPostUrl(normalized, postUrl));
        }
        JSONArray configPostUrls = actionConfig.getJSONArray("postUrls");
        if (configPostUrls != null) {
            configPostUrls.forEach(item -> addNormalizedPostUrl(normalized, item == null ? null : String.valueOf(item)));
        }
        addNormalizedPostUrl(normalized, actionConfig.getStr("postUrl"));
        return new ArrayList<>(normalized);
    }

    private List<String> resolveActionTypes(Integer taskType, String rawConfig) {
        if (Integer.valueOf(14).equals(taskType)) return Collections.singletonList("dm");
        if (Integer.valueOf(10).equals(taskType)) return Collections.singletonList("repost");
        if (Integer.valueOf(9).equals(taskType)) return Collections.singletonList("join_group");
        if (Integer.valueOf(16).equals(taskType)) return Collections.singletonList("follow");
        JSONObject config = parseActionConfig(rawConfig);
        JSONArray actions = config.getJSONArray("actions");
        if (actions != null && actions.toList(Integer.class).contains(6)) {
            return Collections.singletonList("comment");
        }
        return Collections.emptyList();
    }

    private String resolveFollowTargetUrl(FbOperationTaskSaveReqVO createReqVO, JSONObject actionConfig) {
        if (StrUtil.isNotBlank(actionConfig.getStr("targetUrl"))) {
            return actionConfig.getStr("targetUrl").trim();
        }
        if (StrUtil.isNotBlank(actionConfig.getStr("postUrl"))) {
            return actionConfig.getStr("postUrl").trim();
        }
        if (StrUtil.isNotBlank(createReqVO.getPostUrl())) {
            return createReqVO.getPostUrl().trim();
        }
        if (StrUtil.isNotBlank(createReqVO.getTargetUrls())) {
            return createReqVO.getTargetUrls().split("\\r?\\n")[0].trim();
        }
        return "";
    }

    private void addNormalizedPostUrl(Set<String> target, String rawUrl) {
        if (StrUtil.isBlank(rawUrl)) {
            return;
        }
        String[] lines = rawUrl.split("\\r?\\n");
        for (String line : lines) {
            String url = StrUtil.trim(line);
            if (StrUtil.isNotBlank(url)) {
                target.add(url);
            }
        }
    }

    private List<String> parseScriptList(JSONObject config, String field) {
        JSONArray scripts = config.getJSONArray(field);
        if (scripts == null) {
            return Collections.emptyList();
        }
        return scripts.stream()
                .map(item -> item == null ? "" : String.valueOf(item).trim())
                .filter(StrUtil::isNotBlank)
                .collect(Collectors.toList());
    }

    private AccountAllocationState pickNextCommentAccount(List<AccountAllocationState> accounts) {
        return accounts.stream()
                .filter(account -> account.commentAssigned < account.commentRemaining)
                .sorted(Comparator
                        .comparingInt(AccountAllocationState::getCommentLoadRate)
                        .thenComparingInt(account -> account.commentAssigned)
                        .thenComparing(account -> account.accountId))
                .findFirst()
                .orElse(null);
    }

    private AccountAllocationState pickNextLikeAccount(List<AccountAllocationState> accounts) {
        return accounts.stream()
                .sorted(Comparator
                        .comparingInt((AccountAllocationState account) -> account.likeAssigned)
                        .thenComparingInt(account -> account.commentAssigned)
                        .thenComparing(account -> account.accountId))
                .findFirst()
                .orElse(accounts.get(0));
    }

    private DetailAllocation getOrCreateAllocation(Map<String, DetailAllocation> allocations,
                                                   AccountAllocationState account,
                                                   String postUrl) {
        String key = buildAllocationKey(account.accountId, postUrl);
        DetailAllocation existing = allocations.get(key);
        if (existing != null) {
            return existing;
        }
        DetailAllocation allocation = new DetailAllocation();
        allocation.accountId = account.accountId;
        allocation.fbAccount = account.fbAccount;
        allocation.postUrl = postUrl;
        allocations.put(key, allocation);
        return allocation;
    }

    private String buildAllocationKey(String accountId, String postUrl) {
        return accountId + "||" + postUrl;
    }

    private DetailAllocation findAllocationByPost(Map<String, DetailAllocation> allocations, String postUrl) {
        return allocations.values().stream()
                .filter(item -> StrUtil.equals(item.postUrl, postUrl))
                .findFirst()
                .orElse(null);
    }

    private String buildFinalCommentText(List<String> commentScripts, boolean appendRandomEmoji) {
        if (CollUtil.isEmpty(commentScripts)) {
            return null;
        }
        String base = commentScripts.get(ThreadLocalRandom.current().nextInt(commentScripts.size()));
        if (StrUtil.isBlank(base)) {
            return null;
        }
        String normalized = base.trim();
        if (!appendRandomEmoji) {
            return normalized;
        }
        return normalized + " " + generateRandomEmojiSuffix();
    }

    private String generateRandomEmojiSuffix() {
        String[] emojiPool = new String[] {
                "\uD83D\uDE00",
                "\uD83D\uDE04",
                "\uD83D\uDE01",
                "\uD83D\uDE0A",
                "\uD83D\uDE09",
                "\uD83D\uDE42",
                "\uD83D\uDE0D",
                "\uD83E\uDD70",
                "\uD83E\uDD1D",
                "\uD83D\uDC4D",
                "\uD83D\uDD25",
                "\u2764"
        };
        int count = ThreadLocalRandom.current().nextInt(1, 3);
        StringBuilder builder = new StringBuilder();
        for (int i = 0; i < count; i++) {
            builder.append(emojiPool[ThreadLocalRandom.current().nextInt(emojiPool.length)]);
        }
        return builder.toString();
    }

    private static class AccountAllocationState {
        private final String accountId;
        private final String fbAccount;
        private final int commentRemaining;
        private int commentAssigned;
        private int likeAssigned;

        private AccountAllocationState(String accountId, String fbAccount, int commentRemaining) {
            this.accountId = accountId;
            this.fbAccount = fbAccount;
            this.commentRemaining = Math.max(commentRemaining, 0);
        }

        private int getCommentLoadRate() {
            if (commentRemaining <= 0) {
                return Integer.MAX_VALUE;
            }
            return (commentAssigned * 1000) / commentRemaining;
        }
    }

    private static class DetailAllocation {
        private String accountId;
        private String fbAccount;
        private String postUrl;
        private String commentScript;
        private final Set<Integer> actions = new LinkedHashSet<>();
    }

}
