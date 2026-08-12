package cn.iocoder.yudao.module.facebook.job.warmup;

import cn.iocoder.yudao.framework.tenant.core.util.TenantUtils;
import cn.iocoder.yudao.module.facebook.service.warmup.FbWarmupTaskService;
import jakarta.annotation.Resource;
import org.quartz.JobExecutionContext;
import org.springframework.scheduling.quartz.QuartzJobBean;
import org.quartz.DisallowConcurrentExecution;

@DisallowConcurrentExecution
public class FbWarmupQuartzJob extends QuartzJobBean {
    @Resource private FbWarmupTaskService service;
    @Override protected void executeInternal(JobExecutionContext context) {
        Long taskId = context.getMergedJobDataMap().getLong("taskId");
        Long tenantId = context.getMergedJobDataMap().getLong("tenantId");
        TenantUtils.execute(tenantId, () -> service.markReady(taskId));
    }
}
