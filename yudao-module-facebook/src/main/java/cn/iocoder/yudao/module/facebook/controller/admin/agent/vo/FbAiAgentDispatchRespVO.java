package cn.iocoder.yudao.module.facebook.controller.admin.agent.vo;

import io.swagger.v3.oas.annotations.media.Schema;
import lombok.AllArgsConstructor;
import lombok.Data;
import lombok.NoArgsConstructor;

@Schema(description = "管理后台 - Facebook AI获客Agent调度结果 Response VO")
@Data
@NoArgsConstructor
@AllArgsConstructor
public class FbAiAgentDispatchRespVO {

    @Schema(description = "是否已调度")
    private Boolean dispatched;

    @Schema(description = "说明")
    private String message;

}
