package cn.iocoder.yudao.module.facebook.controller.admin.warmup;

import cn.iocoder.yudao.framework.common.pojo.CommonResult;
import cn.iocoder.yudao.framework.common.pojo.PageResult;
import cn.iocoder.yudao.module.facebook.controller.admin.warmup.vo.*;
import cn.iocoder.yudao.module.facebook.service.warmup.FbWarmupTaskService;
import io.swagger.v3.oas.annotations.Operation;
import io.swagger.v3.oas.annotations.tags.Tag;
import jakarta.annotation.Resource;
import jakarta.validation.Valid;
import org.springframework.security.access.prepost.PreAuthorize;
import org.springframework.validation.annotation.Validated;
import org.springframework.web.bind.annotation.*;

import java.util.List;

import static cn.iocoder.yudao.framework.common.pojo.CommonResult.success;

@Tag(name = "管理后台 - Facebook养号任务")
@RestController
@RequestMapping("/facebook/warmup-task")
@Validated
public class FbWarmupTaskController {
    @Resource private FbWarmupTaskService service;

    @PostMapping("/create")
    @PreAuthorize("@ss.hasPermission('facebook:fb-account:create')")
    public CommonResult<Long> create(@Valid @RequestBody FbWarmupTaskSaveReqVO req) { return success(service.create(req, false)); }

    @PostMapping("/execute-now")
    @PreAuthorize("@ss.hasPermission('facebook:fb-account:update')")
    public CommonResult<Long> executeNow(@Valid @RequestBody FbWarmupTaskSaveReqVO req) { return success(service.create(req, true)); }

    @GetMapping("/page")
    @PreAuthorize("@ss.hasPermission('facebook:fb-account:query')")
    public CommonResult<PageResult<FbWarmupTaskRespVO>> page(@Valid FbWarmupTaskPageReqVO req) { return success(service.page(req)); }

    @DeleteMapping("/delete")
    @PreAuthorize("@ss.hasPermission('facebook:fb-account:delete')")
    public CommonResult<Boolean> delete(@RequestParam("id") Long id) { service.delete(id); return success(true); }

    @GetMapping("/claim-pending")
    @PreAuthorize("@ss.hasPermission('facebook:fb-account:query')")
    public CommonResult<List<FbWarmupPendingDetailRespVO>> claimPending(@RequestParam(value = "limit", defaultValue = "10") Integer limit) {
        return success(service.claimPending(limit));
    }

    @PostMapping("/report-detail")
    @PreAuthorize("@ss.hasPermission('facebook:fb-account:update')")
    public CommonResult<Boolean> reportDetail(@RequestParam("detailId") Long detailId,
                                              @RequestParam("success") Boolean ok,
                                              @RequestParam(value = "errorMessage", required = false) String errorMessage) {
        service.reportDetail(detailId, ok, errorMessage);
        return success(true);
    }
}
