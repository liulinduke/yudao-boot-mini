package cn.iocoder.yudao.module.facebook.service.globalconfig;

import cn.iocoder.yudao.module.facebook.controller.admin.globalconfig.vo.FbGlobalConfigSaveReqVO;

import java.util.List;
import java.util.Map;

/**
 * Facebook 全局配置 Service 接口
 *
 * @author 芋道源码
 */
public interface FbGlobalConfigService {

    /**
     * 保存配置
     *
     * @param saveReqVO 配置信息
     * @return 配置ID
     */
    Long saveConfig(FbGlobalConfigSaveReqVO saveReqVO);

    /**
     * 批量保存配置
     *
     * @param configs 配置列表
     */
    void batchSaveConfigs(List<FbGlobalConfigSaveReqVO> configs);

    /**
     * 获取所有配置
     *
     * @return 配置列表
     */
    List<Map<String, String>> getAllConfigs();

    /**
     * 根据配置键获取配置值
     *
     * @param configKey 配置键
     * @return 配置值
     */
    String getConfigValue(String configKey);

    /**
     * 更新配置
     *
     * @param configKey   配置键
     * @param configValue 配置值
     */
    void updateConfig(String configKey, String configValue);

}
