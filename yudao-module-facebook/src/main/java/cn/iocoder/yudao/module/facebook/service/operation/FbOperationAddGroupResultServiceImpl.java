package cn.iocoder.yudao.module.facebook.service.operation;

import cn.iocoder.yudao.framework.common.pojo.PageResult;
import cn.iocoder.yudao.framework.common.util.object.BeanUtils;
import cn.iocoder.yudao.module.facebook.controller.admin.operation.vo.FbOperationAddGroupResultPageReqVO;
import cn.iocoder.yudao.module.facebook.controller.admin.operation.vo.FbOperationAddGroupResultRespVO;
import cn.iocoder.yudao.module.facebook.controller.admin.operation.vo.FbOperationGroupSelectorAccountReqVO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.operation.FbOperationAddGroupResultDO;
import cn.iocoder.yudao.module.facebook.dal.mysql.operation.FbOperationAddGroupResultMapper;
import cn.iocoder.yudao.framework.mybatis.core.query.LambdaQueryWrapperX;
import org.springframework.stereotype.Service;
import org.springframework.validation.annotation.Validated;

import jakarta.annotation.Resource;
import java.util.List;
import java.util.stream.Collectors;
import java.time.LocalDateTime;
import cn.iocoder.yudao.module.facebook.dal.dataobject.fbcollectgroup.FbCollectGroupDO;
import cn.iocoder.yudao.module.facebook.dal.mysql.fbcollectgroup.FbCollectGroupMapper;
import cn.iocoder.yudao.module.facebook.dal.dataobject.operation.FbOperationTaskDO;
import cn.iocoder.yudao.module.facebook.dal.mysql.operation.FbOperationTaskMapper;
import cn.iocoder.yudao.module.facebook.dal.dataobject.account.FbAccountDO;
import cn.iocoder.yudao.module.facebook.dal.mysql.account.FbAccountMapper;
import cn.iocoder.yudao.module.facebook.service.account.FbAccountTaskAllocationService;
import java.util.Map;
import java.util.HashMap;
import java.util.Set;
import java.util.LinkedHashSet;

/**
 * 链接加组结果 Service 实现类
 *
 * @author 芋道源码
 */
@Service
@Validated
public class FbOperationAddGroupResultServiceImpl implements FbOperationAddGroupResultService {

    @Resource
    private FbOperationAddGroupResultMapper addGroupResultMapper;
    @Resource
    private FbCollectGroupMapper collectGroupMapper;
    @Resource
    private FbOperationTaskMapper operationTaskMapper;
    @Resource
    private FbAccountMapper accountMapper;
    @Resource
    private FbAccountTaskAllocationService accountAllocationService;

