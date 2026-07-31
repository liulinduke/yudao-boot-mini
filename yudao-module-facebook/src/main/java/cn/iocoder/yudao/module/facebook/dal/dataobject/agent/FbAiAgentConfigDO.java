package cn.iocoder.yudao.module.facebook.dal.dataobject.agent;

import cn.iocoder.yudao.framework.tenant.core.db.TenantBaseDO;
import com.baomidou.mybatisplus.annotation.IdType;
import com.baomidou.mybatisplus.annotation.KeySequence;
import com.baomidou.mybatisplus.annotation.TableId;
import com.baomidou.mybatisplus.annotation.TableName;
import com.baomidou.mybatisplus.annotation.TableField;
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

    @TableId(type = IdType.ASSIGN_ID)
    private Long id;

    /**
     * Agent 名称
     */
    private String agentName;

    /**
     * Agent类型：page_lead/post_lead/group_post/group_comment/competitor_buyer
     */
    private String agentType;

    /**
     * 搜索方式：keyword/link
     */
    private String searchMode;

    /**
     * 关键词搜索链接模板。链接模式下仅替换其中的 q 参数，保留其他筛选条件。
     */
    private String searchUrlTemplate;

    /**
     * 用户主营/出口产品
     */
    private String exportProduct;

    /**
     * 知识库ID列表，逗号分隔
     */
    private String knowledgeIds;

    /**
     * 关键词种子，JSON数组
     */
    private String seedKeywords;

    /**
     * 最终关键词池，JSON数组
     */
    private String keywordPool;

    /**
     * 关键词轮询游标
     */
    private Integer keywordCursor;

    /**
     * 每轮执行关键词数量
     */
    private Integer keywordsPerRun;

    /**
     * 是否启用AI扩展关键词
     */
    private Boolean aiKeywordExpandEnabled;

    /**
     * AI扩展关键词数量
     */
    private Integer aiKeywordExpandCount;

    /**
     * 目标客户数量
     */
    private Integer targetCustomerCount;

    /**
     * 执行频率：1-7天，1表示每天
     */
    private String executeFrequency;

    /**
     * 执行时间，格式 HH:mm
     */
    private String executeTime;

    /**
     * 最近一次自动调度执行时间
     */
    private java.time.LocalDateTime lastExecuteTime;

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

    /** 账号分配模式：AUTO/MANUAL。 */
    private String accountSelectionMode;

    /**
     * 监控群组ID列表，逗号分隔
     */
    private String monitorGroupIds;

    /**
     * 触达评分阈值
     */
    private Integer touchScoreThreshold;

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
     * AI业务员人设类型
     */
    private String personaType;

    /**
     * 状态：0草稿 1运行中 2暂停 3停止
     */
    private Integer status;

    /**
     * 线索数（非持久化）
     */
    @TableField(exist = false)
    private Long leadCount;

    /**
     * 待处理数（非持久化）
     */
    @TableField(exist = false)
    private Long pendingCount;

}
