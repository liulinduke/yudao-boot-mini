package cn.iocoder.yudao.module.facebook.controller.admin.message.vo;

import jakarta.validation.constraints.NotNull;
import lombok.Data;

@Data
public class FbMessageMonitorAccountSaveReqVO {
    private Long id;
    @NotNull
    private Long accountId;
    private String mode;
    private Integer checkIntervalMinutes;
    private Integer status;
}
