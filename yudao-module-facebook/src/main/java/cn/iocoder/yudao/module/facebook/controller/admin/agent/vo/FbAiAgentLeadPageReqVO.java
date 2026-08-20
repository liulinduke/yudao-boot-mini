package cn.iocoder.yudao.module.facebook.controller.admin.agent.vo;

import cn.iocoder.yudao.framework.common.pojo.SortablePageParam;
import io.swagger.v3.oas.annotations.media.Schema;
import lombok.Data;

@Schema(description = "管理后台 - Facebook AI获客Agent线索分页 Request VO")
@Data
public class FbAiAgentLeadPageReqVO extends SortablePageParam {

    @Schema(description = "Agent配置ID")
    private Long agentConfigId;

}
