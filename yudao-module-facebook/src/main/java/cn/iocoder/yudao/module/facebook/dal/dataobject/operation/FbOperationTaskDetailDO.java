package cn.iocoder.yudao.module.facebook.dal.dataobject.operation;

import cn.iocoder.yudao.framework.tenant.core.db.TenantBaseDO;
import com.baomidou.mybatisplus.annotation.KeySequence;
import com.baomidou.mybatisplus.annotation.TableId;
import com.baomidou.mybatisplus.annotation.TableName;
import lombok.*;

/**
 * 运营任务明细表 DO
 *
 * @author 芋道源码
 */
@TableName("fb_operation_task_detail")
@KeySequence("fb_operation_task_detail_seq")
@Data
@EqualsAndHashCode(callSuper = true)
@ToString(callSuper = true)
@Builder
@NoArgsConstructor
@AllArgsConstructor
public class FbOperationTaskDetailDO extends TenantBaseDO {

    /**
     * 明细ID
     */
    @TableId
    private Long id;
    /**
     * 任务ID
     */
    private Long taskId;
    /**
     * Facebook账号ID
     */
    private String accountId;
    /**
     * Facebook账号
     */
    private String fbAccount;
    /**
     * 目标链接列表（换行分隔）
     */
    private String targetUrls;
    /**
     * 目标群组ID列表（逗号分隔）
     */
    private String targetGroupIds;
    /**
     * 帖子链接
     */
    private String postUrl;
    /**
     * 执行项配置（JSON格式）
     */
    private String actionConfig;
    /**
     * 评论话术
     */
    private String commentScript;
    /**
     * 话术库ID
     */
    private Long scriptLibraryId;
    /**
     * 期望数量
     */
    private Integer expectedCount;
    /**
     * 实际完成数量
     */
    private Integer actualCount;
    /**
     * 状态（0-待执行 1-执行中 2-已完成 3-失败）
     */
    private Integer status;
    /**
     * 开始时间
     */
    private java.time.LocalDateTime startTime;
    /**
     * 结束时间
     */
    private java.time.LocalDateTime endTime;
    /**
     * 错误信息
     */
    private String errorMsg;

}
