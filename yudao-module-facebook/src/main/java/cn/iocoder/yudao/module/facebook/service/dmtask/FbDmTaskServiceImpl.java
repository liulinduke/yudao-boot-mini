package cn.iocoder.yudao.module.facebook.service.dmtask;

import cn.hutool.core.collection.CollUtil;
import cn.hutool.core.util.StrUtil;
import cn.hutool.json.JSONUtil;
import cn.iocoder.yudao.framework.common.pojo.PageResult;
import cn.iocoder.yudao.framework.common.util.object.BeanUtils;
import cn.iocoder.yudao.module.facebook.controller.admin.dmtask.vo.FbDmTaskSaveReqVO;
import cn.iocoder.yudao.module.facebook.controller.admin.dmtask.vo.FbDmTaskRespVO;
import cn.iocoder.yudao.module.facebook.controller.admin.dmtask.vo.FbDmTaskPageReqVO;
import cn.iocoder.yudao.module.facebook.controller.admin.dmtask.vo.FbDmTaskDetailRespVO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.dmtask.FbDmTaskDO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.dmtask.FbDmTaskDetailDO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.agent.FbAiTouchRecordDO;
import cn.iocoder.yudao.module.facebook.dal.mysql.dmtask.FbDmTaskDetailMapper;
import cn.iocoder.yudao.module.facebook.dal.dataobject.message.FbMessageDO;
import cn.iocoder.yudao.module.facebook.dal.mysql.message.FbMessageMapper;
import cn.iocoder.yudao.module.facebook.dal.mysql.dmtask.FbDmTaskMapper;
import cn.iocoder.yudao.module.facebook.dal.mysql.agent.FbAiTouchRecordMapper;
import cn.iocoder.yudao.module.facebook.dal.dataobject.account.FbAccountDO;
import cn.iocoder.yudao.module.facebook.dal.mysql.account.FbAccountMapper;
import cn.iocoder.yudao.module.facebook.enums.OperationTypeEnum;
import cn.iocoder.yudao.module.facebook.service.agent.FbAiAgentCollectQueueService;
import cn.iocoder.yudao.module.facebook.service.dailylimit.FacebookDailyLimitService;
import cn.iocoder.yudao.module.facebook.service.account.FbAccountTaskAllocationService;
import com.baomidou.mybatisplus.core.conditions.query.LambdaQueryWrapper;
import jakarta.annotation.Resource;
import lombok.extern.slf4j.Slf4j;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;
import org.springframework.validation.annotation.Validated;

