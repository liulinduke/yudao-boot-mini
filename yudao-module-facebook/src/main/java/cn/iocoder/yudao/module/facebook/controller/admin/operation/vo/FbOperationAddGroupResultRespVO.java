package cn.iocoder.yudao.module.facebook.controller.admin.operation.vo;

import io.swagger.v3.oas.annotations.media.Schema;
import lombok.*;

import java.time.LocalDateTime;

@Schema(description = "管理后台 - 加组结果 Response VO")
@Data
public class FbOperationAddGroupResultRespVO {

    @Schema(description = "结果ID", requiredMode = Schema.RequiredMode.REQUIRED, example = "1")
    private Long id;

    @Schema(description = "任务明细ID", requiredMode = Schema.RequiredMode.REQUIRED, example = "1024")
    private Long detailId;

    @Schema(description = "任务ID", requiredMode = Schema.RequiredMode.REQUIRED, example = "512")
    private Long taskId;

    @Schema(description = "Facebook账号ID", requiredMode = Schema.RequiredMode.REQUIRED, example = "10001234567890")
    private String accountId;

    @Schema(description = "Facebook账号", example = "user@example.com")
    private String fbAccount;

    @Schema(description = "用户主页链接", example = "https://www.facebook.com/profile.php?id=xxx")
    private String targetUrl;

    @Schema(description = "群组ID", example = "987654321")
    private String groupId;

    @Schema(description = "群组名称", example = "测试群组")
    private String groupName;

    @Schema(description = "群组链接", example = "https://www.facebook.com/groups/987654321")
    private String groupUrl;

    @Schema(description = "加组状态（0-待处理 1-成功 2-失败 3-已加入/待审核）", example = "1")
    private Integer joinStatus;

    @Schema(description = "失败原因", example = "网络错误")
    private String failReason;

    @Schema(description = "加入时间")
    private LocalDateTime joinTime;

    @Schema(description = "同步时间")
    private LocalDateTime syncTime;

    @Schema(description = "创建时间", requiredMode = Schema.RequiredMode.REQUIRED)
    private LocalDateTime createTime;

}
