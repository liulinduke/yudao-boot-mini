package cn.iocoder.yudao.module.facebook.controller.admin.dmtask.vo;

import io.swagger.v3.oas.annotations.media.Schema;
import lombok.Data;

import javax.validation.constraints.NotBlank;
import javax.validation.constraints.NotNull;

@Schema(description = "管理后台 - 上报群发私信明细结果 Request VO")
@Data
public class FbDmTaskDetailReportReqVO {

    @Schema(description = "明细ID（字符串，避免前端雪花ID精度丢失）", requiredMode = Schema.RequiredMode.REQUIRED, example = "2064167790520016897")
    @NotBlank(message = "明细ID不能为空")
    private String detailId;

    @Schema(description = "状态（1-成功 2-失败）", requiredMode = Schema.RequiredMode.REQUIRED, example = "1")
    @NotNull(message = "状态不能为空")
    private Integer status;

    @Schema(description = "失败原因", example = "发送超时")
    private String errorMsg;

}
