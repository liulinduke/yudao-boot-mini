package cn.iocoder.yudao.module.facebook.controller.admin.message.vo;

import lombok.Data;
import com.fasterxml.jackson.databind.annotation.JsonSerialize;
import com.fasterxml.jackson.databind.ser.std.ToStringSerializer;

@Data
public class FbMessageMonitorClaimRespVO {
    @JsonSerialize(using = ToStringSerializer.class)
    private Long monitorId;
    @JsonSerialize(using = ToStringSerializer.class)
    private Long accountId;
    @JsonSerialize(using = ToStringSerializer.class)
    private Long deviceId;
    private String fbAccount;
    private String cookie;
    private String mode;
    private String url;
    private Integer checkIntervalMinutes;
}
