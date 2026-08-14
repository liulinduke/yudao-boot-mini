package cn.iocoder.yudao.module.ai.service.tenant;

/**
 * AI 租户配置初始化 Service
 */
public interface AiTenantConfigService {

    /**
     * 使用模板租户的 AI 配置补齐目标租户。
     *
     * @param tenantId 目标租户编号
     */
    void initializeTenantConfig(Long tenantId);

}
