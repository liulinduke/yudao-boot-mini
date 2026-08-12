package cn.iocoder.yudao.module.facebook.service.warmup;

import cn.iocoder.yudao.framework.common.pojo.PageResult;
import cn.iocoder.yudao.framework.common.util.object.BeanUtils;
import cn.iocoder.yudao.framework.common.enums.UserTypeEnum;
import cn.iocoder.yudao.framework.tenant.core.context.TenantContextHolder;
import cn.iocoder.yudao.module.facebook.controller.admin.warmup.vo.*;
import cn.iocoder.yudao.module.facebook.dal.dataobject.account.FbAccountDO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.warmup.*;
import cn.iocoder.yudao.module.facebook.dal.mysql.account.FbAccountMapper;
import cn.iocoder.yudao.module.facebook.dal.mysql.warmup.*;
import cn.iocoder.yudao.module.infra.api.websocket.WebSocketSenderApi;
import com.baomidou.mybatisplus.core.conditions.query.LambdaQueryWrapper;
import com.baomidou.mybatisplus.core.conditions.update.LambdaUpdateWrapper;
import jakarta.annotation.Resource;
import lombok.extern.slf4j.Slf4j;
import org.quartz.*;
import org.springframework.beans.factory.ObjectProvider;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;
import org.springframework.validation.annotation.Validated;

import java.time.LocalDateTime;
import java.time.ZoneId;
import java.util.*;
import java.util.stream.Collectors;

@Slf4j
@Service
@Validated
public class FbWarmupTaskServiceImpl implements FbWarmupTaskService {
    public static final int WAITING = 0, READY = 1, RUNNING = 2, SUCCESS = 3, FAILED = 4, CANCELLED = 5;
    private static final String JOB_HANDLER = "fbWarmupDispatchJob";
    private static final String JOB_PREFIX = "fb-warmup-";

    @Resource private FbWarmupTaskMapper taskMapper;
    @Resource private FbWarmupTaskDetailMapper detailMapper;
    @Resource private FbAccountMapper accountMapper;
    @Resource private WebSocketSenderApi webSocketSenderApi;
    @Resource private ObjectProvider<Scheduler> schedulerProvider;

    @Override
    @Transactional(rollbackFor = Exception.class)
    public Long create(FbWarmupTaskSaveReqVO req, boolean immediate) {
        if (!immediate && !req.getScheduleTime().isAfter(LocalDateTime.now())) {
            throw new IllegalArgumentException("执行时间必须晚于当前时间");
        }
        List<Long> accountIds = req.getAccountIds().stream().map(Long::valueOf).distinct().collect(Collectors.toList());
        List<FbAccountDO> accounts = accountMapper.selectBatchIds(accountIds);
        Set<Long> found = accounts.stream().map(FbAccountDO::getId).collect(Collectors.toSet());
        if (found.size() != accountIds.size()) throw new IllegalArgumentException("存在无效的Facebook账号");
        Long tenantId = TenantContextHolder.getRequiredTenantId();
        FbWarmupTaskDO task = new FbWarmupTaskDO();
        task.setTaskName(req.getTaskName() == null || req.getTaskName().isBlank() ? "养号任务" : req.getTaskName().trim());
        task.setScheduleTime(immediate ? LocalDateTime.now() : req.getScheduleTime());
        task.setWarmupConfig(req.getWarmupConfig());
        task.setStatus(immediate ? READY : WAITING);
        task.setAccountCount(accountIds.size());
        taskMapper.insert(task);
        List<FbWarmupTaskDetailDO> details = accountIds.stream().map(accountId -> FbWarmupTaskDetailDO.builder()
                .taskId(task.getId()).accountId(String.valueOf(accountId)).status(0).build()).collect(Collectors.toList());
        detailMapper.insertBatch(details);
        if (immediate) {
            task.setReadyTime(LocalDateTime.now());
            taskMapper.updateById(task);
            notifyReady();
        } else {
            schedule(task.getId(), tenantId, task.getScheduleTime());
        }
        return task.getId();
    }

