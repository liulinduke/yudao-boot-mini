package cn.iocoder.yudao.module.facebook.controller.admin.agent.vo;

import io.swagger.v3.oas.annotations.media.Schema;
import jakarta.validation.constraints.Max;
import jakarta.validation.constraints.Min;
import jakarta.validation.constraints.NotBlank;
import lombok.Data;

@Schema(description = "管理后台 - Facebook AI获客Agent配置保存 Request VO")
@Data
public class FbAiAgentConfigSaveReqVO {

    @Schema(description = "编号")
    private Long id;

    @Schema(description = "Agent名称", requiredMode = Schema.RequiredMode.REQUIRED)
    @NotBlank(message = "Agent名称不能为空")
    private String agentName;

    @Schema(description = "知识库ID列表，逗号分隔")
    private String knowledgeIds;

    @Schema(description = "关键词种子，JSON数组")
    private String seedKeywords;

    @Schema(description = "目标国家，JSON数组")
    private String targetCountries;

    @Schema(description = "目标语言，JSON数组")
    private String targetLanguages;

    @Schema(description = "账号ID列表，逗号分隔")
    private String accountIds;

    @Schema(description = "监控群组ID列表，逗号分隔")
    private String monitorGroupIds;

    @Schema(description = "线索评分工作流ID")
    private Long leadScoreWorkflowId;

    @Schema(description = "评论生成工作流ID")
    private Long commentWorkflowId;

    @Schema(description = "私信生成工作流ID")
    private Long dmWorkflowId;

    @Schema(description = "是否自动评论")
    private Boolean autoCommentEnabled;

    @Schema(description = "是否自动私信")
    private Boolean autoDmEnabled;

    @Schema(description = "每日评论上限")
    @Min(value = 0, message = "每日评论上限不能小于0")
    private Integer dailyCommentLimit;

    @Schema(description = "每日私信上限")
    @Min(value = 0, message = "每日私信上限不能小于0")
    private Integer dailyDmLimit;

    @Schema(description = "回复延迟范围，JSON数组")
    private String replyDelayRange;

    @Schema(description = "人设配置 JSON")
    private String personaConfig;

    @Schema(description = "状态：0-停用 1-启用")
    @Min(value = 0, message = "状态不正确")
    @Max(value = 1, message = "状态不正确")
    private Integer status;

}
