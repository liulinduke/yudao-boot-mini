package cn.iocoder.yudao.module.facebook.dal.dataobject.message;

import cn.iocoder.yudao.framework.tenant.core.db.TenantBaseDO;
import com.baomidou.mybatisplus.annotation.TableId;
import com.baomidou.mybatisplus.annotation.TableName;
import lombok.*;

import java.time.LocalDateTime;

@TableName("facebook_message_conversation")
@Data
@EqualsAndHashCode(callSuper = true)
@ToString(callSuper = true)
@Builder
@NoArgsConstructor
@AllArgsConstructor
public class FbMessageConversationDO extends TenantBaseDO {
    @TableId
    private Long id;
    private Long accountId;
    private String conversationKey;
    private String targetUserId;
    private String targetName;
    private String targetUrl;
    private String targetAvatar;
    private String sourceType;
    private String detectedLanguage;
    private String replyTargetLanguage;
    private Integer unreadCount;
    private String lastMessagePreview;
    private LocalDateTime lastMessageTime;
    private LocalDateTime lastReadTime;
    private Integer status;
}
