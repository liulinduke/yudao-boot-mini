package cn.iocoder.yudao.module.facebook.dal.dataobject.dmtask;

import cn.iocoder.yudao.framework.tenant.core.db.TenantBaseDO;
import com.baomidou.mybatisplus.annotation.KeySequence;
import com.baomidou.mybatisplus.annotation.TableId;
import com.baomidou.mybatisplus.annotation.TableName;
import lombok.*;

import java.time.LocalDateTime;

/**
 * Facebook 群发私信任务明细 DO
 *
 * @author 芋道源码
 */
@TableName("facebook_dm_task_detail")
@KeySequence("facebook_dm_task_detail_seq")
@Data
@EqualsAndHashCode(callSuper = true)
@ToString(callSuper = true)
@Builder
@NoArgsConstructor
@AllArgsConstructor
public class FbDmTaskDetailDO extends TenantBaseDO {

    /**
     * 主键ID
     */
    @TableId
    private Long id;

    /**
     * 任务ID
     */
    private Long taskId;

    /**
     * 执行账号ID
     */
    private String accountId;

    /**
     * 目标用户FB ID
     */
    private String targetUserId;

    /**
     * 使用的话术
     */
    private String scriptContent;

    /**
     * 状态：0待执行 1成功 2失败
     */
    private Integer status;

    /**
     * 错误信息
     */
    private String errorMsg;

    /**
     * 发送时间
     */
    private LocalDateTime sendTime;

}
