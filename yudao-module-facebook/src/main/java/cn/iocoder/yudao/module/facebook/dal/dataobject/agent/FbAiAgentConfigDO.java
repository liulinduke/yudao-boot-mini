package cn.iocoder.yudao.module.facebook.dal.dataobject.agent;

import cn.iocoder.yudao.framework.tenant.core.db.TenantBaseDO;
import com.baomidou.mybatisplus.annotation.KeySequence;
import com.baomidou.mybatisplus.annotation.TableId;
import com.baomidou.mybatisplus.annotation.TableName;
import lombok.*;

/**
 * Facebook AI获客Agent配置 DO
 */
@TableName("fb_ai_agent_config")
@KeySequence("fb_ai_agent_config_seq")
@Data
@EqualsAndHashCode(callSuper = true)
@ToString(callSuper = true)
@Builder
@NoArgsConstructor
@AllArgsConstructor
public class FbAiAgentConfigDO extends TenantBaseDO {

    @TableId
    private Long id;

    /**
     * Agent 名称
     */
    private String agentName;

    /**
     * 知识库ID列表，逗号分隔
     */
    private String knowledgeIds;

    /**
     * 关键词种子，JSON数组
     */
    private String seedKeywords;

    /**
     * 目标国家，JSON数组
     */
    private String targetCountries;

    /**
     * 目标语言，JSON数组
     */
    private String targetLanguages;

    /**
     * 执行账号ID列表，逗号分隔
     */
    private String accountIds;

    /**
     * 监控群组ID列表，逗号分隔
     */
    private String monitorGroupIds;

    /**
     * 线索评分工作流ID
     */
    private Long leadScoreWorkflowId;

    /**
     * 评论生成工作流ID
     */
    private Long commentWorkflowId;

    /**
     * 私信生成工作流ID
     */
    private Long dmWorkflowId;

    /**
     * 是否自动评论
     */
    private Boolean autoCommentEnabled;

    /**
     * 是否自动私信
     */
    private Boolean autoDmEnabled;

    /**
     * 每日评论上限
     */
    private Integer dailyCommentLimit;

    /**
     * 每日私信上限
     */
    private Integer dailyDmLimit;

    /**
     * 回复延迟范围，JSON数组，如 [180,600]
     */
    private String replyDelayRange;

    /**
     * 人设和回复策略 JSON
     */
    private String personaConfig;

    /**
     * 状态：0-停用 1-启用
     */
    private Integer status;

}
