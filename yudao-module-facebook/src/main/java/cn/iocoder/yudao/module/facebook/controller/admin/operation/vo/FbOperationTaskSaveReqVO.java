package cn.iocoder.yudao.module.facebook.controller.admin.operation.vo;

import io.swagger.v3.oas.annotations.media.Schema;
import lombok.*;

import javax.validation.constraints.NotEmpty;
import javax.validation.constraints.NotNull;
import java.util.List;

@Schema(description = "管理后台 - 运营任务创建/修改 Request VO")
@Data
public class FbOperationTaskSaveReqVO {

    @Schema(description = "任务ID", example = "1")
    private Long id;

    @Schema(description = "任务类型（1-链接加组 2-转贴 3-群发私信）", requiredMode = Schema.RequiredMode.REQUIRED, example = "1")
    @NotNull(message = "任务类型不能为空")
    private Integer taskType;

    @Schema(description = "任务名称", example = "链接加组任务")
    private String taskName;

    @Schema(description = "账号ID列表", requiredMode = Schema.RequiredMode.REQUIRED)
    @NotEmpty(message = "账号ID列表不能为空")
    private List<String> accountIds;

    @Schema(description = "目标链接列表（换行分隔）", example = "https://www.facebook.com/profile.php?id=xxx")
    private String targetUrls;

    @Schema(description = "目标群组ID列表（逗号分隔）", example = "123456,789012")
    private String targetGroupIds;

    @Schema(description = "期望数量", requiredMode = Schema.RequiredMode.REQUIRED, example = "100")
    @NotNull(message = "期望数量不能为空")
    private Integer expectedCount;

    @Schema(description = "备注", example = "测试任务")
    private String remark;

}
