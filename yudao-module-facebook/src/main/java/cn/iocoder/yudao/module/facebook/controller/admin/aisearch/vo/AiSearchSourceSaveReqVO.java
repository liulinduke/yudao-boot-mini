package cn.iocoder.yudao.module.facebook.controller.admin.aisearch.vo;
import jakarta.validation.constraints.NotBlank;
import lombok.Data;
@Data public class AiSearchSourceSaveReqVO {
    private Long id; @NotBlank private String name; private String domain; private String sourceType;
    private String sourceRole; private String countries; private String industries; private String customerTypes;
    private String keywords; private String languages; private String entityTypes; private String discoveryMethods;
    private String capabilities; private Integer priority = 0; private String status = "CANDIDATE"; private Boolean verified = false;
}
