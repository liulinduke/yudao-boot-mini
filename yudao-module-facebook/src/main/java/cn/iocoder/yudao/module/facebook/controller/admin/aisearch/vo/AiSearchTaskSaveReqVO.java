package cn.iocoder.yudao.module.facebook.controller.admin.aisearch.vo;
import io.swagger.v3.oas.annotations.media.Schema;
import jakarta.validation.constraints.*;
import lombok.Data;
@Data
public class AiSearchTaskSaveReqVO {
    private Long id;
    @NotBlank private String userGoal;
    private String name;
    private String targetCountry;
    @Min(1) private Integer targetCount = 100;
    private String customerType;
    private String referenceWebsite;
    private String hsCodes;
    private Boolean aiKeywordExpand = true;
    private Boolean contactEnrichment = true;
    private String positionStrategy;
    private String scheduleType = "ONCE";
    private String scheduleInterval;
}
