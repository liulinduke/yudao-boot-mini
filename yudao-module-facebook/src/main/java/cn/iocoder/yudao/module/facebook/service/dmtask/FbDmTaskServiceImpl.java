package cn.iocoder.yudao.module.facebook.service.dmtask;

import cn.hutool.core.collection.CollUtil;
import cn.hutool.json.JSONUtil;
import cn.iocoder.yudao.framework.common.pojo.PageResult;
import cn.iocoder.yudao.framework.common.util.object.BeanUtils;
import cn.iocoder.yudao.module.facebook.controller.admin.dmtask.vo.FbDmTaskSaveReqVO;
import cn.iocoder.yudao.module.facebook.controller.admin.dmtask.vo.FbDmTaskRespVO;
import cn.iocoder.yudao.module.facebook.controller.admin.dmtask.vo.FbDmTaskPageReqVO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.dmtask.FbDmTaskDO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.dmtask.FbDmTaskDetailDO;
import cn.iocoder.yudao.module.facebook.dal.mysql.dmtask.FbDmTaskDetailMapper;
import cn.iocoder.yudao.module.facebook.dal.mysql.dmtask.FbDmTaskMapper;
import cn.iocoder.yudao.module.facebook.enums.OperationTypeEnum;
import cn.iocoder.yudao.module.facebook.service.dailylimit.FacebookDailyLimitService;
import com.baomidou.mybatisplus.core.conditions.query.LambdaQueryWrapper;
import javax.annotation.Resource;
import lombok.extern.slf4j.Slf4j;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;
import org.springframework.validation.annotation.Validated;

import java.time.LocalDateTime;
import java.util.*;
import java.util.stream.Collectors;

import static cn.iocoder.yudao.framework.common.exception.util.ServiceExceptionUtil.exception;
import static cn.iocoder.yudao.module.facebook.enums.ErrorCodeConstants.DM_TASK_NOT_EXISTS;

/**
 * Facebook 群发私信任务 Service 实现类
 *
 * @author 芋道源码
 */
@Slf4j
@Service
@Validated
public class FbDmTaskServiceImpl implements FbDmTaskService {

    @Resource
    private FbDmTaskMapper dmTaskMapper;

    @Resource
    private FbDmTaskDetailMapper dmTaskDetailMapper;

    @Resource
    private DmTaskAllocator taskAllocator;

    @Resource
    private FacebookDailyLimitService dailyLimitService;

    @Override
    @Transactional(rollbackFor = Exception.class)
    public Long createDmTask(FbDmTaskSaveReqVO saveReqVO) {
        // 1. 创建主任务
        FbDmTaskDO task = BeanUtils.toBean(saveReqVO, FbDmTaskDO.class);
        task.setTargetUserIds(JSONUtil.toJsonStr(saveReqVO.getTargetUserIds()));
        task.setScripts(JSONUtil.toJsonStr(saveReqVO.getScripts()));
        task.setAccountIds(JSONUtil.toJsonStr(saveReqVO.getAccountIds()));
        task.setStatus(0); // 待执行
        task.setTotalCount(saveReqVO.getTargetUserIds().size());
        task.setCompletedCount(0);
        task.setFailedCount(0);
        dmTaskMapper.insert(task);

        // 2. 使用分配器分配任务
        Map<String, List<String>> allocation = taskAllocator.allocate(
                saveReqVO.getAccountIds(),
                saveReqVO.getTargetUserIds()
        );

        // 3. 创建任务明细
        List<FbDmTaskDetailDO> details = new ArrayList<>();
        for (Map.Entry<String, List<String>> entry : allocation.entrySet()) {
            String accountId = entry.getKey();
            List<String> userIds = entry.getValue();

            for (String userId : userIds) {
                FbDmTaskDetailDO detail = new FbDmTaskDetailDO();
                detail.setTaskId(task.getId());
                detail.setAccountId(accountId);
                detail.setTargetUserId(userId);
                detail.setStatus(0); // 待执行
                details.add(detail);
            }
        }

        if (CollUtil.isNotEmpty(details)) {
            dmTaskDetailMapper.insertBatch(details);
        }

        log.info("创建群发私信任务成功，任务ID: {}, 总任务数: {}, 分配账号数: {}",
                task.getId(), task.getTotalCount(), allocation.size());

        return task.getId();
    }

