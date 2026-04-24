package cn.iocoder.yudao.module.facebook.dal.dataobject.operation;

import cn.iocoder.yudao.framework.tenant.core.db.TenantBaseDO;
import com.baomidou.mybatisplus.annotation.KeySequence;
import com.baomidou.mybatisplus.annotation.TableId;
import com.baomidou.mybatisplus.annotation.TableName;
import lombok.*;

/**
 * Facebook转帖结果表 DO
 *
 * @author 芋道源码
 */
@TableName("fb_repost_result")
@KeySequence("fb_repost_result_seq")
@Data
@EqualsAndHashCode(callSuper = true)
@ToString(callSuper = true)
@Builder
@NoArgsConstructor
@AllArgsConstructor
public class FbRepostResultDO extends TenantBaseDO {

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
     * 原帖子链接
     */
    private String postUrl;
    /**
     * 操作类型（1-点赞 2-转发到动态 3-转帖到个人中心 4-转贴到好友 5-转发到群组）
     */
    private Integer actionType;
    /**
     * 目标类型（friend/group）
     */
    private String targetType;
    /**
     * 目标ID（好友ID或群组ID）
     */
    private String targetId;
    /**
     * 目标名称
     */
    private String targetName;
    /**
     * 目标链接
     */
    private String targetUrl;
    /**
     * 状态（0-待处理 1-成功 2-失败）
     */
    private Integer status;
    /**
     * 失败原因
     */
    private String failReason;
    /**
     * 执行时间
     */
    private java.time.LocalDateTime executeTime;
    /**
     * 备注
     */
    private String remark;

}
