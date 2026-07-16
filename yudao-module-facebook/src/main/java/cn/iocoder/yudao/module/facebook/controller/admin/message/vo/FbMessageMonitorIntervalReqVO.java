package cn.iocoder.yudao.module.facebook.controller.admin.message.vo;

import jakarta.validation.constraints.NotEmpty;
import jakarta.validation.constraints.NotNull;
import lombok.Data;

import java.util.List;

@Data
public class FbMessageMonitorIntervalReqVO {
    @NotEmpty
    private List<Long> accountIds;
    @NotNull
    private Integer checkIntervalMinutes;
}
