package cn.iocoder.yudao.module.facebook.controller.admin.message.vo;

import lombok.Data;

@Data
public class FbMessageIngestReqVO {
    private Long accountId;
    private String conversationKey;
    private String targetUserId;
    private String targetName;
    private String targetUrl;
    private String sourceType;
    private String externalMessageId;
    private String direction;
    private String senderUserId;
    private String senderName;
    private String originalText;
    private String detectedLanguage;
    private String translatedText;
    private String sourcePostId;
    private String sourcePostUrl;
    private String sourceCommentId;
    private String messageTime;
}
