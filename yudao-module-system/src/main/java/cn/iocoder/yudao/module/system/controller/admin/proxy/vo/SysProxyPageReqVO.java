
package cn.iocoder.yudao.module.system.controller.admin.proxy.vo;

import cn.iocoder.yudao.framework.common.pojo.PageParam;
import io.swagger.v3.oas.annotations.media.Schema;
import lombok.Data;

@Schema(description = "管理后台 - 代理信息分页请求 VO")
@Data
public class SysProxyPageReqVO extends PageParam {

    @Schema(description = "代理名称", example = "美国")
    private String proxyName;

    @Schema(description = "代理类型（1-HTTP, 2-HTTPS, 3-SOCKS5）", example = "1")
    private Integer proxyType;

    @Schema(description = "代理服务器地址", example = "127.0.0.1")
    private String host;

    @Schema(description = "状态（0-禁用，1-启用）", example = "1")
    private Integer status;

    @Schema(description = "国家/地区", example = "美国")
    private String country;

}