    @Override
    public PageResult<FbWarmupTaskRespVO> page(FbWarmupTaskPageReqVO req) {
        PageResult<FbWarmupTaskDO> result = taskMapper.selectPage(req);
        PageResult<FbWarmupTaskRespVO> page = BeanUtils.toBean(result, FbWarmupTaskRespVO.class);
        for (FbWarmupTaskRespVO item : page.getList()) {
            item.setAccountIds(detailMapper.selectListByTaskId(item.getId()).stream().map(FbWarmupTaskDetailDO::getAccountId).toList());
        }
        return page;
    }

    @Override
    @Transactional(rollbackFor = Exception.class)
    public void delete(Long id) {
        FbWarmupTaskDO task = taskMapper.selectById(id);
        if (task == null) return;
        if (!Objects.equals(task.getStatus(), WAITING) && !Objects.equals(task.getStatus(), READY)) {
            throw new IllegalArgumentException("只能删除等待执行的养号任务");
        }
        task.setStatus(CANCELLED);
        task.setEndTime(LocalDateTime.now());
        taskMapper.updateById(task);
        unschedule(id);
    }

    @Override
    @Transactional(rollbackFor = Exception.class)
    public List<FbWarmupPendingDetailRespVO> claimPending(Integer limit) {
        expireUnclaimedTasks();
        int max = Math.max(1, Math.min(limit == null ? 10 : limit, 20));
        List<FbWarmupTaskDetailDO> details = detailMapper.selectList(new LambdaQueryWrapper<FbWarmupTaskDetailDO>()
                .eq(FbWarmupTaskDetailDO::getStatus, 0).orderByAsc(FbWarmupTaskDetailDO::getId).last("LIMIT " + max));
        List<FbWarmupPendingDetailRespVO> result = new ArrayList<>();
        LocalDateTime now = LocalDateTime.now();
        for (FbWarmupTaskDetailDO detail : details) {
            FbWarmupTaskDO task = taskMapper.selectById(detail.getTaskId());
            if (task == null || (!Objects.equals(task.getStatus(), READY) && !Objects.equals(task.getStatus(), RUNNING))) continue;
            int updated = detailMapper.update(null, new LambdaUpdateWrapper<FbWarmupTaskDetailDO>()
                    .eq(FbWarmupTaskDetailDO::getId, detail.getId()).eq(FbWarmupTaskDetailDO::getStatus, 0)
                    .set(FbWarmupTaskDetailDO::getStatus, 1).set(FbWarmupTaskDetailDO::getStartTime, now));
            if (updated != 1) continue;
            FbAccountDO account = accountMapper.selectById(Long.valueOf(detail.getAccountId()));
            if (account == null) continue;
            FbWarmupPendingDetailRespVO item = new FbWarmupPendingDetailRespVO();
            item.setTaskId(task.getId()); item.setDetailId(detail.getId()); item.setAccountId(detail.getAccountId());
            item.setFbAccount(account.getFbAccount()); item.setCookie(account.getCookie()); item.setPassword(account.getPassword());
            item.setTfa(account.getTfa()); item.setDeviceId(account.getDeviceId() == null ? null : String.valueOf(account.getDeviceId()));
            item.setWarmupConfig(task.getWarmupConfig()); result.add(item);
            if (Objects.equals(task.getStatus(), READY)) { task.setStatus(RUNNING); task.setStartTime(now); taskMapper.updateById(task); }
        }
        return result;
    }

