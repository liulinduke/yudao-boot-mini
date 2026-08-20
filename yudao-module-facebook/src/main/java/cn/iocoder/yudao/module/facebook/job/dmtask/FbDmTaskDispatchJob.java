package cn.iocoder.yudao.module.facebook.job.dmtask;

import cn.iocoder.yudao.framework.quartz.core.handler.JobHandler;
import cn.iocoder.yudao.framework.tenant.core.job.TenantJob;
import cn.iocoder.yudao.module.facebook.service.dmtask.FbDmTaskService;
import jakarta.annotation.Resource;
import org.springframework.stereotype.Component;

/** Periodically releases due DM details into the per-account execution queue. */
@Component
public class FbDmTaskDispatchJob implements JobHandler {

    @Resource
    private FbDmTaskService dmTaskService;

    @Override
    @TenantJob
    public String execute(String param) {
        dmTaskService.enqueueDueDetails();
        return "已检查到期私信明细";
    }
}