import java.time.LocalDateTime;
import java.util.*;
import java.util.function.Function;
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
    private FbMessageMapper fbMessageMapper;
    @Resource
    private FbAiTouchRecordMapper aiTouchRecordMapper;

    @Resource
    private DmTaskAllocator taskAllocator;

    @Resource
    private FacebookDailyLimitService dailyLimitService;

    @Resource
    private FbAccountMapper accountMapper;
    @Resource
    private FbAiAgentCollectQueueService accountTaskQueueService;

    @Resource
    private FbAccountTaskAllocationService accountAllocationService;

    @Override
    @Transactional(rollbackFor = Exception.class)
    public Long createDmTask(FbDmTaskSaveReqVO saveReqVO) {
        // 1. 创建主任务
        FbDmTaskDO task = BeanUtils.toBean(saveReqVO, FbDmTaskDO.class);
        task.setTargetUserIds(JSONUtil.toJsonStr(saveReqVO.getTargetUserIds()));
        task.setScripts(JSONUtil.toJsonStr(saveReqVO.getScripts()));
        List<String> requestedAccountIds = saveReqVO.getAccountIds() == null ? Collections.emptyList() : saveReqVO.getAccountIds();
        List<Long> selectedIds = accountAllocationService.selectAccounts(
                saveReqVO.getAccountSelectionMode(), requestedAccountIds.stream().map(Long::valueOf).collect(Collectors.toList()),
                saveReqVO.getTargetUserIds().size(), "operation", Collections.singletonList("dm"));
        List<String> selectedAccountIds = selectedIds.stream().map(String::valueOf).collect(Collectors.toList());
        if (selectedAccountIds.isEmpty()) {
            throw new IllegalArgumentException("没有可用的Facebook账号，请检查账号状态或每日私信额度");
        }
        task.setAccountIds(JSONUtil.toJsonStr(selectedAccountIds));
        task.setAccountSelectionMode(saveReqVO.getAccountSelectionMode());
        task.setStatus(0); // 待执行
        task.setTotalCount(saveReqVO.getTargetUserIds().size());
        task.setCompletedCount(0);
        task.setFailedCount(0);
        dmTaskMapper.insert(task);

        // 2. 使用分配器分配任务
        Map<String, List<String>> allocation = taskAllocator.allocate(
                selectedAccountIds,
                saveReqVO.getTargetUserIds()
        );

        // 3. 创建任务明细（账号×用户打散，话术轮询打散，可选随机表情）
        List<FbDmTaskDetailDO> details = new ArrayList<>();
        for (Map.Entry<String, List<String>> entry : allocation.entrySet()) {
            String accountId = entry.getKey();
            for (String userId : entry.getValue()) {
                FbDmTaskDetailDO detail = new FbDmTaskDetailDO();
                detail.setTaskId(task.getId());
                detail.setAccountId(accountId);
                detail.setTargetUserId(userId);
                detail.setStatus(0);
                details.add(detail);
            }
        }

        if (CollUtil.isNotEmpty(details)) {
            List<String> scatteredScripts = DmScriptHelper.scatterScripts(saveReqVO.getScripts(), details.size());
            boolean appendEmoji = Boolean.TRUE.equals(saveReqVO.getAppendRandomEmoji());
            for (int i = 0; i < details.size(); i++) {
                String script = scatteredScripts.get(i);
                if (appendEmoji) {
                    script = DmScriptHelper.appendRandomEmoji(script);
                }
                details.get(i).setScriptContent(script);
            }
            dmTaskDetailMapper.insertBatch(details);
            pushDmDetailsToAccountQueue(details);
            task.setTotalCount(details.size());
            dmTaskMapper.updateById(task);
        }

        log.info("创建群发私信任务成功，任务ID: {}, 总任务数: {}, 分配账号数: {}, 话术数: {}",
                task.getId(), details.size(), allocation.size(), saveReqVO.getScripts().size());

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
        
        // 查询任务明细列表
        List<FbDmTaskDetailDO> details = dmTaskDetailMapper.selectListByTaskId(id);
        if (CollUtil.isNotEmpty(details)) {
            Set<String> accountIds = details.stream()
                    .map(FbDmTaskDetailDO::getAccountId)
                    .collect(Collectors.toSet());

            List<Long> accountIdLongs = accountIds.stream()
                    .filter(StrUtil::isNotBlank)
                    .map(ida -> {
                        try {
                            return Long.valueOf(ida.trim());
                        } catch (NumberFormatException ex) {
                            return null;
                        }
                    })
                    .filter(Objects::nonNull)
                    .collect(Collectors.toList());
            Map<String, String> cookieMap = new HashMap<>();
            if (!accountIdLongs.isEmpty()) {
                List<FbAccountDO> accounts = accountMapper.selectList(
                        new LambdaQueryWrapper<FbAccountDO>().in(FbAccountDO::getId, accountIdLongs));
                cookieMap = accounts.stream()
                        .filter(acc -> acc.getCookie() != null)
                        .collect(Collectors.toMap(
                                acc -> String.valueOf(acc.getId()),
                                FbAccountDO::getCookie,
                                (v1, v2) -> v1));
            }
            
            // 转换为RespVO并填充cookie
            final Map<String, String> finalCookieMap = cookieMap;
            Map<String, FbAccountDO> accountMap = accountIdLongs.isEmpty()
                    ? Collections.emptyMap()
                    : accountMapper.selectList(new LambdaQueryWrapper<FbAccountDO>()
                            .in(FbAccountDO::getId, accountIdLongs))
                    .stream()
                    .collect(Collectors.toMap(acc -> String.valueOf(acc.getId()), Function.identity(), (v1, v2) -> v1));
            List<FbDmTaskDetailRespVO> detailRespVOs = details.stream()
                    .map(detail -> {
                        FbDmTaskDetailRespVO detailVO = BeanUtils.toBean(detail, FbDmTaskDetailRespVO.class);
                        // 从账号表中获取cookie
                        detailVO.setCookie(finalCookieMap.getOrDefault(detail.getAccountId(), ""));
                        FbAccountDO account = accountMap.get(detail.getAccountId());
                        if (account != null) {
                            detailVO.setPassword(account.getPassword());
                            detailVO.setTfa(account.getTfa());
                        }
                        return detailVO;
                    })
                    .collect(Collectors.toList());
            
            respVO.setDetails(detailRespVOs);
        } else {
            respVO.setDetails(new ArrayList<>());
        }
        
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
        
        // 获取任务明细列表
        List<FbDmTaskDetailDO> details = dmTaskDetailMapper.selectListByTaskId(taskId);
        if (CollUtil.isEmpty(details)) {
            log.warn("任务 {} 没有明细数据", taskId);
            return;
        }
        
        log.info("任务 {} 共有 {} 条私信需要发送", taskId, details.size());
        
        // TODO: 这里触发WPF执行任务的逻辑
        // 目前WPF已通过JsBridgeService.StartDmTask实现，前端可以直接调用
        // 如果需要后端主动推送，可以集成WebSocket或MQTT
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
            log.warn("私信明细不存在，无法更新状态，detailId={}", detailId);
            return;
        }

        detail.setStatus(status);
        detail.setErrorMsg(errorMsg);
        if (status == 1) { // 只统计实际发送成功
            detail.setSendTime(LocalDateTime.now());
            // 消耗一次次数
            dailyLimitService.useOnce(detail.getAccountId(), OperationTypeEnum.DM);
        }
        dmTaskDetailMapper.updateById(detail);
        FbMessageDO message = fbMessageMapper.selectBySendDetailId(detailId);
        if (message != null) {
            FbMessageDO messageUpdate = new FbMessageDO();
            messageUpdate.setId(message.getId());
            messageUpdate.setSendStatus(status == 1 ? 2 : 3);
            messageUpdate.setSendTime(LocalDateTime.now());
            messageUpdate.setErrorMessage(errorMsg);
            fbMessageMapper.updateById(messageUpdate);
        }
        releaseDmAccountRunning(detail);
        updateAiTouchRecordStatus(detail, status, errorMsg);

        // 更新主任务统计
        updateTaskStatistics(detail.getTaskId());
    }

    private void pushDmDetailsToAccountQueue(List<FbDmTaskDetailDO> details) {
        if (CollUtil.isEmpty(details)) {
            return;
        }
        Map<String, String> accountMap = resolveFbAccountMap(details.stream()
                .map(FbDmTaskDetailDO::getAccountId)
                .collect(Collectors.toList()));
        for (FbDmTaskDetailDO detail : details) {
            String fbAccount = accountMap.get(detail.getAccountId());
            if (StrUtil.isNotBlank(fbAccount)) {
                accountTaskQueueService.push("dm", detail.getId(), fbAccount);
            }
        }
    }

    private void releaseDmAccountRunning(FbDmTaskDetailDO detail) {
        Map<String, String> accountMap = resolveFbAccountMap(Collections.singletonList(detail.getAccountId()));
        String fbAccount = accountMap.get(detail.getAccountId());
        if (StrUtil.isNotBlank(fbAccount)) {
            accountTaskQueueService.releaseRunning(fbAccount);
        }
    }

    private Map<String, String> resolveFbAccountMap(List<String> accountIds) {
        List<Long> ids = accountIds.stream()
                .filter(StrUtil::isNotBlank)
                .map(this::parseLongOrNull)
                .filter(Objects::nonNull)
                .distinct()
                .collect(Collectors.toList());
        if (CollUtil.isEmpty(ids)) {
            return Collections.emptyMap();
        }
        return accountMapper.selectBatchIds(ids).stream()
                .collect(Collectors.toMap(account -> String.valueOf(account.getId()), FbAccountDO::getFbAccount, (a, b) -> a));
    }

    private Long parseLongOrNull(String value) {
        try {
            return value == null ? null : Long.valueOf(value.trim());
        } catch (Exception ex) {
            return null;
        }
    }

    private void updateAiTouchRecordStatus(FbDmTaskDetailDO detail, Integer dmStatus, String errorMsg) {
        if (detail == null || detail.getTaskId() == null || detail.getTargetUserId() == null) {
            return;
        }
        FbAiTouchRecordDO record = aiTouchRecordMapper.selectOne(new LambdaQueryWrapper<FbAiTouchRecordDO>()
                .eq(FbAiTouchRecordDO::getTouchType, "dm")
                .eq(FbAiTouchRecordDO::getOperationTaskId, detail.getTaskId())
                .eq(FbAiTouchRecordDO::getTargetUserId, detail.getTargetUserId())
                .last("LIMIT 1"));
        if (record == null) {
            return;
        }
        if (Objects.equals(record.getStatus(), 2) || Objects.equals(record.getStatus(), 3)) {
            return;
        }
        FbAiTouchRecordDO updateObj = new FbAiTouchRecordDO();
        updateObj.setId(record.getId());
        if (Objects.equals(dmStatus, 1)) {
            updateObj.setStatus(2);
            updateObj.setSentTime(LocalDateTime.now());
            updateObj.setFailReason(null);
        } else if (Objects.equals(dmStatus, 2)) {
            updateObj.setStatus(3);
            updateObj.setFailReason(errorMsg);
        } else {
            return;
        }
        aiTouchRecordMapper.updateById(updateObj);
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

        FbDmTaskDO existing = dmTaskMapper.selectById(taskId);
        if (existing == null) {
            return;
        }

        FbDmTaskDO task = new FbDmTaskDO();
        task.setId(taskId);
        task.setCompletedCount((int) completedCount);
        task.setFailedCount((int) failedCount);

        long finishedCount = completedCount + failedCount;
        if (existing.getStartTime() == null && finishedCount > 0) {
            task.setStartTime(LocalDateTime.now());
        }
        if ((existing.getStatus() == null || existing.getStatus() == 0) && finishedCount > 0) {
            task.setStatus(1);
        }

        // 判断任务是否完成
        if (finishedCount >= details.size()) {
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
