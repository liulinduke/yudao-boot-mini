package cn.iocoder.yudao.module.facebook.controller.admin.agent.vo;

import io.swagger.v3.oas.annotations.media.Schema;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.NotNull;
import lombok.Data;

@Schema(description = "管理后台 - Facebook AI线索分析保存 Request VO")
@Data
public class FbAiLeadAnalysisSaveReqVO {

    @Schema(description = "线索类型：user/post", requiredMode = Schema.RequiredMode.REQUIRED)
    @NotBlank(message = "线索类型不能为空")
    private String leadType;

    @Schema(description = "线索ID", requiredMode = Schema.RequiredMode.REQUIRED)
    @NotNull(message = "线索ID不能为空")
    private Long leadId;

    @Schema(description = "AI标签，逗号分隔")
    private String aiTags;

    @Schema(description = "意向等级：high/medium/low/unknown")
    private String intentLevel;

    @Schema(description = "意向判断理由")
    private String intentReason;

    @Schema(description = "情绪")
    private String sentiment;

    @Schema(description = "线索类型")
    private String leadCategory;

    @Schema(description = "国家")
    private String country;

    @Schema(description = "语言")
    private String language;

    @Schema(description = "产品相关度")
    private Integer productRelevanceScore;

    @Schema(description = "AI摘要")
    private String aiSummary;

    @Schema(description = "触达状态")
    private String touchStatus;

}
