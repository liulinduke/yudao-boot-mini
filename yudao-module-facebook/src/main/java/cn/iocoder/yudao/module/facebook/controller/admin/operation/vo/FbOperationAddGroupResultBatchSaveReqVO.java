package cn.iocoder.yudao.module.facebook.controller.admin.operation.vo;

import io.swagger.v3.oas.annotations.media.Schema;
import lombok.*;

import javax.validation.constraints.NotEmpty;
import javax.validation.constraints.NotNull;
import java.util.List;

@Schema(description = "管理后台 - 批量保存链接加组结果 Request VO")
@Data
public class FbOperationAddGroupResultBatchSaveReqVO {

    @Schema(description = "任务明细ID", requiredMode = Schema.RequiredMode.REQUIRED, example = "1")
    @NotNull(message = "任务明细ID不能为空")
    private Long detailId;

    @Schema(description = "加组结果列表", requiredMode = Schema.RequiredMode.REQUIRED)
    @NotEmpty(message = "加组结果列表不能为空")
    private List<FbOperationAddGroupResultItem> results;

    @Data
    public static class FbOperationAddGroupResultItem {
        @Schema(description = "Facebook账号ID", example = "10001234567890")
        private String accountId;

        @Schema(description = "Facebook账号", example = "user@example.com")
        private String fbAccount;

        @Schema(description = "用户主页链接", example = "https://www.facebook.com/profile.php?id=xxx")
        private String targetUrl;

        @Schema(description = "群组ID", example = "1142005467206051")
        private String groupId;

        @Schema(description = "群组名称", example = "测试群组")
        private String groupName;

        @Schema(description = "群组链接", example = "https://www.facebook.com/groups/1142005467206051/")
        private String groupUrl;

        @Schema(description = "加组状态（0-待处理 1-成功 2-失败 3-已加入/待审核）", example = "1")
        private Integer joinStatus;

        @Schema(description = "失败原因", example = "群组已满")
        private String failReason;

        @Schema(description = "加入时间")
        private java.time.LocalDateTime joinTime;

        @Schema(description = "同步时间")
        private java.time.LocalDateTime syncTime;
    }

}
