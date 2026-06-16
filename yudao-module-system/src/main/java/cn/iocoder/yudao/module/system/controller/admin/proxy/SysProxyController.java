
package cn.iocoder.yudao.module.system.controller.admin.proxy;

import cn.iocoder.yudao.framework.common.pojo.CommonResult;
import cn.iocoder.yudao.framework.common.pojo.PageResult;
import cn.iocoder.yudao.framework.excel.core.util.ExcelUtils;
import cn.iocoder.yudao.module.system.controller.admin.proxy.vo.SysProxyCreateReqVO;
import cn.iocoder.yudao.module.system.controller.admin.proxy.vo.SysProxyPageReqVO;
import cn.iocoder.yudao.module.system.controller.admin.proxy.vo.SysProxyRespVO;
import cn.iocoder.yudao.module.system.controller.admin.proxy.vo.SysProxyUpdateReqVO;
import cn.iocoder.yudao.module.system.service.SysProxyService;
import io.swagger.v3.oas.annotations.Operation;
import io.swagger.v3.oas.annotations.Parameter;
import io.swagger.v3.oas.annotations.tags.Tag;
import javax.annotation.Resource;
import javax.validation.Valid;
import org.springframework.security.access.prepost.PreAuthorize;
import org.springframework.web.bind.annotation.*;

import java.util.List;

import static cn.iocoder.yudao.framework.common.pojo.CommonResult.success;

@Tag(name = "管理后台 - 代理管理")
@RestController
@RequestMapping("/system/proxy")
public class SysProxyController {

    @Resource
    private SysProxyService proxyService;

    @PostMapping("/create")
    @Operation(summary = "创建代理")
    @PreAuthorize("@ss.hasPermission('system:proxy:create')")
    public CommonResult<Long> createProxy(@Valid @RequestBody SysProxyCreateReqVO createReqVO) {
        Long id = proxyService.createProxy(createReqVO);
        return success(id);
    }

    @PutMapping("/update")
    @Operation(summary = "更新代理")
    @PreAuthorize("@ss.hasPermission('system:proxy:update')")
    public CommonResult<Boolean> updateProxy(@Valid @RequestBody SysProxyUpdateReqVO updateReqVO) {
        proxyService.updateProxy(updateReqVO);
        return success(true);
    }

    @DeleteMapping("/delete")
    @Operation(summary = "删除代理")
    @PreAuthorize("@ss.hasPermission('system:proxy:delete')")
    public CommonResult<Boolean> deleteProxy(@Parameter(description = "代理ID", required = true) @RequestParam("id") Long id) {
        proxyService.deleteProxy(id);
        return success(true);
    }

    @GetMapping("/get")
    @Operation(summary = "获取代理详情")
    @PreAuthorize("@ss.hasPermission('system:proxy:query')")
    public CommonResult<SysProxyRespVO> getProxy(@Parameter(description = "代理ID", required = true) @RequestParam("id") Long id) {
        return success(proxyService.getProxy(id));
    }

    @GetMapping("/page")
    @Operation(summary = "分页查询代理")
    @PreAuthorize("@ss.hasPermission('system:proxy:query')")
    public CommonResult<PageResult<SysProxyRespVO>> getProxyPage(@Valid SysProxyPageReqVO pageReqVO) {
        return success(proxyService.getProxyPage(pageReqVO));
    }

    @GetMapping("/list")
    @Operation(summary = "获取所有启用的代理列表")
    public CommonResult<List<SysProxyRespVO>> getAllEnabledProxyList() {
        return success(proxyService.getAllEnabledProxyList());
    }

}
