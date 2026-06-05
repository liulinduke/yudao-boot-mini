package cn.iocoder.yudao.module.facebook.service.operation;

import cn.hutool.core.collection.CollUtil;
import cn.hutool.core.util.StrUtil;
import cn.iocoder.yudao.framework.common.pojo.PageResult;
import cn.iocoder.yudao.framework.common.util.object.BeanUtils;
import cn.iocoder.yudao.module.facebook.controller.admin.operation.vo.*;
import cn.iocoder.yudao.module.facebook.dal.dataobject.operation.FbOperationAddGroupResultDO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.operation.FbOperationTaskDO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.operation.FbOperationTaskDetailDO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.operation.FbRepostResultDO;
import cn.iocoder.yudao.module.facebook.dal.mysql.operation.FbOperationAddGroupResultMapper;
import cn.iocoder.yudao.module.facebook.dal.mysql.operation.FbOperationTaskDetailMapper;
import cn.iocoder.yudao.module.facebook.dal.mysql.operation.FbOperationTaskMapper;
import cn.iocoder.yudao.module.facebook.dal.mysql.operation.FbRepostResultMapper;
import cn.iocoder.yudao.module.facebook.dal.dataobject.account.FbAccountDO;
import cn.iocoder.yudao.module.facebook.dal.mysql.account.FbAccountMapper;
import cn.iocoder.yudao.framework.mybatis.core.query.LambdaQueryWrapperX;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;
import org.springframework.validation.annotation.Validated;

import javax.annotation.Resource;

import java.time.LocalDateTime;
import java.time.OffsetDateTime;
import java.time.format.DateTimeFormatter;
import java.util.ArrayList;
import java.util.List;
import java.util.Map;
import java.util.stream.Collectors;

import static cn.iocoder.yudao.framework.common.exception.util.ServiceExceptionUtil.exception;
import static cn.iocoder.yudao.module.facebook.enums.ErrorCodeConstants.*;

/**
 * 运营任务 Service 实现类
 *
 * @author 芋道源码
 */
@Service
@Validated
public class FbOperationTaskServiceImpl implements FbOperationTaskService {

    @Resource
    private FbOperationTaskMapper operationTaskMapper;

    @Resource
    private FbOperationTaskDetailMapper operationTaskDetailMapper;

    @Resource
    private FbOperationAddGroupResultMapper addGroupResultMapper;

    @Resource
    private FbRepostResultMapper repostResultMapper;

    @Resource
    private FbAccountMapper fbAccountMapper;

    @Override
    @Transactional(rollbackFor = Exception.class)
    public Long createOperationTask(FbOperationTaskSaveReqVO createReqVO) {
        // 1. 创建主任务
        FbOperationTaskDO task = BeanUtils.toBean(createReqVO, FbOperationTaskDO.class);
        task.setStatus(0); // 待执行
        task.setActualCount(0);
        task.setAccountIds(String.join(",", createReqVO.getAccountIds()));
        operationTaskMapper.insert(task);

        // 2. 规范化账号ID并查询账号信息映射
        List<String> normalizedAccountIds = createReqVO.getAccountIds().stream()
                .map(id -> String.valueOf(id).trim())
                .collect(Collectors.toList());
        List<Long> accountIdLongs = normalizedAccountIds.stream()
                .map(Long::valueOf)
                .collect(Collectors.toList());
        List<FbAccountDO> accountList = fbAccountMapper.selectBatchIds(accountIdLongs);
        Map<Long, String> accountIdToFbAccountMap = accountList.stream()
                .collect(Collectors.toMap(FbAccountDO::getId, FbAccountDO::getFbAccount, (a, b) -> a));

        // 3. 为每个账号创建明细
        List<FbOperationTaskDetailDO> details = new ArrayList<>();
        for (String accountIdStr : normalizedAccountIds) {
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
            detail.setTargetUrls(createReqVO.getTargetUrls());
            detail.setTargetGroupIds(createReqVO.getTargetGroupIds());
            detail.setExpectedCount(createReqVO.getExpectedCount());
            detail.setActualCount(0);
            detail.setStatus(0); // 待执行
            operationTaskDetailMapper.insert(detail);
            details.add(detail);
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
        }
    }

    @Override
    public FbOperationTaskDetailRespVO getOperationTask(Long id) {
        // 获取主任务
        FbOperationTaskDO task = operationTaskMapper.selectById(id);
        if (task == null) {
            throw exception(OPERATION_TASK_NOT_EXISTS);
        }

        FbOperationTaskDetailRespVO respVO = new FbOperationTaskDetailRespVO();
        respVO.setTask(BeanUtils.toBean(task, FbOperationTaskRespVO.class));

        // 获取明细列表
        List<FbOperationTaskDetailDO> details = operationTaskDetailMapper.selectListByTaskId(id);
        List<FbOperationTaskDetailRespVO.FbOperationTaskDetailItemVO> detailItems = BeanUtils.toBean(details, FbOperationTaskDetailRespVO.FbOperationTaskDetailItemVO.class);
        enrichDetailFbAccount(detailItems);
        respVO.setDetails(detailItems);

        // 获取结果列表（链接加组任务）
        if (task.getTaskType() != null && task.getTaskType() == 9) {
            List<FbOperationAddGroupResultDO> results = addGroupResultMapper.selectListByTaskId(id);
            respVO.setResults(BeanUtils.toBean(results, FbOperationAddGroupResultRespVO.class));
        }

        return respVO;
    }

    @Override
    public PageResult<FbOperationTaskRespVO> getOperationTaskPage(FbOperationTaskPageReqVO pageReqVO) {
        PageResult<FbOperationTaskDO> pageResult = operationTaskMapper.selectPage(pageReqVO,
                new LambdaQueryWrapperX<FbOperationTaskDO>()
                        .eqIfPresent(FbOperationTaskDO::getTaskType, pageReqVO.getTaskType())
                        .eqIfPresent(FbOperationTaskDO::getStatus, pageReqVO.getStatus())
                        .betweenIfPresent(FbOperationTaskDO::getCreateTime, pageReqVO.getCreateTime())
                        .orderByDesc(FbOperationTaskDO::getId));
        return BeanUtils.toBean(pageResult, FbOperationTaskRespVO.class);
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

        // 更新明细的实际完成数量和状态
        int successCount = (int) results.stream()
                .filter(r -> r.getStatus() != null && r.getStatus() == 1) // 1-成功
                .count();

        detail.setActualCount(detail.getActualCount() + successCount);
        if (detail.getActualCount() >= detail.getExpectedCount()) {
            detail.setStatus(2); // 已完成
            detail.setEndTime(LocalDateTime.now());
        }
        operationTaskDetailMapper.updateById(detail);

        // 更新主任务的统计信息
        updateTaskStatistics(detail.getTaskId());
    }

    /**
     * 补全明细中的 FB 账号（兼容历史数据）
     */
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

        if (completedCount == details.size()) {
            status = 2; // 已完成
        } else if (failedCount > 0) {
            status = 4; // 失败
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

}
