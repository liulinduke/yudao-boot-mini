package cn.iocoder.yudao.module.facebook.controller.admin.agent.vo;

import io.swagger.v3.oas.annotations.media.Schema;
import jakarta.validation.constraints.NotEmpty;
import lombok.Data;

import java.util.List;

@Schema(description = "管理后台 - Facebook AI获客Agent立即执行 Request VO")
@Data
public class FbAiAgentExecuteNowReqVO {

    @Schema(description = "Agent编号列表", requiredMode = Schema.RequiredMode.REQUIRED)
    @NotEmpty(message = "请选择要执行的Agent")
    private List<Long> ids;

}
