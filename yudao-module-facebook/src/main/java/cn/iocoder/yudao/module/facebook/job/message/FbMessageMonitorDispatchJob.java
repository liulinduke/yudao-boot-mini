package cn.iocoder.yudao.module.facebook.job.message;

import cn.iocoder.yudao.framework.common.enums.UserTypeEnum;
import cn.iocoder.yudao.framework.quartz.core.handler.JobHandler;
import cn.iocoder.yudao.framework.tenant.core.job.TenantJob;
import cn.iocoder.yudao.module.infra.api.websocket.WebSocketSenderApi;
import jakarta.annotation.Resource;
import lombok.extern.slf4j.Slf4j;
import org.springframework.stereotype.Component;

/**
 * 唤醒 WPF 领取到期的 Facebook 消息接收任务。
 * 具体账号和时间由 monitor/claim 接口判断，通知本身不携带账号信息。
 */
@Slf4j
@Component
public class FbMessageMonitorDispatchJob implements JobHandler {

    @Resource
    private WebSocketSenderApi webSocketSenderApi;

    @Override
    @TenantJob
    public String execute(String param) {
        webSocketSenderApi.send(UserTypeEnum.ADMIN.getValue(), "fb-message-monitor-task-ready", "{}");
        log.debug("[execute][已发送 Facebook 消息接收任务唤醒通知]");
        return "Facebook消息接收任务唤醒通知已发送";
    }
}
