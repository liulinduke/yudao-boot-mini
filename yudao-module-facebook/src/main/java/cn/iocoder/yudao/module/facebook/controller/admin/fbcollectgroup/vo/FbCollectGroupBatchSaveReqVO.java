package cn.iocoder.yudao.module.facebook.controller.admin.fbcollectgroup.vo;

import io.swagger.v3.oas.annotations.media.Schema;
import lombok.*;

import jakarta.validation.Valid;
import jakarta.validation.constraints.*;
import java.util.List;

@Schema(description = "管理后台 - FB群组采集结果批量保存 Request VO")
@Data
public class FbCollectGroupBatchSaveReqVO {

    @Schema(description = "明细ID", requiredMode = Schema.RequiredMode.REQUIRED, example = "123456")
    @NotNull(message = "明细ID不能为空")
    private Long detailId;

    @Schema(description = "采集结果列表")
    @Valid
    private List<FbCollectGroupSaveReqVO> results;

}
