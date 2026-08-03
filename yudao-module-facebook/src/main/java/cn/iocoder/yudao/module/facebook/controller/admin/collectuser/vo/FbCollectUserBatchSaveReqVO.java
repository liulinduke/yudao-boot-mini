package cn.iocoder.yudao.module.facebook.controller.admin.collectuser.vo;

import io.swagger.v3.oas.annotations.media.Schema;
import lombok.*;

import jakarta.validation.Valid;
import jakarta.validation.constraints.*;
import java.util.List;

@Schema(description = "管理后台 - FB用户采集结果批量保存 Request VO")
@Data
public class FbCollectUserBatchSaveReqVO {

    @Schema(description = "明细ID", requiredMode = Schema.RequiredMode.REQUIRED, example = "123456")
    @NotNull(message = "明细ID不能为空")
    private Long detailId;

    @Schema(description = "采集结果资源分组ID")
    private Long resourceGroupId;

    @Schema(description = "采集结果列表", requiredMode = Schema.RequiredMode.REQUIRED)
    @Valid
    private List<FbCollectUserSaveReqVO> results;

}
