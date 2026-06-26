package cn.iocoder.yudao.module.facebook.dal.dataobject.agent;

import cn.iocoder.yudao.framework.tenant.core.db.TenantBaseDO;
import com.baomidou.mybatisplus.annotation.KeySequence;
import com.baomidou.mybatisplus.annotation.TableId;
import com.baomidou.mybatisplus.annotation.TableName;
import lombok.*;

/**
 * Facebook AI获客Agent客户发现日志 DO
 */
@TableName("fb_ai_agent_discovery_log")
@KeySequence("fb_ai_agent_discovery_log_seq")
@Data
@EqualsAndHashCode(callSuper = true)
@ToString(callSuper = true)
@Builder
@NoArgsConstructor
@AllArgsConstructor
public class FbAiAgentDiscoveryLogDO extends TenantBaseDO {

    @TableId
    private Long id;

    /**
     * Agent配置ID
     */
    private Long agentConfigId;

    /**
     * 关键词
     */
    private String keyword;

    /**
     * 发现来源：page
     */
    private String sourceType;

    /**
     * 发现客户数
     */
    private Integer discoveredCount;

    /**
     * 高意向客户数
     */
    private Integer highIntentCount;

    /**
     * 主页采集数
     */
    private Integer pageCollectCount;

    /**
     * AI分析数
     */
    private Integer aiAnalyzeCount;

    /**
     * 过滤数
     */
    private Integer filteredCount;

    /**
     * 最终线索数
     */
    private Integer finalLeadCount;

    /**
     * 关联采集任务ID
     */
    private Long collectTaskId;

}
