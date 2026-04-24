package cn.iocoder.yudao.module.facebook.controller.admin.dmtask.vo;

import io.swagger.v3.oas.annotations.media.Schema;
import lombok.Data;

import javax.validation.constraints.NotEmpty;
import javax.validation.constraints.NotNull;
import java.util.List;

@Schema(description = "管理后台 - Facebook 群发私信任务保存 Request VO")
@Data
public class FbDmTaskSaveReqVO {

    @Schema(description = "主键ID", example = "1")
    private Long id;

    @Schema(description = "任务名称", example = "测试任务")
    private String taskName;

    @Schema(description = "目标用户FB ID列表", requiredMode = Schema.RequiredMode.REQUIRED)
    @NotEmpty(message = "目标用户不能为空")
    private List<String> targetUserIds;

    @Schema(description = "话术列表", requiredMode = Schema.RequiredMode.REQUIRED)
    @NotEmpty(message = "话术不能为空")
    private List<String> scripts;

    @Schema(description = "话术类型：1手动输入 2话术库", requiredMode = Schema.RequiredMode.REQUIRED, example = "1")
    @NotNull(message = "话术类型不能为空")
    private Integer scriptType;

    @Schema(description = "是否追加随机表情", example = "false")
    private Boolean appendRandomEmoji;

    @Schema(description = "执行账号ID列表", requiredMode = Schema.RequiredMode.REQUIRED)
    @NotEmpty(message = "执行账号不能为空")
    private List<String> accountIds;

    @Schema(description = "最小间隔(秒)", example = "4")
    private Integer minIntervalSeconds;

    @Schema(description = "最大间隔(秒)", example = "10")
    private Integer maxIntervalSeconds;

    @Schema(description = "备注", example = "备注信息")
    private String remark;

}
