package cn.iocoder.yudao.module.facebook.controller.admin.operation.vo;

import com.fasterxml.jackson.databind.ser.std.ToStringSerializer;
import com.fasterxml.jackson.databind.annotation.JsonSerialize;
import io.swagger.v3.oas.annotations.media.Schema;
import lombok.*;

import java.time.LocalDateTime;

@Schema(description = "管理后台 - 运营任务 Response VO")
@Data
public class FbOperationTaskRespVO {

    @Schema(description = "任务ID", requiredMode = Schema.RequiredMode.REQUIRED, example = "1")
    @JsonSerialize(using = ToStringSerializer.class)
    private Long id;

    @Schema(description = "任务类型（9-链接加组 10-转贴 11-群发私信 12-发个人帖 13-发群帖）", requiredMode = Schema.RequiredMode.REQUIRED, example = "9")
    private Integer taskType;

    @Schema(description = "任务名称", example = "链接加组任务")
    private String taskName;

    @Schema(description = "任务状态（0-待执行 1-执行中 2-已完成 3-已停止 4-失败）", requiredMode = Schema.RequiredMode.REQUIRED, example = "0")
    private Integer status;

    @Schema(description = "期望数量", example = "100")
    private Integer expectedCount;

    @Schema(description = "实际完成数量", example = "50")
    private Integer actualCount;

    @Schema(description = "账号ID列表（逗号分隔）", example = "1,2,3")
    private String accountIds;

    @Schema(description = "动作配置（JSON格式）")
    private String actionConfig;

    @Schema(description = "开始时间")
    private LocalDateTime startTime;

    @Schema(description = "结束时间")
    private LocalDateTime endTime;

    @Schema(description = "备注", example = "测试任务")
    private String remark;

    @Schema(description = "创建时间", requiredMode = Schema.RequiredMode.REQUIRED)
    private LocalDateTime createTime;

    @Schema(description = "数据来源：operation-运营任务表 dm-私信任务表", example = "operation")
    private String sourceType;

}
