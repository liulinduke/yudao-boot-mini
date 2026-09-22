package cn.iocoder.yudao.module.facebook.job.aisearch;

import cn.iocoder.yudao.framework.quartz.core.handler.JobHandler;
import cn.iocoder.yudao.framework.tenant.core.job.TenantJob;
import cn.iocoder.yudao.module.facebook.service.aisearch.AiSearchTaskService;
import jakarta.annotation.Resource;
import lombok.extern.slf4j.Slf4j;
import org.springframework.stereotype.Component;

@Slf4j
@Component
public class AiSearchTaskDispatchJob implements JobHandler {
    @Resource private AiSearchTaskService service;
    @Override @TenantJob
    public String execute(String param) {
        int count = service.dispatchScheduled();
        log.info("[AI企业获客定期搜索] 本轮执行任务数={}", count);
        return "执行任务数=" + count;
    }
}
