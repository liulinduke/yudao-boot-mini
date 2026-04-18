package cn.iocoder.yudao.module.facebook.controller.admin.collectuser.vo;

import io.swagger.v3.oas.annotations.media.Schema;
import lombok.*;

import javax.validation.Valid;
import javax.validation.constraints.*;
import java.util.List;

@Schema(description = "管理后台 - FB用户采集结果批量保存 Request VO")
@Data
public class FbCollectUserBatchSaveReqVO {

    @Schema(description = "任务ID", requiredMode = Schema.RequiredMode.REQUIRED, example = "15717")
    @NotNull(message = "任务ID不能为空")
    private Long taskId;

    @Schema(description = "采集结果列表", requiredMode = Schema.RequiredMode.REQUIRED)
    @NotEmpty(message = "采集结果列表不能为空")
    @Valid
    private List<FbCollectUserSaveReqVO> results;

}
