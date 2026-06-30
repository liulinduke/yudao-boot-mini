package cn.iocoder.yudao.module.facebook.dal.dataobject.agent;

import cn.iocoder.yudao.framework.tenant.core.db.TenantBaseDO;
import com.baomidou.mybatisplus.annotation.IdType;
import com.baomidou.mybatisplus.annotation.KeySequence;
import com.baomidou.mybatisplus.annotation.TableId;
import com.baomidou.mybatisplus.annotation.TableName;
import lombok.*;

import java.time.LocalDateTime;

/**
 * Facebook AI触达记录 DO
 */
@TableName("fb_ai_touch_record")
@KeySequence("fb_ai_touch_record_seq")
@Data
@EqualsAndHashCode(callSuper = true)
@ToString(callSuper = true)
@Builder
@NoArgsConstructor
@AllArgsConstructor
public class FbAiTouchRecordDO extends TenantBaseDO {

    @TableId(type = IdType.ASSIGN_ID)
    private Long id;

    /**
     * Agent配置ID
     */
    private Long agentConfigId;

    /**
     * 线索类型：user/post/comment
     */
    private String leadType;

    /**
     * 线索ID
     */
    private Long leadId;

    /**
     * 目标用户ID
     */
    private String targetUserId;

    /**
     * 目标链接
     */
    private String targetUrl;

    /**
     * 执行账号数据库ID
     */
    private Long accountDbId;

    /**
     * Facebook账号ID
     */
    private String accountId;

    /**
     * Facebook账号
     */
    private String fbAccount;

    /**
     * 触达类型：comment/dm
     */
    private String touchType;

    /**
     * AI生成内容
     */
    private String generatedContent;

    /**
     * AI判断理由
     */
    private String aiReason;

    /**
     * 状态：0-待发送 1-发送中 2-成功 3-失败 4-跳过
     */
    private Integer status;

    /**
     * 失败原因
     */
    private String failReason;

    /**
     * 计划发送时间
     */
    private LocalDateTime scheduledTime;

    /**
     * 实际发送时间
     */
    private LocalDateTime sentTime;

    /**
     * 关联运营任务ID
     */
    private Long operationTaskId;

    /**
     * 关联运营任务明细ID
     */
    private Long operationDetailId;

}
