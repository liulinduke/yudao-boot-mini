package cn.iocoder.yudao.module.facebook.controller.admin.message.vo;

import jakarta.validation.constraints.NotEmpty;
import lombok.Data;

import java.util.List;

@Data
public class FbMessageMonitorPoolReqVO {
    @NotEmpty
    private List<Long> accountIds;
    private Integer checkIntervalMinutes;
}
