package cn.iocoder.yudao.module.facebook.service.message;

import cn.iocoder.yudao.framework.quartz.core.enums.JobDataKeyEnum;
import cn.iocoder.yudao.framework.quartz.core.handler.JobHandlerInvoker;
import cn.iocoder.yudao.module.facebook.dal.dataobject.message.FbMessageMonitorAccountDO;
import cn.iocoder.yudao.module.facebook.dal.mysql.message.FbMessageMonitorAccountMapper;
import cn.iocoder.yudao.module.infra.dal.dataobject.job.JobDO;
import cn.iocoder.yudao.module.infra.dal.mysql.job.JobMapper;
import jakarta.annotation.PostConstruct;
import jakarta.annotation.Resource;
import lombok.extern.slf4j.Slf4j;
import org.quartz.*;
import org.quartz.impl.matchers.GroupMatcher;
import org.springframework.beans.factory.ObjectProvider;
import org.springframework.stereotype.Service;

import java.time.LocalTime;
import java.util.*;
import java.util.stream.Collectors;

/**
 * Facebook 消息接收的全局 Quartz 调度。
 * 一个时间对应一个 Trigger，账号不单独创建 Trigger；到点后由 claim 接口批量领取账号。
 */
@Slf4j
@Service
public class FbMessageMonitorQuartzScheduleService {

    private static final String HANDLER_NAME = "fbMessageMonitorDispatchJob";
    private static final String JOB_KEY = HANDLER_NAME;
    private static final String TRIGGER_GROUP = "facebook-message-monitor";

    @Resource
    private FbMessageMonitorAccountMapper monitorMapper;
    @Resource
    private JobMapper jobMapper;
    @Resource
    private ObjectProvider<Scheduler> schedulerProvider;

    @PostConstruct
    public void initialize() {
        refresh();
    }

    public synchronized void refresh() {
        Scheduler scheduler = schedulerProvider.getIfAvailable();
        if (scheduler == null) return;
        try {
            JobDO job = jobMapper.selectByHandlerName(HANDLER_NAME);
            if (job == null) {
                log.warn("未找到 {} 定时任务，请先在系统定时任务中创建消息接收任务", HANDLER_NAME);
                return;
            }

            JobKey jobKey = new JobKey(JOB_KEY);
            JobDetail detail = JobBuilder.newJob(JobHandlerInvoker.class)
                    .withIdentity(jobKey)
                    .usingJobData(JobDataKeyEnum.JOB_ID.name(), job.getId())
                    .usingJobData(JobDataKeyEnum.JOB_HANDLER_NAME.name(), HANDLER_NAME)
                    .usingJobData(JobDataKeyEnum.JOB_HANDLER_PARAM.name(), "")
                    .usingJobData(JobDataKeyEnum.JOB_RETRY_COUNT.name(), 0)
                    .usingJobData(JobDataKeyEnum.JOB_RETRY_INTERVAL.name(), 0)
                    .build();
            scheduler.addJob(detail, true, true);

            // 移除系统定时任务页面创建的旧默认 Trigger，避免继续每分钟执行。
            TriggerKey defaultTrigger = new TriggerKey(HANDLER_NAME);
            if (scheduler.checkExists(defaultTrigger)) scheduler.unscheduleJob(defaultTrigger);

            for (TriggerKey key : scheduler.getTriggerKeys(
                    GroupMatcher.triggerGroupEquals(TRIGGER_GROUP))) {
                scheduler.unscheduleJob(key);
            }

            Set<String> times = monitorMapper.selectList(null).stream()
                    .filter(row -> Objects.equals(row.getReceiveEnabled(), 1))
                    .filter(row -> Objects.equals(row.getStatus(), 1))
                    .filter(row -> !"disabled".equalsIgnoreCase(row.getMode()))
                    .flatMap(row -> parseTimes(row.getScheduleTimes()).stream())
                    .collect(Collectors.toCollection(TreeSet::new));

            if (times.isEmpty()) {
                scheduler.pauseJob(jobKey);
                return;
            }
            scheduler.resumeJob(jobKey);
            for (String time : times) {
                LocalTime parsed = LocalTime.parse(time);
                Trigger trigger = TriggerBuilder.newTrigger()
                        .withIdentity("time-" + time.replace(':', '-'), TRIGGER_GROUP)
                        .forJob(jobKey)
                        .withSchedule(CronScheduleBuilder.cronSchedule(
                                String.format("0 %d %d * * ?", parsed.getMinute(), parsed.getHour())))
                        .build();
                scheduler.scheduleJob(trigger);
            }
            log.info("Facebook消息接收Quartz调度已更新，时间={}", times);
        } catch (Exception ex) {
            log.warn("更新Facebook消息接收Quartz调度失败: {}", ex.getMessage());
        }
    }

    private static List<String> parseTimes(String value) {
        if (value == null || value.isBlank()) return List.of();
        return Arrays.stream(value.split(","))
                .map(String::trim)
                .filter(item -> item.matches("(?:[01]\\d|2[0-3]):[0-5]\\d"))
                .distinct()
                .toList();
    }
}
