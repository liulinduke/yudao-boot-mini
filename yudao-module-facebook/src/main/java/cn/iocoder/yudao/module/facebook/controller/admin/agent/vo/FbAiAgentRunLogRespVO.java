package cn.iocoder.yudao.module.facebook.controller.admin.agent.vo;

import com.fasterxml.jackson.databind.annotation.JsonSerialize;
import com.fasterxml.jackson.databind.ser.std.ToStringSerializer;
import io.swagger.v3.oas.annotations.media.Schema;
import lombok.Data;

import java.time.LocalDateTime;

@Schema(description = "管理后台 - Facebook AI获客Agent运行日志 Response VO")
@Data
public class FbAiAgentRunLogRespVO {

    @Schema(description = "编号")
    @JsonSerialize(using = ToStringSerializer.class)
    private Long id;

    @Schema(description = "Agent配置ID")
    @JsonSerialize(using = ToStringSerializer.class)
    private Long agentConfigId;

    @Schema(description = "日志标题")
    private String title;

    @Schema(description = "日志内容")
    private String content;

    @Schema(description = "日志级别")
    private String logLevel;

    @Schema(description = "创建时间")
    private LocalDateTime createTime;

}