    private void expireUnclaimedTasks() {
        LocalDateTime deadline = LocalDateTime.now().minusSeconds(60);
        List<FbWarmupTaskDO> expired = taskMapper.selectList(new LambdaQueryWrapper<FbWarmupTaskDO>()
                .eq(FbWarmupTaskDO::getStatus, READY)
                .lt(FbWarmupTaskDO::getReadyTime, deadline));
        for (FbWarmupTaskDO task : expired) {
            task.setStatus(FAILED);
            task.setEndTime(LocalDateTime.now());
            task.setErrorMessage("到点后60秒内没有可用的WPF客户端");
            taskMapper.updateById(task);
            detailMapper.update(null, new LambdaUpdateWrapper<FbWarmupTaskDetailDO>()
                    .eq(FbWarmupTaskDetailDO::getTaskId, task.getId())
                    .eq(FbWarmupTaskDetailDO::getStatus, 0)
                    .set(FbWarmupTaskDetailDO::getStatus, 3)
                    .set(FbWarmupTaskDetailDO::getEndTime, LocalDateTime.now())
                    .set(FbWarmupTaskDetailDO::getErrorMessage, "到点后60秒内没有可用的WPF客户端"));
        }
    }

    @Override
    @Transactional(rollbackFor = Exception.class)
    public void reportDetail(Long detailId, Boolean success, String errorMessage) {
        FbWarmupTaskDetailDO detail = detailMapper.selectById(detailId);
        if (detail == null || Objects.equals(detail.getStatus(), 2) || Objects.equals(detail.getStatus(), 3)) return;
        detail.setStatus(Boolean.TRUE.equals(success) ? 2 : 3);
        detail.setEndTime(LocalDateTime.now()); detail.setErrorMessage(errorMessage);
        detailMapper.updateById(detail);
        List<FbWarmupTaskDetailDO> all = detailMapper.selectListByTaskId(detail.getTaskId());
        if (all.stream().anyMatch(d -> d.getStatus() == null || d.getStatus() == 0 || d.getStatus() == 1)) return;
        FbWarmupTaskDO task = taskMapper.selectById(detail.getTaskId());
        if (task == null) return;
        boolean ok = all.stream().allMatch(d -> Objects.equals(d.getStatus(), 2));
        task.setStatus(ok ? SUCCESS : FAILED); task.setEndTime(LocalDateTime.now());
        if (!ok) task.setErrorMessage(all.stream().filter(d -> d.getErrorMessage() != null).map(FbWarmupTaskDetailDO::getErrorMessage).findFirst().orElse("部分账号养号失败"));
        taskMapper.updateById(task);
    }

    @Override
    @Transactional(rollbackFor = Exception.class)
    public void markReady(Long taskId) {
        FbWarmupTaskDO task = taskMapper.selectById(taskId);
        if (task == null || !Objects.equals(task.getStatus(), WAITING)) return;
        task.setStatus(READY); task.setReadyTime(LocalDateTime.now()); taskMapper.updateById(task); notifyReady();
    }

    private void notifyReady() { webSocketSenderApi.send(UserTypeEnum.ADMIN.getValue(), "fb-warmup-task-ready", "{}"); }

    private void schedule(Long taskId, Long tenantId, LocalDateTime time) {
        Scheduler scheduler = schedulerProvider.getIfAvailable();
        if (scheduler == null) throw new IllegalStateException("Quartz 未启用，无法创建定时养号任务");
        try {
            JobDetail job = JobBuilder.newJob(cn.iocoder.yudao.module.facebook.job.warmup.FbWarmupQuartzJob.class)
                    .withIdentity(JOB_PREFIX + taskId).usingJobData("taskId", taskId).usingJobData("tenantId", tenantId).build();
            Trigger trigger = TriggerBuilder.newTrigger().withIdentity(JOB_PREFIX + taskId)
                    .forJob(job).startAt(Date.from(time.atZone(ZoneId.systemDefault()).toInstant())).build();
            scheduler.scheduleJob(job, trigger);
        } catch (SchedulerException e) { throw new IllegalStateException("创建养号定时任务失败", e); }
    }

    private void unschedule(Long taskId) {
        Scheduler scheduler = schedulerProvider.getIfAvailable(); if (scheduler == null) return;
        try { scheduler.deleteJob(new JobKey(JOB_PREFIX + taskId)); } catch (SchedulerException e) { log.warn("删除养号Quartz任务失败, id={}", taskId, e); }
    }
}
