package cn.iocoder.yudao.module.facebook.controller.admin.agent.vo;

import io.swagger.v3.oas.annotations.media.Schema;
import jakarta.validation.constraints.Min;
import lombok.Data;
import jakarta.validation.constraints.Size;

import java.util.List;

@Schema(description = "管理后台 - Facebook AI获客Agent关键词生成 Request VO")
@Data
public class FbAiKeywordGenerateReqVO {

    @Schema(description = "AI获客类型：page_lead/post_lead")
    private String agentType;

    @Schema(description = "种子关键词")
    private List<String> seedKeywords;

    @Schema(description = "目标国家/市场")
    private List<String> targetCountries;

    @Schema(description = "主营/出口产品说明")
    @Size(max = 255, message = "主营/出口产品说明不能超过 255 个字符")
    private String productDescription;

    @Schema(description = "关键词输出语言")
    private String targetLanguage;

    @Schema(description = "扩展数量")
    @Min(value = 1, message = "扩展数量不能小于1")
    private Integer expandCount;

}
