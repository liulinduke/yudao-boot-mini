package cn.iocoder.yudao.module.facebook.controller.admin.account.vo;

import io.swagger.v3.oas.annotations.media.Schema;
import lombok.Data;

import jakarta.validation.constraints.NotBlank;

@Schema(description = "管理后台 - FB账号导入 Request VO")
@Data
public class FbAccountImportReqVO {

    @Schema(description = "账号数据", requiredMode = Schema.RequiredMode.REQUIRED)
    @NotBlank(message = "账号数据不能为空")
    private String data;

    @Schema(description = "分组ID")
    private Long groupId;

    @Schema(description = "代理ID")
    private Long proxyId;

}