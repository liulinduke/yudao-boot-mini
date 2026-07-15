package cn.iocoder.yudao.module.facebook.controller.admin.message.vo;

import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.NotNull;
import lombok.Data;

@Data
public class FbMessageSendReqVO {
    @NotNull
    private Long accountId;
    @NotBlank
    private String targetUserId;
    private String targetName;
    private String targetUrl;
    private String conversationKey;
    @NotBlank
    private String text;
    private String targetLanguage;
}
