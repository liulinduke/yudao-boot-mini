package cn.iocoder.yudao.module.facebook.job.agent;

import cn.iocoder.yudao.framework.quartz.core.handler.JobHandler;
import cn.iocoder.yudao.framework.tenant.core.job.TenantJob;
import cn.iocoder.yudao.module.facebook.controller.admin.agent.vo.FbAiAgentDispatchRespVO;
import cn.iocoder.yudao.module.facebook.service.agent.FbAiAgentService;
import jakarta.annotation.Resource;
import lombok.extern.slf4j.Slf4j;
import org.springframework.stereotype.Component;

/**
 * Facebook AI获客Agent调度 Job。
 */
@Slf4j
@Component
public class FbAiAgentDispatchJob implements JobHandler {

    @Resource
    private FbAiAgentService aiAgentService;

    @Override
    @TenantJob
    public String execute(String param) {
        FbAiAgentDispatchRespVO result = aiAgentService.dispatchScheduled();
        log.info("[execute][Facebook AI获客Agent调度结果: {}]", result.getMessage());
        return result.getMessage();
    }

}
