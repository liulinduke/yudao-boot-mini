package cn.iocoder.yudao.module.facebook.job.operation;

import cn.iocoder.yudao.framework.quartz.core.handler.JobHandler;
import cn.iocoder.yudao.framework.tenant.core.job.TenantJob;
import cn.iocoder.yudao.module.facebook.service.operation.FbOperationTaskService;
import jakarta.annotation.Resource;
import org.springframework.stereotype.Component;

/** Releases only due follow-task details into the account execution queue. */
@Component
public class FbFollowTaskDispatchJob implements JobHandler {

    @Resource
    private FbOperationTaskService operationTaskService;

    @Override
    @TenantJob
    public String execute(String param) {
        operationTaskService.enqueueDueFollowDetails();
        return "已检查到期刷粉明细";
    }
}
