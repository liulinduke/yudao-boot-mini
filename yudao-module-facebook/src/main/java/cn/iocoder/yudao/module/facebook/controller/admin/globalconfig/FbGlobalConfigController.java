package cn.iocoder.yudao.module.facebook.controller.admin.globalconfig;

import cn.iocoder.yudao.framework.common.pojo.CommonResult;
import cn.iocoder.yudao.module.facebook.controller.admin.globalconfig.vo.FbGlobalConfigSaveReqVO;
import cn.iocoder.yudao.module.facebook.service.globalconfig.FbGlobalConfigService;
import io.swagger.v3.oas.annotations.Operation;
import io.swagger.v3.oas.annotations.tags.Tag;
import javax.annotation.Resource;
import org.springframework.security.access.prepost.PreAuthorize;
import org.springframework.validation.annotation.Validated;
import org.springframework.web.bind.annotation.*;

import java.util.List;
import java.util.Map;

import static cn.iocoder.yudao.framework.common.pojo.CommonResult.success;

@Tag(name = "管理后台 - Facebook全局配置")
@RestController
@RequestMapping("/facebook/global-config")
@Validated
public class FbGlobalConfigController {

    @Resource
    private FbGlobalConfigService globalConfigService;

    @PostMapping("/save")
    @Operation(summary = "保存配置")
    @PreAuthorize("@ss.hasPermission('facebook:global-config:update')")
    public CommonResult<Long> saveConfig(@RequestBody @Validated FbGlobalConfigSaveReqVO saveReqVO) {
        return success(globalConfigService.saveConfig(saveReqVO));
    }

    @PostMapping("/batch-save")
    @Operation(summary = "批量保存配置")
    @PreAuthorize("@ss.hasPermission('facebook:global-config:update')")
    public CommonResult<Boolean> batchSaveConfigs(@RequestBody @Validated List<FbGlobalConfigSaveReqVO> configs) {
        globalConfigService.batchSaveConfigs(configs);
        return success(true);
    }

    @GetMapping("/all")
    @Operation(summary = "获取所有配置")
    @PreAuthorize("@ss.hasPermission('facebook:global-config:query')")
    public CommonResult<List<Map<String, String>>> getAllConfigs() {
        return success(globalConfigService.getAllConfigs());
    }

    @GetMapping("/value")
    @Operation(summary = "获取配置值")
    @PreAuthorize("@ss.hasPermission('facebook:global-config:query')")
    public CommonResult<String> getConfigValue(@RequestParam String configKey) {
        return success(globalConfigService.getConfigValue(configKey));
    }

}