    @Override
    @Transactional(rollbackFor = Exception.class)
    public void updateDmTask(FbDmTaskSaveReqVO saveReqVO) {
        validateDmTaskExists(saveReqVO.getId());
        FbDmTaskDO updateObj = BeanUtils.toBean(saveReqVO, FbDmTaskDO.class);
        updateObj.setTargetUserIds(JSONUtil.toJsonStr(saveReqVO.getTargetUserIds()));
        updateObj.setScripts(JSONUtil.toJsonStr(saveReqVO.getScripts()));
        updateObj.setAccountIds(JSONUtil.toJsonStr(saveReqVO.getAccountIds()));
        dmTaskMapper.updateById(updateObj);
    }

    @Override
    @Transactional(rollbackFor = Exception.class)
    public void deleteDmTask(Long id) {
        validateDmTaskExists(id);
        dmTaskMapper.deleteById(id);
        // 删除关联的明细
        dmTaskDetailMapper.delete(new LambdaQueryWrapper<FbDmTaskDetailDO>()
                .eq(FbDmTaskDetailDO::getTaskId, id));
    }

    @Override
    public FbDmTaskRespVO getDmTask(Long id) {
        FbDmTaskDO task = dmTaskMapper.selectById(id);
        if (task == null) {
            throw exception(DM_TASK_NOT_EXISTS);
        }
        FbDmTaskRespVO respVO = BeanUtils.toBean(task, FbDmTaskRespVO.class);
        // 解析JSON字符串为List
        respVO.setTargetUserIds(JSONUtil.toList(task.getTargetUserIds(), String.class));
        respVO.setScripts(JSONUtil.toList(task.getScripts(), String.class));
        respVO.setAccountIds(JSONUtil.toList(task.getAccountIds(), String.class));
        return respVO;
    }

    @Override
    public PageResult<FbDmTaskRespVO> getDmTaskPage(FbDmTaskPageReqVO pageReqVO) {
        PageResult<FbDmTaskDO> pageResult = dmTaskMapper.selectPage(pageReqVO);
        return BeanUtils.toBean(pageResult, FbDmTaskRespVO.class);
    }

    @Override
    @Transactional(rollbackFor = Exception.class)
    public void startTask(Long taskId) {
        FbDmTaskDO task = dmTaskMapper.selectById(taskId);
        if (task == null) {
            throw exception(DM_TASK_NOT_EXISTS);
        }

        // 更新任务状态为执行中
        task.setStatus(1);
        task.setStartTime(LocalDateTime.now());
        dmTaskMapper.updateById(task);

        log.info("启动群发私信任务，任务ID: {}", taskId);
        // TODO: 这里可以触发WPF执行任务的逻辑
    }

    @Override
    @Transactional(rollbackFor = Exception.class)
    public void cancelTask(Long taskId) {
        FbDmTaskDO task = dmTaskMapper.selectById(taskId);
        if (task == null) {
            throw exception(DM_TASK_NOT_EXISTS);
        }

        task.setStatus(4); // 已取消
        task.setEndTime(LocalDateTime.now());
        dmTaskMapper.updateById(task);

        log.info("取消群发私信任务，任务ID: {}", taskId);
    }

    @Override
    @Transactional(rollbackFor = Exception.class)
    public void updateDetailStatus(Long detailId, Integer status, String errorMsg) {
        FbDmTaskDetailDO detail = dmTaskDetailMapper.selectById(detailId);
        if (detail == null) {
            return;
        }

        detail.setStatus(status);
        detail.setErrorMsg(errorMsg);
        if (status == 1 || status == 2) { // 成功或失败
            detail.setSendTime(LocalDateTime.now());
            // 消耗一次次数
            dailyLimitService.useOnce(detail.getAccountId(), OperationTypeEnum.DM);
        }
        dmTaskDetailMapper.updateById(detail);

        // 更新主任务统计
        updateTaskStatistics(detail.getTaskId());
    }

    /**
     * 更新任务统计信息
     */
    private void updateTaskStatistics(Long taskId) {
        List<FbDmTaskDetailDO> details = dmTaskDetailMapper.selectListByTaskId(taskId);
        if (CollUtil.isEmpty(details)) {
            return;
        }

        long completedCount = details.stream().filter(d -> d.getStatus() != null && d.getStatus() == 1).count();
        long failedCount = details.stream().filter(d -> d.getStatus() != null && d.getStatus() == 2).count();

        FbDmTaskDO task = new FbDmTaskDO();
        task.setId(taskId);
        task.setCompletedCount((int) completedCount);
        task.setFailedCount((int) failedCount);

        // 判断任务是否完成
        if (completedCount + failedCount >= details.size()) {
            task.setStatus(2); // 已完成
            task.setEndTime(LocalDateTime.now());
        }

        dmTaskMapper.updateById(task);
    }

    private void validateDmTaskExists(Long id) {
        if (dmTaskMapper.selectById(id) == null) {
            throw exception(DM_TASK_NOT_EXISTS);
        }
    }

}