    @Override
    public PageResult<FbOperationAddGroupResultRespVO> getAddGroupResultPage(FbOperationAddGroupResultPageReqVO pageReqVO) {
        LambdaQueryWrapperX<FbOperationAddGroupResultDO> wrapper = new LambdaQueryWrapperX<FbOperationAddGroupResultDO>()
                        .eqIfPresent(FbOperationAddGroupResultDO::getTaskId, pageReqVO.getTaskId())
                        .eqIfPresent(FbOperationAddGroupResultDO::getDetailId, pageReqVO.getDetailId())
                        .eqIfPresent(FbOperationAddGroupResultDO::getAccountId, pageReqVO.getAccountId())
                        .inIfPresent(FbOperationAddGroupResultDO::getAccountId, pageReqVO.getAccountIds())
                        .eqIfPresent(FbOperationAddGroupResultDO::getJoinStatus, pageReqVO.getJoinStatus())
                        .likeIfPresent(FbOperationAddGroupResultDO::getGroupId, pageReqVO.getGroupId())
                        .likeIfPresent(FbOperationAddGroupResultDO::getGroupName, pageReqVO.getGroupName());
        if (pageReqVO.getJoinedBeforeDays() != null && pageReqVO.getJoinedBeforeDays() > 0) {
            wrapper.le(FbOperationAddGroupResultDO::getJoinTime, LocalDateTime.now().minusDays(pageReqVO.getJoinedBeforeDays()));
        }
        // 选择发群帖群组时，只展示加组任务产生的已加入记录，发帖结果不作为新的群组来源。
        if (pageReqVO.getTaskId() == null && pageReqVO.getDetailId() == null
                && pageReqVO.getAccountIds() != null && !pageReqVO.getAccountIds().isEmpty()) {
            List<Long> joinTaskIds = operationTaskMapper.selectList(new LambdaQueryWrapperX<FbOperationTaskDO>()
                    .eq(FbOperationTaskDO::getTaskType, 9)).stream().map(FbOperationTaskDO::getId).collect(Collectors.toList());
            if (joinTaskIds.isEmpty()) {
                wrapper.eq(FbOperationAddGroupResultDO::getTaskId, -1L);
            } else {
                wrapper.in(FbOperationAddGroupResultDO::getTaskId, joinTaskIds);
            }
        }
        if (pageReqVO.getResourceGroupId() != null) {
            List<String> groupIds = collectGroupMapper.selectList(new LambdaQueryWrapperX<FbCollectGroupDO>()
                    .eq(FbCollectGroupDO::getResourceGroupId, pageReqVO.getResourceGroupId()))
                    .stream().map(FbCollectGroupDO::getGroupId).filter(java.util.Objects::nonNull)
                    .map(String::valueOf).collect(Collectors.toList());
            if (groupIds.isEmpty()) {
                wrapper.eq(FbOperationAddGroupResultDO::getGroupId, "__NO_RESOURCE_GROUP__");
            } else {
                wrapper.in(FbOperationAddGroupResultDO::getGroupId, groupIds);
            }
        }
        PageResult<FbOperationAddGroupResultDO> pageResult = addGroupResultMapper.selectPage(pageReqVO,
                wrapper.orderByDesc(FbOperationAddGroupResultDO::getId));
        PageResult<FbOperationAddGroupResultRespVO> response = BeanUtils.toBean(pageResult, FbOperationAddGroupResultRespVO.class);
        Map<String, String> accountNames = new HashMap<>();
        List<Long> accountIds = new java.util.ArrayList<>();
        response.getList().forEach(row -> {
            try {
                if (row.getAccountId() != null) accountIds.add(Long.valueOf(row.getAccountId()));
            } catch (NumberFormatException ignored) {
                // 某些历史记录可能直接保存了 Facebook 账号字符串。
            }
        });
        if (!accountIds.isEmpty()) {
            for (FbAccountDO account : accountMapper.selectBatchIds(accountIds)) {
                accountNames.put(String.valueOf(account.getId()), account.getFbAccount());
            }
        }
        response.getList().forEach(row -> {
            String fbAccount = accountNames.get(row.getAccountId());
            if (fbAccount != null) row.setFbAccount(fbAccount);
        });
        List<Long> publishTaskIds = operationTaskMapper.selectList(new LambdaQueryWrapperX<FbOperationTaskDO>()
                .eq(FbOperationTaskDO::getTaskType, 13)).stream().map(FbOperationTaskDO::getId).collect(Collectors.toList());
        for (FbOperationAddGroupResultRespVO row : response.getList()) {
            if (publishTaskIds.isEmpty() || row.getAccountId() == null || row.getGroupId() == null) {
                row.setPublishCount(0);
                continue;
            }
            List<FbOperationAddGroupResultDO> history = addGroupResultMapper.selectList(new LambdaQueryWrapperX<FbOperationAddGroupResultDO>()
                    .in(FbOperationAddGroupResultDO::getTaskId, publishTaskIds)
                    .eq(FbOperationAddGroupResultDO::getAccountId, row.getAccountId())
                    .eq(FbOperationAddGroupResultDO::getGroupId, row.getGroupId())
                    .eq(FbOperationAddGroupResultDO::getJoinStatus, 1));
            row.setPublishCount(history.size());
            row.setLastPublishTime(history.stream().map(FbOperationAddGroupResultDO::getJoinTime)
                    .filter(java.util.Objects::nonNull).max(LocalDateTime::compareTo).orElse(null));
        }
        response.setList(response.getList().stream()
                .sorted(java.util.Comparator.comparing(FbOperationAddGroupResultRespVO::getPublishCount,
                        java.util.Comparator.nullsFirst(Integer::compareTo)).thenComparing(FbOperationAddGroupResultRespVO::getAccountId,
                        java.util.Comparator.nullsLast(String::compareTo)))
                .collect(Collectors.toList()));
        return response;
    }

