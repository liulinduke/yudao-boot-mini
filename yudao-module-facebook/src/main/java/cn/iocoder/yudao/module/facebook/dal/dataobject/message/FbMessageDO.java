package cn.iocoder.yudao.module.facebook.dal.dataobject.message;

import cn.iocoder.yudao.framework.tenant.core.db.TenantBaseDO;
import com.baomidou.mybatisplus.annotation.TableId;
import com.baomidou.mybatisplus.annotation.TableName;
import lombok.*;

import java.time.LocalDateTime;

@TableName("facebook_message")
@Data
@EqualsAndHashCode(callSuper = true)
@ToString(callSuper = true)
@Builder
@NoArgsConstructor
@AllArgsConstructor
public class FbMessageDO extends TenantBaseDO {
    @TableId
    private Long id;
    private Long conversationId;
    private Long accountId;
    private String externalMessageId;
    private String direction;
    private String sourceType;
    private String senderUserId;
    private String senderName;
    private String originalText;
    private String detectedLanguage;
    private String translatedText;
    private String targetLanguage;
    private Integer translationStatus;
    private Boolean isRead;
    private Integer sendStatus;
    private Long sendTaskId;
    private Long sendDetailId;
    private String sourcePostId;
    private String sourcePostUrl;
    private String sourceCommentId;
    private LocalDateTime messageTime;
    private LocalDateTime sendTime;
    private String errorMessage;
}
