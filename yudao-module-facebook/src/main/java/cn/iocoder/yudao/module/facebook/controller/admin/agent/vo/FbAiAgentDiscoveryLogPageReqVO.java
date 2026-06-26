package cn.iocoder.yudao.module.facebook.controller.admin.agent.vo;

import cn.iocoder.yudao.framework.common.pojo.PageParam;
import io.swagger.v3.oas.annotations.media.Schema;
import lombok.Data;

@Schema(description = "管理后台 - Facebook AI获客Agent客户发现分页 Request VO")
@Data
public class FbAiAgentDiscoveryLogPageReqVO extends PageParam {

    @Schema(description = "Agent配置ID")
    private Long agentConfigId;

    @Schema(description = "关键词")
    private String keyword;

}
