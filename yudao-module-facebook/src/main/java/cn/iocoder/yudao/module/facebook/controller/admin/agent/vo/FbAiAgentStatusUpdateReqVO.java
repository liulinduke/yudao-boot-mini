package cn.iocoder.yudao.module.facebook.controller.admin.agent.vo;

import io.swagger.v3.oas.annotations.media.Schema;
import jakarta.validation.constraints.Max;
import jakarta.validation.constraints.Min;
import jakarta.validation.constraints.NotNull;
import lombok.Data;

@Schema(description = "管理后台 - Facebook AI获客Agent状态更新 Request VO")
@Data
public class FbAiAgentStatusUpdateReqVO {

    @Schema(description = "Agent编号", requiredMode = Schema.RequiredMode.REQUIRED)
    @NotNull(message = "Agent编号不能为空")
    private Long id;

    @Schema(description = "状态：0草稿 1运行中 2暂停 3停止", requiredMode = Schema.RequiredMode.REQUIRED)
    @NotNull(message = "状态不能为空")
    @Min(value = 0, message = "状态不正确")
    @Max(value = 3, message = "状态不正确")
    private Integer status;

}