    @Override
    public List<FbOperationAddGroupResultRespVO> getAddGroupResultByTaskId(Long taskId) {
        List<FbOperationAddGroupResultDO> list = addGroupResultMapper.selectListByTaskId(taskId);
        return BeanUtils.toBean(list, FbOperationAddGroupResultRespVO.class);
    }

    @Override
    public List<String> getSelectorAccountIds(FbOperationGroupSelectorAccountReqVO reqVO) {
        int targetCount = reqVO.getTargetAccountCount() == null ? 0 : reqVO.getTargetAccountCount();
        int minGroupCount = Math.max(reqVO.getMinGroupCount() == null ? 1 : reqVO.getMinGroupCount(), 1);
        if (targetCount <= 0) {
            return List.of();
        }
        LambdaQueryWrapperX<FbOperationAddGroupResultDO> wrapper = new LambdaQueryWrapperX<FbOperationAddGroupResultDO>()
                .eq(FbOperationAddGroupResultDO::getJoinStatus, 1);
        List<Long> joinTaskIds = operationTaskMapper.selectList(new LambdaQueryWrapperX<FbOperationTaskDO>()
                .eq(FbOperationTaskDO::getTaskType, 9)).stream()
                .map(FbOperationTaskDO::getId).collect(Collectors.toList());
        if (joinTaskIds.isEmpty()) {
            return List.of();
        }
        wrapper.in(FbOperationAddGroupResultDO::getTaskId, joinTaskIds);
        if (reqVO.getJoinedBeforeDays() != null && reqVO.getJoinedBeforeDays() > 0) {
            wrapper.le(FbOperationAddGroupResultDO::getJoinTime,
                    LocalDateTime.now().minusDays(reqVO.getJoinedBeforeDays()));
        }
        wrapper.likeIfPresent(FbOperationAddGroupResultDO::getGroupName, reqVO.getGroupName());
        if (reqVO.getAccountIds() != null && !reqVO.getAccountIds().isEmpty()) {
            wrapper.in(FbOperationAddGroupResultDO::getAccountId, reqVO.getAccountIds());
        }
        if (reqVO.getResourceGroupId() != null) {
            List<String> groupIds = collectGroupMapper.selectList(new LambdaQueryWrapperX<FbCollectGroupDO>()
                    .eq(FbCollectGroupDO::getResourceGroupId, reqVO.getResourceGroupId()))
                    .stream().map(FbCollectGroupDO::getGroupId).filter(java.util.Objects::nonNull)
                    .map(String::valueOf).collect(Collectors.toList());
            if (groupIds.isEmpty()) return List.of();
            wrapper.in(FbOperationAddGroupResultDO::getGroupId, groupIds);
        }
        Map<String, Set<String>> groupsByAccount = new HashMap<>();
        addGroupResultMapper.selectList(wrapper).forEach(row -> {
            if (row.getAccountId() != null && row.getGroupId() != null) {
                groupsByAccount.computeIfAbsent(row.getAccountId(), key -> new LinkedHashSet<>())
                        .add(row.getGroupId());
            }
        });
        List<Long> candidates = groupsByAccount.entrySet().stream()
                .filter(entry -> entry.getValue().size() >= minGroupCount)
                .map(entry -> parseLong(entry.getKey())).filter(java.util.Objects::nonNull)
                .collect(Collectors.toList());
        if (candidates.isEmpty()) return List.of();
        String actionType = "repost".equalsIgnoreCase(reqVO.getActionType()) ? "repost" : "group_post";
        List<Long> selected = accountAllocationService.selectAccounts(
                "MANUAL", candidates, targetCount, "operation", List.of(actionType));
        return selected.stream().map(String::valueOf).collect(Collectors.toList());
    }

    private Long parseLong(String value) {
        try { return value == null ? null : Long.valueOf(value); }
        catch (Exception ignored) { return null; }
    }

}
