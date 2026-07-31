package cn.iocoder.yudao.module.facebook.controller.admin.agent.vo;

import io.swagger.v3.oas.annotations.media.Schema;
import jakarta.validation.constraints.Max;
import jakarta.validation.constraints.Min;
import jakarta.validation.constraints.NotBlank;
import lombok.Data;

@Schema(description = "管理后台 - Facebook AI获客Agent配置保存 Request VO")
@Data
public class FbAiAgentConfigSaveReqVO {

    @Schema(description = "编号")
    private Long id;

    @Schema(description = "Agent名称", requiredMode = Schema.RequiredMode.REQUIRED)
    @NotBlank(message = "Agent名称不能为空")
    private String agentName;

    @Schema(description = "Agent类型：page_lead")
    private String agentType;

    @Schema(description = "搜索方式：keyword/link")
    private String searchMode;

    @Schema(description = "关键词搜索链接模板，链接模式下仅替换 q 参数")
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
    @Min(value = 0, message = "关键词游标不能小于0")
    private Integer keywordCursor;

    @Schema(description = "每轮执行关键词数量")
    @Min(value = 1, message = "每轮执行关键词数量不能小于1")
    private Integer keywordsPerRun;

    @Schema(description = "是否启用AI扩展关键词")
    private Boolean aiKeywordExpandEnabled;

    @Schema(description = "AI扩展关键词数量")
    @Min(value = 1, message = "AI扩展关键词数量不能小于1")
    private Integer aiKeywordExpandCount;

    @Schema(description = "目标客户数量")
    @Min(value = 1, message = "目标客户数量不能小于1")
    private Integer targetCustomerCount;

    @Schema(description = "执行频率：1-7天，1表示每天")
    private String executeFrequency;

    @Schema(description = "执行时间，格式 HH:mm")
    private String executeTime;

    @Schema(description = "目标国家，JSON数组")
    private String targetCountries;

    @Schema(description = "目标语言，JSON数组")
    private String targetLanguages;

    @Schema(description = "账号ID列表，逗号分隔")
    private String accountIds;

    @Schema(description = "账号分配模式：AUTO程序自动选择，MANUAL手动选择")
    private String accountSelectionMode = "AUTO";

    @Schema(description = "监控群组ID列表，逗号分隔")
    private String monitorGroupIds;

    @Schema(description = "触达评分阈值")
    @Min(value = 0, message = "触达评分阈值不能小于0")
    @Max(value = 100, message = "触达评分阈值不能大于100")
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
    @Min(value = 0, message = "每日评论上限不能小于0")
    private Integer dailyCommentLimit;

    @Schema(description = "每日私信上限")
    @Min(value = 0, message = "每日私信上限不能小于0")
    private Integer dailyDmLimit;

    @Schema(description = "回复延迟范围，JSON数组")
    private String replyDelayRange;

    @Schema(description = "人设配置 JSON")
    private String personaConfig;

    @Schema(description = "AI业务员人设类型")
    private String personaType;

    @Schema(description = "状态：0草稿 1运行中 2暂停 3停止")
    @Min(value = 0, message = "状态不正确")
    @Max(value = 3, message = "状态不正确")
    private Integer status;

}
