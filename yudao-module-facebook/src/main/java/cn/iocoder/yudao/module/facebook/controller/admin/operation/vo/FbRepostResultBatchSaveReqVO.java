package cn.iocoder.yudao.module.facebook.controller.admin.operation.vo;

import io.swagger.v3.oas.annotations.media.Schema;
import lombok.*;

import javax.validation.constraints.NotEmpty;
import javax.validation.constraints.NotNull;
import java.util.List;

@Schema(description = "管理后台 - 转帖结果批量保存 Request VO")
@Data
public class FbRepostResultBatchSaveReqVO {

    @Schema(description = "任务明细ID", requiredMode = Schema.RequiredMode.REQUIRED, example = "1024")
    @NotNull(message = "任务明细ID不能为空")
    private Long detailId;

    @Schema(description = "转帖结果列表", requiredMode = Schema.RequiredMode.REQUIRED)
    @NotEmpty(message = "转帖结果列表不能为空")
    private List<FbRepostResultItem> results;

    @Data
    public static class FbRepostResultItem {
        
        @Schema(description = "Facebook账号ID", example = "10001234567890")
        private String accountId;

        @Schema(description = "Facebook账号", example = "user@example.com")
        private String fbAccount;

        @Schema(description = "原帖子链接", example = "https://www.facebook.com/xxx/posts/123")
        private String postUrl;

        @Schema(description = "操作类型（1-点赞 2-转发到动态 3-转帖到个人中心 4-转贴到好友 5-转发到群组）", example = "5")
        private Integer actionType;

        @Schema(description = "目标类型（friend/group）", example = "group")
        private String targetType;

        @Schema(description = "目标ID（好友ID或群组ID）", example = "987654321")
        private String targetId;

        @Schema(description = "目标名称", example = "测试群组")
        private String targetName;

        @Schema(description = "目标链接", example = "https://www.facebook.com/groups/987654321")
        private String targetUrl;

        @Schema(description = "状态（0-待处理 1-成功 2-失败）", example = "1")
        private Integer status;

        @Schema(description = "失败原因", example = "网络错误")
        private String failReason;

        @Schema(description = "执行时间", example = "2024-01-01 12:00:00")
        private java.time.LocalDateTime executeTime;

        @Schema(description = "备注", example = "转帖成功")
        private String remark;
    }

}
