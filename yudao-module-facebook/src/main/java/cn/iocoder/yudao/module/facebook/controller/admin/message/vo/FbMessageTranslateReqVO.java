package cn.iocoder.yudao.module.facebook.controller.admin.message.vo;

import jakarta.validation.constraints.NotBlank;
import lombok.Data;

@Data
public class FbMessageTranslateReqVO {
    @NotBlank
    private String text;
    private String sourceLanguage;
    @NotBlank
    private String targetLanguage;
    private String context;
}
