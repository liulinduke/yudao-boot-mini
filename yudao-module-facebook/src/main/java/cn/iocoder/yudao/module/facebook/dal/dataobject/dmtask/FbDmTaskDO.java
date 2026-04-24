package cn.iocoder.yudao.module.facebook.dal.dataobject.dmtask;

import cn.iocoder.yudao.framework.tenant.core.db.TenantBaseDO;
import com.baomidou.mybatisplus.annotation.KeySequence;
import com.baomidou.mybatisplus.annotation.TableId;
import com.baomidou.mybatisplus.annotation.TableName;
import lombok.*;

import java.time.LocalDateTime;

/**
 * Facebook 群发私信任务 DO
 *
 * @author 芋道源码
 */
@TableName("facebook_dm_task")
@KeySequence("facebook_dm_task_seq")
@Data
@EqualsAndHashCode(callSuper = true)
@ToString(callSuper = true)
@Builder
@NoArgsConstructor
@AllArgsConstructor
public class FbDmTaskDO extends TenantBaseDO {

    /**
     * 主键ID
     */
    @TableId
    private Long id;

    /**
     * 任务名称
     */
    private String taskName;

    /**
     * 目标用户FB ID列表(JSON数组)
     */
    private String targetUserIds;

    /**
     * 话术列表(JSON数组)
     */
    private String scripts;

    /**
     * 话术类型：1手动输入 2话术库
     */
    private Integer scriptType;

    /**
     * 是否追加随机表情
     */
    private Boolean appendRandomEmoji;

    /**
     * 执行账号ID列表(JSON数组)
     */
    private String accountIds;

    /**
     * 最小间隔(秒)
     */
    private Integer minIntervalSeconds;

    /**
     * 最大间隔(秒)
     */
    private Integer maxIntervalSeconds;

    /**
     * 状态：0待执行 1执行中 2已完成 3失败 4已取消
     */
    private Integer status;

    /**
     * 总任务数
     */
    private Integer totalCount;

    /**
     * 已完成数
     */
    private Integer completedCount;

    /**
     * 失败数
     */
    private Integer failedCount;

    /**
     * 备注
     */
    private String remark;

    /**
     * 开始时间
     */
    private LocalDateTime startTime;

    /**
     * 结束时间
     */
    private LocalDateTime endTime;

    /**
     * 错误信息
     */
    private String errorMsg;

}
