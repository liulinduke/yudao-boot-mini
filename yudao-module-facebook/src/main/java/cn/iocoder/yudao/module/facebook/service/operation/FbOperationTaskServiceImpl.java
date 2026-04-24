package cn.iocoder.yudao.module.facebook.service.operation;

import cn.hutool.core.collection.CollUtil;
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
import cn.iocoder.yudao.framework.mybatis.core.query.LambdaQueryWrapperX;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;
import org.springframework.validation.annotation.Validated;

import javax.annotation.Resource;

import java.time.LocalDateTime;
import java.util.ArrayList;
import java.util.List;
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

    @Override
    @Transactional(rollbackFor = Exception.class)
    public Long createOperationTask(FbOperationTaskSaveReqVO createReqVO) {
        // 1. 创建主任务
        FbOperationTaskDO task = BeanUtils.toBean(createReqVO, FbOperationTaskDO.class);
        task.setStatus(0); // 待执行
        task.setActualCount(0);
        task.setAccountIds(String.join(",", createReqVO.getAccountIds()));
        operationTaskMapper.insert(task);

        // 2. 为每个账号创建明细
        List<FbOperationTaskDetailDO> details = new ArrayList<>();
        for (String accountId : createReqVO.getAccountIds()) {
            FbOperationTaskDetailDO detail = new FbOperationTaskDetailDO();
            detail.setTaskId(task.getId());
            detail.setAccountId(accountId);
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
        respVO.setDetails(detailItems);

        // 获取结果列表（仅链接加组任务）
        if (task.getTaskType() == 1) {
            List<FbOperationAddGroupResultDO> results = addGroupResultMapper.selectListByTaskId(id);
            // TODO: 需要创建对应的VO类
            // respVO.setResults(BeanUtils.toBean(results, FbOperationAddGroupResultRespVO.class));
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
        List<FbOperationAddGroupResultDO> results = batchSaveReqVO.getResults().stream()
                .map(item -> {
                    FbOperationAddGroupResultDO result = new FbOperationAddGroupResultDO();
                    result.setDetailId(detailId);
                    result.setTaskId(detail.getTaskId());
                    result.setAccountId(item.getAccountId());
                    result.setFbAccount(item.getFbAccount());
                    result.setTargetUrl(item.getTargetUrl());
                    result.setGroupId(item.getGroupId());
                    result.setGroupName(item.getGroupName());
                    result.setGroupUrl(item.getGroupUrl());
                    result.setJoinStatus(item.getJoinStatus());
                    result.setFailReason(item.getFailReason());
                    result.setJoinTime(item.getJoinTime());
                    result.setSyncTime(item.getSyncTime());
                    return result;
                })
                .collect(Collectors.toList());

        addGroupResultMapper.insertBatch(results);

        // 更新明细的实际完成数量和状态
        int successCount = (int) results.stream()
                .filter(r -> r.getJoinStatus() != null && r.getJoinStatus() == 1) // 1-成功
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
