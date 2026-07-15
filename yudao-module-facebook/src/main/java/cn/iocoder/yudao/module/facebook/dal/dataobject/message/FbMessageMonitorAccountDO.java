package cn.iocoder.yudao.module.facebook.dal.dataobject.message;

import cn.iocoder.yudao.framework.tenant.core.db.TenantBaseDO;
import com.baomidou.mybatisplus.annotation.TableId;
import com.baomidou.mybatisplus.annotation.TableName;
import lombok.*;

import java.time.LocalDateTime;

@TableName("facebook_message_monitor_account")
@Data
@EqualsAndHashCode(callSuper = true)
@ToString(callSuper = true)
@Builder
@NoArgsConstructor
@AllArgsConstructor
public class FbMessageMonitorAccountDO extends TenantBaseDO {
    @TableId
    private Long id;
    private Long accountId;
    private String mode;
    private Integer checkIntervalMinutes;
    private LocalDateTime nextCheckTime;
    private LocalDateTime lastCheckTime;
    private LocalDateTime lastSuccessTime;
    private Integer status;
    private String errorMessage;
    private Integer messengerUnreadCount;
    private Integer notificationUnreadCount;
    private LocalDateTime lastBadgeCheckTime;
}
