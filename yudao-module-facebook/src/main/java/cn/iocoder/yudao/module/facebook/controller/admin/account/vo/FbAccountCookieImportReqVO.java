package cn.iocoder.yudao.module.facebook.controller.admin.account.vo;

import io.swagger.v3.oas.annotations.media.Schema;
import lombok.Data;

import jakarta.validation.constraints.NotBlank;

@Schema(description = "管理后台 - FB账号Cookie导入 Request VO")
@Data
public class FbAccountCookieImportReqVO {

    @Schema(description = "Cookie数据", requiredMode = Schema.RequiredMode.REQUIRED)
    @NotBlank(message = "Cookie数据不能为空")
    private String data;

    @Schema(description = "分组ID")
    private Long groupId;

    @Schema(description = "代理ID")
    private Long proxyId;

    @Schema(description = "是否固定使用此Cookie")
    private Boolean useSessionCookie;

}