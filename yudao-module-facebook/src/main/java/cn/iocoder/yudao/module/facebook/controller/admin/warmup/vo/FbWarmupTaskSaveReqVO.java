package cn.iocoder.yudao.module.facebook.controller.admin.warmup.vo;

import io.swagger.v3.oas.annotations.media.Schema;
import jakarta.validation.constraints.NotEmpty;
import jakarta.validation.constraints.NotNull;
import lombok.Data;

import java.time.LocalDateTime;
import java.util.List;

@Data
public class FbWarmupTaskSaveReqVO {
    @Schema(description = "任务名称")
    private String taskName;
    @NotNull(message = "执行时间不能为空")
    private LocalDateTime scheduleTime;
    @NotEmpty(message = "执行账号不能为空")
    private List<String> accountIds;
    @NotEmpty(message = "养号配置不能为空")
    private String warmupConfig;
}
