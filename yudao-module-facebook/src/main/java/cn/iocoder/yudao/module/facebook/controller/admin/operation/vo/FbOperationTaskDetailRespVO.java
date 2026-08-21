package cn.iocoder.yudao.module.facebook.controller.admin.operation.vo;

import io.swagger.v3.oas.annotations.media.Schema;
import lombok.*;

import java.util.List;

@Schema(description = "管理后台 - 运营任务详情 Response VO（包含主任务和明细）")
@Data
public class FbOperationTaskDetailRespVO {

    @Schema(description = "主任务信息", requiredMode = Schema.RequiredMode.REQUIRED)
    private FbOperationTaskRespVO task;

    @Schema(description = "任务明细列表")
    private List<FbOperationTaskDetailItemVO> details;

    @Schema(description = "加组结果列表")
    private List<FbOperationAddGroupResultRespVO> results;

    @Schema(description = "转帖结果列表")
    private List<FbRepostResultRespVO> repostResults;

    @Schema(description = "发群帖结果列表")
    private List<FbGroupPublishResultRespVO> groupPublishResults;

    @Data
    public static class FbOperationTaskDetailItemVO {
        @Schema(description = "明细ID", requiredMode = Schema.RequiredMode.REQUIRED, example = "1")
        private Long id;

        @Schema(description = "Facebook账号ID", requiredMode = Schema.RequiredMode.REQUIRED, example = "10001234567890")
        private String accountId;

        @Schema(description = "Facebook账号", example = "user@example.com")
        private String fbAccount;

        @Schema(description = "目标链接列表（换行分隔）")
        private String targetUrls;

        @Schema(description = "目标群组ID列表（逗号分隔）")
        private String targetGroupIds;

        @Schema(description = "帖子链接（转帖任务）")
        private String postUrl;

        @Schema(description = "执行项配置（JSON，转帖任务）")
        private String actionConfig;

        @Schema(description = "期望数量", example = "100")
        private Integer expectedCount;

        @Schema(description = "实际完成数量", example = "50")
        private Integer actualCount;

        @Schema(description = "状态（0-待执行 1-执行中 2-已完成 3-失败）", example = "0")
        private Integer status;

        @Schema(description = "计划进入账号执行队列的时间")
        private java.time.LocalDateTime scheduledTime;

        @Schema(description = "开始时间")
        private java.time.LocalDateTime startTime;

        @Schema(description = "结束时间")
        private java.time.LocalDateTime endTime;

        @Schema(description = "错误信息")
        private String errorMsg;

        @Schema(description = "目标用户FB ID（群发私信明细）")
        private String targetUserId;

        @Schema(description = "私信话术（群发私信明细）")
        private String scriptContent;

        @Schema(description = "发送时间（群发私信明细）")
        private java.time.LocalDateTime sendTime;

        @Schema(description = "创建时间", requiredMode = Schema.RequiredMode.REQUIRED)
        private java.time.LocalDateTime createTime;
    }

}
