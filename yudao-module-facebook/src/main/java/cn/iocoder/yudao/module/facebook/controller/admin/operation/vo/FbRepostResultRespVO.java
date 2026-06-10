package cn.iocoder.yudao.module.facebook.controller.admin.operation.vo;

import io.swagger.v3.oas.annotations.media.Schema;
import lombok.Data;

import java.time.LocalDateTime;

@Schema(description = "管理后台 - 转帖结果 Response VO")
@Data
public class FbRepostResultRespVO {

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

    @Schema(description = "原帖子链接")
    private String postUrl;

    @Schema(description = "操作类型（1-点赞 2-转发到动态消息 3-已废弃 4-转贴到好友 5-转发到群组）")
    private Integer actionType;

    @Schema(description = "目标类型（friend/group）")
    private String targetType;

    @Schema(description = "目标ID")
    private String targetId;

    @Schema(description = "目标名称")
    private String targetName;

    @Schema(description = "目标链接")
    private String targetUrl;

    @Schema(description = "状态（0-待处理 1-成功 2-失败 3-待审核）")
    private Integer status;

    @Schema(description = "失败原因")
    private String failReason;

    @Schema(description = "执行时间")
    private LocalDateTime executeTime;

    @Schema(description = "备注")
    private String remark;

    @Schema(description = "创建时间", requiredMode = Schema.RequiredMode.REQUIRED)
    private LocalDateTime createTime;

}
