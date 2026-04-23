package cn.iocoder.yudao.module.facebook.dal.dataobject.operation;

import cn.iocoder.yudao.framework.tenant.core.db.TenantBaseDO;
import com.baomidou.mybatisplus.annotation.KeySequence;
import com.baomidou.mybatisplus.annotation.TableId;
import com.baomidou.mybatisplus.annotation.TableName;
import lombok.*;

/**
 * 运营任务主表 DO
 *
 * @author 芋道源码
 */
@TableName("fb_operation_task")
@KeySequence("fb_operation_task_seq") // 用于 Oracle、PostgreSQL、Kingbase、DB2、H2 数据库的主键自增。如果是 MySQL 等数据库，可不写。
@Data
@EqualsAndHashCode(callSuper = true)
@ToString(callSuper = true)
@Builder
@NoArgsConstructor
@AllArgsConstructor
public class FbOperationTaskDO extends TenantBaseDO {

    /**
     * 任务ID
     */
    @TableId
    private Long id;
    /**
     * 任务类型（1-链接加组 2-转贴 3-群发私信）
     */
    private Integer taskType;
    /**
     * 任务名称
     */
    private String taskName;
    /**
     * 任务状态（0-待执行 1-执行中 2-已完成 3-已停止 4-失败）
     */
    private Integer status;
    /**
     * 期望数量
     */
    private Integer expectedCount;
    /**
     * 实际完成数量
     */
    private Integer actualCount;
    /**
     * 账号ID列表（逗号分隔）
     */
    private String accountIds;
    /**
     * 开始时间
     */
    private java.time.LocalDateTime startTime;
    /**
     * 结束时间
     */
    private java.time.LocalDateTime endTime;
    /**
     * 备注
     */
    private String remark;

}
