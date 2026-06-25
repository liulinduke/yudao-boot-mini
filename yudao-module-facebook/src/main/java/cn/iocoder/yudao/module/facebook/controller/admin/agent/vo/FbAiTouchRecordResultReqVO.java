package cn.iocoder.yudao.module.facebook.controller.admin.agent.vo;

import io.swagger.v3.oas.annotations.media.Schema;
import jakarta.validation.constraints.NotNull;
import lombok.Data;

@Schema(description = "管理后台 - Facebook AI触达结果更新 Request VO")
@Data
public class FbAiTouchRecordResultReqVO {

    @Schema(description = "触达记录ID", requiredMode = Schema.RequiredMode.REQUIRED)
    @NotNull(message = "触达记录ID不能为空")
    private Long id;

    @Schema(description = "状态：2成功 3失败 4跳过", requiredMode = Schema.RequiredMode.REQUIRED)
    @NotNull(message = "状态不能为空")
    private Integer status;

    @Schema(description = "失败原因")
    private String failReason;

}
