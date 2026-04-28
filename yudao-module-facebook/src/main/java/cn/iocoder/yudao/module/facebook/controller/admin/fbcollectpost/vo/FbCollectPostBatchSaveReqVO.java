package cn.iocoder.yudao.module.facebook.controller.admin.fbcollectpost.vo;

import io.swagger.v3.oas.annotations.media.Schema;
import lombok.*;

import javax.validation.Valid;
import javax.validation.constraints.*;
import java.util.List;

@Schema(description = "管理后台 - FB帖子采集结果批量保存 Request VO")
@Data
public class FbCollectPostBatchSaveReqVO {

    @Schema(description = "明细ID", requiredMode = Schema.RequiredMode.REQUIRED, example = "123456")
    @NotNull(message = "明细ID不能为空")
    private Long detailId;

    @Schema(description = "采集结果列表", requiredMode = Schema.RequiredMode.REQUIRED)
    @NotEmpty(message = "采集结果列表不能为空")
    @Valid
    private List<FbCollectPostSaveReqVO> results;

}
