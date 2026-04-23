package cn.iocoder.yudao.module.facebook.dal.dataobject.operation;

import cn.iocoder.yudao.framework.tenant.core.db.TenantBaseDO;
import com.baomidou.mybatisplus.annotation.KeySequence;
import com.baomidou.mybatisplus.annotation.TableId;
import com.baomidou.mybatisplus.annotation.TableName;
import lombok.*;

/**
 * 链接加组结果表 DO
 *
 * @author 芋道源码
 */
@TableName("fb_operation_add_group_result")
@KeySequence("fb_operation_add_group_result_seq")
@Data
@EqualsAndHashCode(callSuper = true)
@ToString(callSuper = true)
@Builder
@NoArgsConstructor
@AllArgsConstructor
public class FbOperationAddGroupResultDO extends TenantBaseDO {

    /**
     * 结果ID
     */
    @TableId
    private Long id;
    /**
     * 任务明细ID
     */
    private Long detailId;
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
     * 用户主页链接
     */
    private String targetUrl;
    /**
     * 群组ID
     */
    private String groupId;
    /**
     * 群组名称
     */
    private String groupName;
    /**
     * 群组链接
     */
    private String groupUrl;
    /**
     * 加组状态（0-待处理 1-成功 2-失败 3-已加入/待审核）
     */
    private Integer joinStatus;
    /**
     * 失败原因
     */
    private String failReason;
    /**
     * 加入时间
     */
    private java.time.LocalDateTime joinTime;
    /**
     * 同步时间
     */
    private java.time.LocalDateTime syncTime;

}
