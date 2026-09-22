package cn.iocoder.yudao.module.facebook.controller.admin.aisearch.vo;
import jakarta.validation.constraints.NotBlank;
import lombok.Data;
@Data
public class AiSearchExpansionReqVO {
    @NotBlank private String userGoal;
    private String company;
    private String keywords;
    private String hsCode;
    private String targetCountry;
    private String customerType;
    private Integer keywordLimit = 6;
    private Integer sceneLimit = 4;
}
