
package cn.iocoder.yudao.module.facebook.controller.admin.operation.vo;

import io.swagger.v3.oas.annotations.media.Schema;
import lombok.Data;

import java.time.LocalDateTime;

@Schema(description = "管理后台 - 发群帖结果 Response VO")
@Data
public class FbGroupPublishResultRespVO {

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

    @Schema(description = "群组ID")
    private String groupId;

    @Schema(description = "群名")
    private String groupName;

    @Schema(description = "群链接")
    private String groupUrl;

    @Schema(description = "帖子内容")
    private String targetUrl;

    @Schema(description = "发布状态（0-待执行 1-成功 2-失败）")
    private Integer joinStatus;

    @Schema(description = "失败原因")
    private String failReason;

    @Schema(description = "发布时间")
    private LocalDateTime joinTime;

    @Schema(description = "创建时间", requiredMode = Schema.RequiredMode.REQUIRED)
    private LocalDateTime createTime;

    public Integer getStatus() {
        return joinStatus;
    }

    public void setStatus(Integer status) {
        this.joinStatus = status;
    }

    public String getPostContent() {
        return targetUrl;
    }

    public void setPostContent(String postContent) {
        this.targetUrl = postContent;
    }

    public LocalDateTime getExecuteTime() {
        return joinTime;
    }

    public void setExecuteTime(LocalDateTime executeTime) {
        this.joinTime = executeTime;
    }

}
