package cn.iocoder.yudao.module.facebook.controller.admin.operation.vo;

import io.swagger.v3.oas.annotations.media.Schema;
import lombok.*;
import cn.iocoder.yudao.framework.common.pojo.PageParam;

import java.time.LocalDateTime;

@Schema(description = "管理后台 - 运营任务分页 Request VO")
@Data
public class FbOperationTaskPageReqVO extends PageParam {

    @Schema(description = "任务类型（1-链接加组 2-转贴 3-群发私信）", example = "1")
    private Integer taskType;

    @Schema(description = "任务状态（0-待执行 1-执行中 2-已完成 3-已停止 4-失败）", example = "0")
    private Integer status;

    @Schema(description = "创建时间")
    private LocalDateTime[] createTime;

}
