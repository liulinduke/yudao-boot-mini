package cn.iocoder.yudao.module.facebook.controller.admin.warmup.vo;

import com.fasterxml.jackson.databind.annotation.JsonSerialize;
import com.fasterxml.jackson.databind.ser.std.ToStringSerializer;
import lombok.Data;

@Data
public class FbWarmupPendingDetailRespVO {
    @JsonSerialize(using = ToStringSerializer.class)
    private Long taskId;
    @JsonSerialize(using = ToStringSerializer.class)
    private Long detailId;
    private String accountId;
    private String fbAccount;
    private String cookie;
    private String password;
    private String tfa;
    private String deviceId;
    private String warmupConfig;
}
