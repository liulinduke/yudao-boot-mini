package cn.iocoder.yudao.module.facebook.controller.admin.agent.vo;

import io.swagger.v3.oas.annotations.media.Schema;
import jakarta.validation.constraints.Min;
import lombok.Data;

import java.util.List;

@Schema(description = "管理后台 - Facebook AI获客Agent关键词生成 Request VO")
@Data
public class FbAiKeywordGenerateReqVO {

    @Schema(description = "种子关键词")
    private List<String> seedKeywords;

    @Schema(description = "目标国家/市场")
    private List<String> targetCountries;

    @Schema(description = "行业/产品描述")
    private String productDescription;

    @Schema(description = "扩展数量")
    @Min(value = 1, message = "扩展数量不能小于1")
    private Integer expandCount;

}
