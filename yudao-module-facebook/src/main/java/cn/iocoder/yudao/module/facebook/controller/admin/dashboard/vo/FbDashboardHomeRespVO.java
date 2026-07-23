package cn.iocoder.yudao.module.facebook.controller.admin.dashboard.vo;

import io.swagger.v3.oas.annotations.media.Schema;
import lombok.AllArgsConstructor;
import lombok.Builder;
import lombok.Data;
import lombok.NoArgsConstructor;

import java.util.List;

@Schema(description = "管理后台 - AI 获客首页响应 VO")
@Data
public class FbDashboardHomeRespVO {

    @Schema(description = "AI 获客成果")
    private AiResult aiResult;

    @Schema(description = "社媒采集汇总")
    private SocialSummary socialCollection;

    @Schema(description = "社媒运营汇总")
    private SocialSummary socialOperation;

    @Schema(description = "AI 获客成果")
    @Data
    @Builder
    @NoArgsConstructor
    @AllArgsConstructor
    public static class AiResult {
        @Schema(description = "自动发现线索数")
        private Long autoCollectedLeadCount;

        @Schema(description = "自动分析客户数")
        private Long autoAnalyzedCustomerCount;

        @Schema(description = "生成互动建议数")
        private Long generatedInteractionSuggestionCount;

        @Schema(description = "自动完成触达数")
        private Long autoTouchedCount;
    }

    @Schema(description = "社媒汇总")
    @Data
    @Builder
    @NoArgsConstructor
    @AllArgsConstructor
    public static class SocialSummary {
        @Schema(description = "今日总量")
        private Long total;

        @Schema(description = "按类型统计")
        private List<SocialItem> items;
    }

    @Schema(description = "社媒明细")
    @Data
    @Builder
    @NoArgsConstructor
    @AllArgsConstructor
    public static class SocialItem {
        @Schema(description = "类型")
        private String type;

        @Schema(description = "数量")
        private Long count;
    }
}
