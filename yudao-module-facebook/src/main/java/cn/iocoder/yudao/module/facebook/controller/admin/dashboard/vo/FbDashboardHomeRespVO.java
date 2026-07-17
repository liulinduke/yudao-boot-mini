package cn.iocoder.yudao.module.facebook.controller.admin.dashboard.vo;

import io.swagger.v3.oas.annotations.media.Schema;
import lombok.AllArgsConstructor;
import lombok.Builder;
import lombok.Data;
import lombok.NoArgsConstructor;

import java.util.List;

@Schema(description = "管理后台 - Facebook AI 获客首页响应 VO")
@Data
public class FbDashboardHomeRespVO {

    @Schema(description = "首页摘要")
    private Summary summary;

    @Schema(description = "指标卡")
    private List<MetricCard> metrics;

    @Schema(description = "推荐线索")
    private List<RecommendedLead> recommendedLeads;

    @Schema(description = "自动化结果与待办")
    private AutomationAndTodo automationAndTodos;

    @Schema(description = "首页摘要")
    @Data
    @Builder
    @NoArgsConstructor
    @AllArgsConstructor
    public static class Summary {
        @Schema(description = "主文案")
        private String headline;

        @Schema(description = "补充说明")
        private String subline;

        @Schema(description = "新增线索数")
        private Long leadCount;

        @Schema(description = "高意向客户数")
        private Long highIntentCount;

        @Schema(description = "推荐联系数")
        private Long recommendedCount;
    }

    @Schema(description = "指标卡")
    @Data
    @Builder
    @NoArgsConstructor
    @AllArgsConstructor
    public static class MetricCard {
        @Schema(description = "指标标识")
        private String key;

        @Schema(description = "标题")
        private String title;

        @Schema(description = "值")
        private Long value;

        @Schema(description = "较昨日变化")
        private Long delta;

        @Schema(description = "变化文案")
        private String deltaLabel;

        @Schema(description = "跳转路径")
        private String routePath;
    }

    @Schema(description = "推荐线索")
    @Data
    @Builder
    @NoArgsConstructor
    @AllArgsConstructor
    public static class RecommendedLead {
        @Schema(description = "线索ID")
        private Long id;

        @Schema(description = "客户名称")
        private String customerName;

        @Schema(description = "来源渠道")
        private String source;

        @Schema(description = "意向等级")
        private String intentLevel;

        @Schema(description = "推荐原因")
        private String aiReason;

        @Schema(description = "建议动作")
        private String recommendedAction;

        @Schema(description = "主页链接")
        private String targetUrl;
    }

    @Schema(description = "自动化结果与待办")
    @Data
    @Builder
    @NoArgsConstructor
    @AllArgsConstructor
    public static class AutomationAndTodo {
        @Schema(description = "自动化结果")
        private List<AutomationItem> automationItems;

        @Schema(description = "待办")
        private List<TodoItem> todoItems;
    }

    @Schema(description = "自动化结果")
    @Data
    @Builder
    @NoArgsConstructor
    @AllArgsConstructor
    public static class AutomationItem {
        @Schema(description = "标题")
        private String title;

        @Schema(description = "值")
        private Long value;

        @Schema(description = "说明")
        private String description;
    }

    @Schema(description = "待办")
    @Data
    @Builder
    @NoArgsConstructor
    @AllArgsConstructor
    public static class TodoItem {
        @Schema(description = "标题")
        private String title;

        @Schema(description = "数量")
        private Long count;

        @Schema(description = "重要程度")
        private String level;

        @Schema(description = "跳转路径")
        private String routePath;
    }
}
