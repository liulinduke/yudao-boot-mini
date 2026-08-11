package cn.iocoder.yudao.module.facebook.controller.admin.account.vo;

import lombok.Data;

/** 供 WPF 当前任务使用的代理配置。该 VO 只由账号运行时接口返回。 */
@Data
public class FbAccountRuntimeProxyRespVO {
    private Integer proxyType;
    private String host;
    private Integer port;
    private String username;
    private String password;
}
