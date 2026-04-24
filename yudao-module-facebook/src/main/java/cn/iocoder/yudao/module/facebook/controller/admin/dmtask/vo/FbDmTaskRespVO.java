package cn.iocoder.yudao.module.facebook.controller.admin.dmtask.vo;

import io.swagger.v3.oas.annotations.media.Schema;
import lombok.Data;

import java.time.LocalDateTime;
import java.util.List;

@Schema(description = "管理后台 - Facebook 群发私信任务 Response VO")
@Data
public class FbDmTaskRespVO {

    @Schema(description = "主键ID", requiredMode = Schema.RequiredMode.REQUIRED, example = "1")
    private Long id;

    @Schema(description = "任务名称", example = "测试任务")
    private String taskName;

    @Schema(description = "目标用户FB ID列表")
    private List<String> targetUserIds;

    @Schema(description = "话术列表")
    private List<String> scripts;

    @Schema(description = "话术类型：1手动输入 2话术库", example = "1")
    private Integer scriptType;

    @Schema(description = "是否追加随机表情", example = "false")
    private Boolean appendRandomEmoji;

    @Schema(description = "执行账号ID列表")
    private List<String> accountIds;

    @Schema(description = "最小间隔(秒)", example = "4")
    private Integer minIntervalSeconds;

    @Schema(description = "最大间隔(秒)", example = "10")
    private Integer maxIntervalSeconds;

    @Schema(description = "状态：0待执行 1执行中 2已完成 3失败 4已取消", example = "0")
    private Integer status;

    @Schema(description = "总任务数", example = "100")
    private Integer totalCount;

    @Schema(description = "已完成数", example = "50")
    private Integer completedCount;

    @Schema(description = "失败数", example = "5")
    private Integer failedCount;

    @Schema(description = "备注", example = "备注信息")
    private String remark;

    @Schema(description = "开始时间")
    private LocalDateTime startTime;

    @Schema(description = "结束时间")
    private LocalDateTime endTime;

    @Schema(description = "错误信息")
    private String errorMsg;

    @Schema(description = "创建时间", requiredMode = Schema.RequiredMode.REQUIRED)
    private LocalDateTime createTime;

}
