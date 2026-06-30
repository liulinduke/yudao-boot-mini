package cn.iocoder.yudao.module.facebook.controller.admin.agent.vo;

import com.fasterxml.jackson.databind.annotation.JsonSerialize;
import com.fasterxml.jackson.databind.ser.std.ToStringSerializer;
import io.swagger.v3.oas.annotations.media.Schema;
import lombok.Data;

import java.time.LocalDateTime;

@Schema(description = "管理后台 - Facebook AI获客Agent客户发现 Response VO")
@Data
public class FbAiAgentDiscoveryLogRespVO {

    @Schema(description = "编号")
    @JsonSerialize(using = ToStringSerializer.class)
    private Long id;

    @Schema(description = "Agent配置ID")
    @JsonSerialize(using = ToStringSerializer.class)
    private Long agentConfigId;

    @Schema(description = "关键词")
    private String keyword;

    @Schema(description = "发现来源")
    private String sourceType;

    @Schema(description = "发现客户数")
    private Integer discoveredCount;

    @Schema(description = "高意向客户数")
    private Integer highIntentCount;

    @Schema(description = "主页采集数")
    private Integer pageCollectCount;

    @Schema(description = "AI分析数")
    private Integer aiAnalyzeCount;

    @Schema(description = "过滤数")
    private Integer filteredCount;

    @Schema(description = "最终线索数")
    private Integer finalLeadCount;

    @Schema(description = "关联采集任务ID")
    @JsonSerialize(using = ToStringSerializer.class)
    private Long collectTaskId;

    @Schema(description = "创建时间")
    private LocalDateTime createTime;

}
