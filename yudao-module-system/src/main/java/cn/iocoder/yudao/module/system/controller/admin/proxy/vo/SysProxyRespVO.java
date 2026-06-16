
package cn.iocoder.yudao.module.system.controller.admin.proxy.vo;

import io.swagger.v3.oas.annotations.media.Schema;
import lombok.Data;

import java.time.LocalDateTime;

@Schema(description = "管理后台 - 代理信息 Response VO")
@Data
public class SysProxyRespVO {

    @Schema(description = "代理ID", requiredMode = Schema.RequiredMode.REQUIRED, example = "1")
    private Long id;

    @Schema(description = "代理名称", requiredMode = Schema.RequiredMode.REQUIRED, example = "美国代理01")
    private String proxyName;

    @Schema(description = "代理类型（1-HTTP, 2-HTTPS, 3-SOCKS5）", example = "1")
    private Integer proxyType;

    @Schema(description = "代理类型名称", example = "HTTP")
    private String proxyTypeName;

    @Schema(description = "代理服务器地址", requiredMode = Schema.RequiredMode.REQUIRED, example = "127.0.0.1")
    private String host;

    @Schema(description = "代理端口", requiredMode = Schema.RequiredMode.REQUIRED, example = "8080")
    private Integer port;

    @Schema(description = "代理认证用户名", example = "user")
    private String username;

    @Schema(description = "国家/地区", example = "美国")
    private String country;

    @Schema(description = "状态（0-禁用，1-启用）", example = "1")
    private Integer status;

    @Schema(description = "状态名称", example = "启用")
    private String statusName;

    @Schema(description = "备注", example = "测试代理")
    private String remark;

    @Schema(description = "创建时间", requiredMode = Schema.RequiredMode.REQUIRED)
    private LocalDateTime createTime;

}
