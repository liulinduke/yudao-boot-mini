package cn.iocoder.yudao.module.facebook.controller.admin.account.vo;

import io.swagger.v3.oas.annotations.media.Schema;
import lombok.Data;

import jakarta.validation.constraints.NotEmpty;
import java.util.List;

@Schema(description = "管理后台 - FB账号批量更新分组 Request VO")
@Data
public class FbAccountUpdateGroupReqVO {

    @Schema(description = "账号ID列表", requiredMode = Schema.RequiredMode.REQUIRED)
    @NotEmpty(message = "账号ID列表不能为空")
    private List<Long> ids;

    @Schema(description = "分组ID")
    private Long groupId;

}
