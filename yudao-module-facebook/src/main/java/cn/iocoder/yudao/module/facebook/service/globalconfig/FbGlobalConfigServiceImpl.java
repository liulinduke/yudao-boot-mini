package cn.iocoder.yudao.module.facebook.service.globalconfig;

import cn.hutool.core.bean.BeanUtil;
import cn.iocoder.yudao.framework.common.util.object.BeanUtils;
import cn.iocoder.yudao.module.facebook.controller.admin.globalconfig.vo.FbGlobalConfigSaveReqVO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.globalconfig.FbGlobalConfigDO;
import cn.iocoder.yudao.module.facebook.dal.mysql.globalconfig.FbGlobalConfigMapper;
import javax.annotation.Resource;
import lombok.extern.slf4j.Slf4j;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;
import org.springframework.validation.annotation.Validated;

import java.util.*;
import java.util.stream.Collectors;

import static cn.iocoder.yudao.framework.common.exception.util.ServiceExceptionUtil.exception;
import static cn.iocoder.yudao.module.facebook.enums.ErrorCodeConstants.GLOBAL_CONFIG_NOT_EXISTS;

/**
 * Facebook 全局配置 Service 实现类
 *
 * @author 芋道源码
 */
@Slf4j
@Service
@Validated
public class FbGlobalConfigServiceImpl implements FbGlobalConfigService {

    @Resource
    private FbGlobalConfigMapper globalConfigMapper;

    @Override
    @Transactional(rollbackFor = Exception.class)
    public Long saveConfig(FbGlobalConfigSaveReqVO saveReqVO) {
        // 检查配置是否已存在
        FbGlobalConfigDO existingConfig = globalConfigMapper.selectOne("config_key", saveReqVO.getConfigKey());
        
        if (existingConfig != null) {
            // 更新
            existingConfig.setConfigValue(saveReqVO.getConfigValue());
            existingConfig.setDescription(saveReqVO.getDescription());
            globalConfigMapper.updateById(existingConfig);
            log.info("更新全局配置: {} = {}", saveReqVO.getConfigKey(), saveReqVO.getConfigValue());
            return existingConfig.getId();
        } else {
            // 新增
            FbGlobalConfigDO config = BeanUtils.toBean(saveReqVO, FbGlobalConfigDO.class);
            globalConfigMapper.insert(config);
            log.info("新增全局配置: {} = {}", saveReqVO.getConfigKey(), saveReqVO.getConfigValue());
            return config.getId();
        }
    }

    @Override
    @Transactional(rollbackFor = Exception.class)
    public void batchSaveConfigs(List<FbGlobalConfigSaveReqVO> configs) {
        for (FbGlobalConfigSaveReqVO config : configs) {
            saveConfig(config);
        }
    }

    @Override
    public List<Map<String, String>> getAllConfigs() {
        List<FbGlobalConfigDO> configs = globalConfigMapper.selectList();
        return configs.stream().map(config -> {
            Map<String, String> map = new HashMap<>();
            map.put("configKey", config.getConfigKey());
            map.put("configValue", config.getConfigValue());
            map.put("description", config.getDescription());
            return map;
        }).collect(Collectors.toList());
    }

    @Override
    public String getConfigValue(String configKey) {
        FbGlobalConfigDO config = globalConfigMapper.selectOne("config_key", configKey);
        return config != null ? config.getConfigValue() : null;
    }

    @Override
    @Transactional(rollbackFor = Exception.class)
    public void updateConfig(String configKey, String configValue) {
        FbGlobalConfigDO config = globalConfigMapper.selectOne("config_key", configKey);
        if (config == null) {
            throw exception(GLOBAL_CONFIG_NOT_EXISTS);
        }
        config.setConfigValue(configValue);
        globalConfigMapper.updateById(config);
        log.info("更新全局配置: {} = {}", configKey, configValue);
    }

}
