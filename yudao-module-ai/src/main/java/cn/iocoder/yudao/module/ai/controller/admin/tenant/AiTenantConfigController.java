package cn.iocoder.yudao.module.ai.controller.admin.tenant;

import cn.iocoder.yudao.framework.common.pojo.CommonResult;
import cn.iocoder.yudao.module.ai.service.tenant.AiTenantConfigService;
import io.swagger.v3.oas.annotations.Operation;
import io.swagger.v3.oas.annotations.Parameter;
import io.swagger.v3.oas.annotations.tags.Tag;
import jakarta.annotation.Resource;
import jakarta.validation.constraints.NotNull;
import org.springframework.security.access.prepost.PreAuthorize;
import org.springframework.validation.annotation.Validated;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RequestParam;
import org.springframework.web.bind.annotation.RestController;

import static cn.iocoder.yudao.framework.common.pojo.CommonResult.success;

@Tag(name = "管理后台 - AI 租户配置")
@RestController
@RequestMapping("/ai/tenant-config")
@Validated
public class AiTenantConfigController {

    @Resource
    private AiTenantConfigService tenantConfigService;

    @PostMapping("/init")
    @Operation(summary = "初始化租户 AI 配置")
    @Parameter(name = "tenantId", description = "目标租户编号", required = true, example = "122")
    @PreAuthorize("@ss.hasPermission('system:tenant:update')")
    public CommonResult<Boolean> initializeTenantConfig(@RequestParam("tenantId") @NotNull Long tenantId) {
        tenantConfigService.initializeTenantConfig(tenantId);
        return success(true);
    }

}
