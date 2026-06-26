package cn.iocoder.yudao.module.facebook.controller.admin.agent.vo;

import cn.iocoder.yudao.framework.common.pojo.PageParam;
import io.swagger.v3.oas.annotations.media.Schema;
import lombok.Data;

@Schema(description = "管理后台 - Facebook AI获客Agent分页 Request VO")
@Data
public class FbAiAgentConfigPageReqVO extends PageParam {

    @Schema(description = "Agent名称")
    private String agentName;

    @Schema(description = "Agent类型")
    private String agentType;

    @Schema(description = "状态")
    private Integer status;

}
