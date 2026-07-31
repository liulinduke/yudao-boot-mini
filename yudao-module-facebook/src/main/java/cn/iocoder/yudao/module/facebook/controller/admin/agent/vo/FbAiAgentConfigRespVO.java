package cn.iocoder.yudao.module.facebook.controller.admin.agent.vo;

import com.fasterxml.jackson.databind.annotation.JsonSerialize;
import com.fasterxml.jackson.databind.ser.std.ToStringSerializer;
import io.swagger.v3.oas.annotations.media.Schema;
import lombok.Data;

import java.time.LocalDateTime;

@Schema(description = "管理后台 - Facebook AI获客Agent配置 Response VO")
@Data
public class FbAiAgentConfigRespVO {

    @Schema(description = "编号")
    @JsonSerialize(using = ToStringSerializer.class)
    private Long id;

    @Schema(description = "Agent名称")
    private String agentName;

    @Schema(description = "Agent类型")
    private String agentType;

    @Schema(description = "搜索方式")
    private String searchMode;

    @Schema(description = "关键词搜索链接模板")
    private String searchUrlTemplate;

    @Schema(description = "用户主营/出口产品")
    private String exportProduct;

    @Schema(description = "知识库ID列表，逗号分隔")
    private String knowledgeIds;

    @Schema(description = "关键词种子，JSON数组")
    private String seedKeywords;

    @Schema(description = "最终关键词池，JSON数组")
    private String keywordPool;

    @Schema(description = "关键词轮询游标")
    private Integer keywordCursor;

    @Schema(description = "每轮执行关键词数量")
    private Integer keywordsPerRun;

    @Schema(description = "是否启用AI扩展关键词")
    private Boolean aiKeywordExpandEnabled;

    @Schema(description = "AI扩展关键词数量")
    private Integer aiKeywordExpandCount;

    @Schema(description = "目标客户数量")
    private Integer targetCustomerCount;

    @Schema(description = "执行频率")
    private String executeFrequency;


    @Schema(description = "执行时间，格式 HH:mm")
    private String executeTime;

    @Schema(description = "最近一次自动调度执行时间")
    private LocalDateTime lastExecuteTime;

    @Schema(description = "目标国家，JSON数组")
    private String targetCountries;

    @Schema(description = "目标语言，JSON数组")
    private String targetLanguages;

    @Schema(description = "账号ID列表，逗号分隔")
    private String accountIds;

    @Schema(description = "监控群组ID列表，逗号分隔")
    private String monitorGroupIds;

    @Schema(description = "触达评分阈值")
    private Integer touchScoreThreshold;

    @Schema(description = "线索评分工作流ID")
    private Long leadScoreWorkflowId;

    @Schema(description = "评论生成工作流ID")
    private Long commentWorkflowId;

    @Schema(description = "私信生成工作流ID")
    private Long dmWorkflowId;

    @Schema(description = "是否自动评论")
    private Boolean autoCommentEnabled;

    @Schema(description = "是否自动私信")
    private Boolean autoDmEnabled;

    @Schema(description = "每日评论上限")
    private Integer dailyCommentLimit;

    @Schema(description = "每日私信上限")
    private Integer dailyDmLimit;

    @Schema(description = "回复延迟范围，JSON数组")
    private String replyDelayRange;

    @Schema(description = "人设配置 JSON")
    private String personaConfig;

    @Schema(description = "AI业务员人设类型")
    private String personaType;

    @Schema(description = "线索数量")
    private Long leadCount;

    @Schema(description = "待处理数量")
    private Long pendingCount;

    @Schema(description = "状态：0草稿 1运行中 2暂停 3停止")
    private Integer status;

    @Schema(description = "创建时间")
    private LocalDateTime createTime;

}
