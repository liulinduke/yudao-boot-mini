package cn.iocoder.yudao.module.facebook.dal.dataobject.account;

import cn.iocoder.yudao.framework.tenant.core.db.TenantBaseDO;
import com.baomidou.mybatisplus.annotation.KeySequence;
import com.baomidou.mybatisplus.annotation.TableId;
import com.baomidou.mybatisplus.annotation.TableName;
import lombok.*;

import java.time.LocalDateTime;

/** Facebook账号长期操作统计。 */
@TableName("facebook_account_action_stat")
@KeySequence("facebook_account_action_stat_seq")
@Data
@EqualsAndHashCode(callSuper = true)
@ToString(callSuper = true)
@Builder
@NoArgsConstructor
@AllArgsConstructor
public class FbAccountActionStatDO extends TenantBaseDO {

    @TableId
    private Long id;

    private Long accountId;

    /** dm/repost/join_group/comment/follow/collect */
    private String actionType;

    /** 成功完成的任务数。 */
    private Long totalTaskCount;

    /** 成功执行的操作数。 */
    private Long totalActionCount;

    /** 采集到的条数，仅 collect 使用。 */
    private Long totalCollectCount;

    private LocalDateTime lastExecuteTime;

    private LocalDateTime lastSuccessTime;
}
