package cn.iocoder.yudao.module.facebook.controller.admin.message.vo;

import jakarta.validation.constraints.NotEmpty;
import lombok.Data;

import java.util.List;

@Data
public class FbMessageMonitorBatchStateReqVO {
    @NotEmpty
    private List<Long> accountIds;
    @NotEmpty
    private String state;
    private Integer checkIntervalMinutes;
    private Boolean preserveOnline;
}
