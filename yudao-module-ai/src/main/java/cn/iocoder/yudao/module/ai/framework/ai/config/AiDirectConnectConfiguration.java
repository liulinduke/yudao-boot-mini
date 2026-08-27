package cn.iocoder.yudao.module.ai.framework.ai.config;

import jakarta.annotation.PostConstruct;
import org.springframework.context.annotation.Configuration;

/**
 * 国内 AI 服务使用直连。
 *
 * <p>当 JVM/HTTP 客户端配置了代理时，nonProxyHosts 会让这些域名绕过代理；
 * 未列出的海外服务仍按原代理配置执行。</p>
 */
@Configuration(proxyBeanMethods = false)
public class AiDirectConnectConfiguration {

    private static final String DOMESTIC_AI_NON_PROXY_HOSTS = String.join("|",
            "localhost", "127.*", "*.deepseek.com", "deepseek.com",
            "*.siliconflow.cn", "siliconflow.cn", "*.aliyuncs.com",
            "*.dashscope.aliyuncs.com", "*.volces.com", "*.volcengine.com",
            "*.hunyuan.cloud.tencent.com", "*.tencentcloudapi.com",
            "*.xfyun.cn", "*.xfyun.com", "*.baichuan-ai.com",
            "*.moonshot.cn", "*.minimaxi.com");

    @PostConstruct
    public void configureDomesticAiDirectConnect() {
        appendNonProxyHosts("http.nonProxyHosts");
        appendNonProxyHosts("https.nonProxyHosts");
    }

    private void appendNonProxyHosts(String propertyName) {
        String existing = System.getProperty(propertyName);
        if (existing == null || existing.isBlank()) {
            System.setProperty(propertyName, DOMESTIC_AI_NON_PROXY_HOSTS);
            return;
        }
        String value = existing;
        for (String host : DOMESTIC_AI_NON_PROXY_HOSTS.split("\\|")) {
            if (!containsHost(value, host)) {
                value += "|" + host;
            }
        }
        System.setProperty(propertyName, value);
    }

    private boolean containsHost(String value, String host) {
        for (String item : value.split("\\|")) {
            if (host.equalsIgnoreCase(item.trim())) {
                return true;
            }
        }
        return false;
    }
}
