package cn.iocoder.yudao.module.facebook.controller.admin.message.vo;

import jakarta.validation.constraints.NotNull;
import lombok.Data;

@Data
public class FbMessageMonitorBadgeReportReqVO {
    @NotNull
    private Long accountId;
    private Integer messengerUnreadCount;
    private Integer notificationUnreadCount;
    private Boolean loggedIn;
}
