package cn.iocoder.yudao.module.facebook.dal.dataobject.message;

import cn.iocoder.yudao.framework.tenant.core.db.TenantBaseDO;
import com.fasterxml.jackson.databind.annotation.JsonSerialize;
import com.fasterxml.jackson.databind.ser.std.ToStringSerializer;
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
    @JsonSerialize(using = ToStringSerializer.class)
    private Long id;
    @JsonSerialize(using = ToStringSerializer.class)
    private Long accountId;
    private Integer receiveEnabled;
    private Integer onlineStatus;
    private String mode;
    private Integer checkIntervalMinutes;
    /** 每日定时接收时间，格式：06:00,08:00,12:00。 */
    private String scheduleTimes;
    private LocalDateTime nextCheckTime;
    private LocalDateTime lastCheckTime;
    private LocalDateTime lastSuccessTime;
    private Integer status;
    private String errorMessage;
    private Integer messengerUnreadCount;
    private Integer notificationUnreadCount;
    private LocalDateTime lastBadgeCheckTime;
}
