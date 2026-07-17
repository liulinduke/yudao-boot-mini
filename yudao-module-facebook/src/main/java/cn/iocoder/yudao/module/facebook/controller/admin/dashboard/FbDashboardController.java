package cn.iocoder.yudao.module.facebook.controller.admin.dashboard;

import cn.iocoder.yudao.framework.common.pojo.CommonResult;
import cn.iocoder.yudao.module.facebook.controller.admin.dashboard.vo.FbDashboardHomeRespVO;
import cn.iocoder.yudao.module.facebook.service.dashboard.FbDashboardService;
import io.swagger.v3.oas.annotations.Operation;
import io.swagger.v3.oas.annotations.tags.Tag;
import jakarta.annotation.Resource;
import org.springframework.security.access.prepost.PreAuthorize;
import org.springframework.validation.annotation.Validated;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

import static cn.iocoder.yudao.framework.common.pojo.CommonResult.success;

@Tag(name = "管理后台 - Facebook AI 获客首页")
@RestController
@RequestMapping("/facebook/dashboard")
@Validated
public class FbDashboardController {

    @Resource
    private FbDashboardService dashboardService;

    @GetMapping("/home")
    @Operation(summary = "获得 AI 获客首页数据")
    @PreAuthorize("@ss.hasAnyPermissions('facebook:operation-task:query','facebook:fb-collect-user:query','facebook:message:query')")
    public CommonResult<FbDashboardHomeRespVO> getHome() {
        return success(dashboardService.getHome());
    }
}
