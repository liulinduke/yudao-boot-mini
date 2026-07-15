package cn.iocoder.yudao.module.facebook.controller.admin.message.vo;

import lombok.Data;

@Data
public class FbMessageMonitorClaimRespVO {
    private Long monitorId;
    private Long accountId;
    private Long deviceId;
    private String fbAccount;
    private String cookie;
    private String mode;
    private String url;
    private Integer checkIntervalMinutes;
}
