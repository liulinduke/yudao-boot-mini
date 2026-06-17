
package cn.iocoder.yudao.module.system.controller.admin.proxy.vo;

import io.swagger.v3.oas.annotations.media.Schema;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.NotNull;
import lombok.Data;

@Schema(description = "管理后台 - 代理信息更新请求 VO")
@Data
public class SysProxyUpdateReqVO {

    @Schema(description = "代理ID", requiredMode = Schema.RequiredMode.REQUIRED, example = "1")
    @NotNull(message = "代理ID不能为空")
    private Long id;

    @Schema(description = "代理名称", requiredMode = Schema.RequiredMode.REQUIRED, example = "美国代理01")
    @NotBlank(message = "代理名称不能为空")
    private String proxyName;

    @Schema(description = "代理类型（1-HTTP, 2-HTTPS, 3-SOCKS5）", requiredMode = Schema.RequiredMode.REQUIRED, example = "1")
    @NotNull(message = "代理类型不能为空")
    private Integer proxyType;

    @Schema(description = "代理服务器地址", requiredMode = Schema.RequiredMode.REQUIRED, example = "127.0.0.1")
    @NotBlank(message = "代理服务器地址不能为空")
    private String host;

    @Schema(description = "代理端口", requiredMode = Schema.RequiredMode.REQUIRED, example = "8080")
    @NotNull(message = "代理端口不能为空")
    private Integer port;

    @Schema(description = "代理认证用户名", example = "user")
    private String username;

    @Schema(description = "代理认证密码", example = "password")
    private String password;

    @Schema(description = "国家/地区", example = "美国")
    private String country;

    @Schema(description = "状态（0-禁用，1-启用）", example = "1")
    private Integer status;

    @Schema(description = "备注", example = "测试代理")
    private String remark;

}
