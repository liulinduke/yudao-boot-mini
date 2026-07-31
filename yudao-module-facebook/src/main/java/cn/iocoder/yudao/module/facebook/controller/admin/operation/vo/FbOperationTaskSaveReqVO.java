package cn.iocoder.yudao.module.facebook.controller.admin.operation.vo;

import io.swagger.v3.oas.annotations.media.Schema;
import jakarta.validation.constraints.NotEmpty;
import jakarta.validation.constraints.NotNull;
import lombok.Data;

import java.util.List;

@Schema(description = "管理后台 - 运营任务创建/修改 Request VO")
@Data
public class FbOperationTaskSaveReqVO {

    @Schema(description = "任务ID", example = "1")
    private Long id;

    @Schema(description = "任务类型", requiredMode = Schema.RequiredMode.REQUIRED, example = "10")
    @NotNull(message = "任务类型不能为空")
    private Integer taskType;

    @Schema(description = "任务名称", example = "转贴任务")
    private String taskName;

    @Schema(description = "账号ID列表", requiredMode = Schema.RequiredMode.REQUIRED)
    private List<String> accountIds;

    @Schema(description = "账号分配模式：AUTO程序自动选择，MANUAL手动选择")
    private String accountSelectionMode = "AUTO";

    @Schema(description = "目标链接列表", example = "https://www.facebook.com/profile.php?id=xxx")
    private String targetUrls;

    @Schema(description = "目标群组ID列表", example = "123456,789012")
    private String targetGroupIds;

    @Schema(description = "期望数量", requiredMode = Schema.RequiredMode.REQUIRED, example = "100")
    @NotNull(message = "期望数量不能为空")
    private Integer expectedCount;

    @Schema(description = "备注", example = "测试任务")
    private String remark;

    @Schema(description = "帖子链接（转贴/帖子评论任务）")
    private String postUrl;

    @Schema(description = "帖子链接列表（帖子评论任务）")
    private List<String> postUrls;

    @Schema(description = "执行项配置（JSON）")
    private String actionConfig;

    @Schema(description = "评论话术")
    private String commentScript;

    @Schema(description = "话术库ID")
    private Long scriptLibraryId;
}
