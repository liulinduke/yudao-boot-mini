package cn.iocoder.yudao.module.facebook.controller.admin.account.vo;

import io.swagger.v3.oas.annotations.media.Schema;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.NotNull;
import lombok.Data;

@Schema(description = "管理后台 - FB账号登录结果更新 Request VO")
@Data
public class FbAccountLoginResultUpdateReqVO {

    @Schema(description = "账号ID", requiredMode = Schema.RequiredMode.REQUIRED, example = "1")
    @NotNull(message = "账号ID不能为空")
    private Long id;

    @Schema(description = "登录状态", requiredMode = Schema.RequiredMode.REQUIRED, example = "SUCCESS")
    @NotBlank(message = "登录状态不能为空")
    private String loginStatus;

    @Schema(description = "登录异常原因")
    private String loginErrorReason;

    @Schema(description = "Cookie JSON")
    private String cookie;
}
