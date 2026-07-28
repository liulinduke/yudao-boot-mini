package cn.iocoder.yudao.module.facebook.controller.admin.account;

import org.springframework.web.bind.annotation.*;
import jakarta.annotation.Resource;
import org.springframework.validation.annotation.Validated;
import org.springframework.security.access.prepost.PreAuthorize;
import io.swagger.v3.oas.annotations.tags.Tag;
import io.swagger.v3.oas.annotations.Parameter;
import io.swagger.v3.oas.annotations.Operation;

import jakarta.validation.constraints.*;
import jakarta.validation.*;
import jakarta.servlet.http.*;
import java.util.*;
import java.io.IOException;

import cn.iocoder.yudao.framework.common.pojo.PageParam;
import cn.iocoder.yudao.framework.common.pojo.PageResult;
import cn.iocoder.yudao.framework.common.pojo.CommonResult;
import cn.iocoder.yudao.framework.common.util.object.BeanUtils;
import static cn.iocoder.yudao.framework.common.pojo.CommonResult.success;

import cn.iocoder.yudao.framework.excel.core.util.ExcelUtils;

import cn.iocoder.yudao.framework.apilog.core.annotation.ApiAccessLog;
import static cn.iocoder.yudao.framework.apilog.core.enums.OperateTypeEnum.*;

import cn.iocoder.yudao.module.facebook.controller.admin.account.vo.*;
import cn.iocoder.yudao.module.facebook.dal.dataobject.account.FbAccountDO;
import cn.iocoder.yudao.module.facebook.service.account.FbAccountService;

@Tag(name = "管理后台 - FB账号")
@RestController
@RequestMapping("/facebook/fb-account")
@Validated
public class FbAccountController {

    @Resource
    private FbAccountService fbAccountService;

    @PostMapping("/create")
    @Operation(summary = "创建FB账号")
    @PreAuthorize("@ss.hasPermission('facebook:fb-account:create')")
    public CommonResult<Long> createFbAccount(@Valid @RequestBody FbAccountSaveReqVO createReqVO) {
        return success(fbAccountService.createFbAccount(createReqVO));
    }

    @PutMapping("/update")
    @Operation(summary = "更新FB账号")
    @PreAuthorize("@ss.hasPermission('facebook:fb-account:update')")
    public CommonResult<Boolean> updateFbAccount(@Valid @RequestBody FbAccountSaveReqVO updateReqVO) {
        fbAccountService.updateFbAccount(updateReqVO);
        return success(true);
    }

    @DeleteMapping("/delete")
    @Operation(summary = "删除FB账号")
    @Parameter(name = "id", description = "编号", required = true)
    @PreAuthorize("@ss.hasPermission('facebook:fb-account:delete')")
    public CommonResult<Boolean> deleteFbAccount(@RequestParam("id") Long id) {
        fbAccountService.deleteFbAccount(id);
        return success(true);
    }

    @DeleteMapping("/delete-list")
    @Parameter(name = "ids", description = "编号", required = true)
    @Operation(summary = "批量删除FB账号")
    @PreAuthorize("@ss.hasPermission('facebook:fb-account:delete')")
    public CommonResult<Boolean> deleteFbAccountList(@RequestParam("ids") List<Long> ids) {
        fbAccountService.deleteFbAccountListByIds(ids);
        return success(true);
    }

    @GetMapping("/get")
    @Operation(summary = "获得FB账号")
    @Parameter(name = "id", description = "编号", required = true, example = "1024")
    @PreAuthorize("@ss.hasPermission('facebook:fb-account:query')")
    public CommonResult<FbAccountRespVO> getFbAccount(@RequestParam("id") Long id) {
        FbAccountDO fbAccount = fbAccountService.getFbAccount(id);
        return success(BeanUtils.toBean(fbAccount, FbAccountRespVO.class));
    }

    @GetMapping("/page")
    @Operation(summary = "获得FB账号分页")
    @PreAuthorize("@ss.hasPermission('facebook:fb-account:query')")
    public CommonResult<PageResult<FbAccountRespVO>> getFbAccountPage(@Valid FbAccountPageReqVO pageReqVO) {
        PageResult<FbAccountDO> pageResult = fbAccountService.getFbAccountPage(pageReqVO);
        return success(BeanUtils.toBean(pageResult, FbAccountRespVO.class));
    }

    @GetMapping("/export-excel")
    @Operation(summary = "导出FB账号 Excel")
    @PreAuthorize("@ss.hasPermission('facebook:fb-account:export')")
    @ApiAccessLog(operateType = EXPORT)
    public void exportFbAccountExcel(@Valid FbAccountPageReqVO pageReqVO,
              HttpServletResponse response) throws IOException {
        pageReqVO.setPageSize(PageParam.PAGE_SIZE_NONE);
        List<FbAccountDO> list = fbAccountService.getFbAccountPage(pageReqVO).getList();
        ExcelUtils.write(response, "FB账号.xls", "数据", FbAccountRespVO.class,
                        BeanUtils.toBean(list, FbAccountRespVO.class));
    }

    @PutMapping("/update-language")
    @Operation(summary = "更新FB账号语言设置")
    @PreAuthorize("@ss.hasPermission('facebook:fb-account:update')")
    public CommonResult<Boolean> updateFbAccountLanguage(
            @RequestParam("id") Long id,
            @RequestParam("languageCode") String languageCode) {
        fbAccountService.updateFbAccountLanguage(id, languageCode);
        return success(true);
    }

    @PutMapping("/update-proxy")
    @Operation(summary = "批量更新FB账号代理")
    @PreAuthorize("@ss.hasPermission('facebook:fb-account:update')")
    public CommonResult<Boolean> updateFbAccountProxy(
            @Valid @RequestBody FbAccountUpdateProxyReqVO reqVO) {
        fbAccountService.updateFbAccountProxy(reqVO.getIds(), reqVO.getProxyId());
        return success(true);
    }

    @PutMapping("/update-group")
    @Operation(summary = "批量更新FB账号分组")
    @PreAuthorize("@ss.hasPermission('facebook:fb-account:update')")
    public CommonResult<Boolean> updateFbAccountGroup(
            @Valid @RequestBody FbAccountUpdateGroupReqVO reqVO) {
        fbAccountService.updateFbAccountGroup(reqVO.getIds(), reqVO.getGroupId());
        return success(true);
    }

    @PutMapping("/update-status")
    @Operation(summary = "批量更新 FB 账号启用状态")
    @PreAuthorize("@ss.hasPermission('facebook:fb-account:update')")
    public CommonResult<Boolean> updateFbAccountStatus(
            @Valid @RequestBody FbAccountBatchStatusReqVO reqVO) {
        fbAccountService.updateFbAccountStatus(reqVO.getIds(), reqVO.getStatus());
        return success(true);
    }

    @PostMapping("/import")
    @Operation(summary = "导入FB账号")
    @PreAuthorize("@ss.hasPermission('facebook:fb-account:create')")
    public CommonResult<Boolean> importFbAccount(@Valid @RequestBody FbAccountImportReqVO reqVO) {
        fbAccountService.importFbAccount(reqVO);
        return success(true);
    }

    @PostMapping("/import-cookie")
    @Operation(summary = "导入FB账号Cookie")
    @PreAuthorize("@ss.hasPermission('facebook:fb-account:create')")
    public CommonResult<Boolean> importFbAccountCookie(@Valid @RequestBody FbAccountCookieImportReqVO reqVO) {
        fbAccountService.importFbAccountCookie(reqVO);
        return success(true);
    }

    @PutMapping("/update-login-result")
    @Operation(summary = "更新FB账号登录结果")
    public CommonResult<Boolean> updateFbAccountLoginResult(@Valid @RequestBody FbAccountLoginResultUpdateReqVO reqVO) {
        fbAccountService.updateFbAccountLoginResult(reqVO);
        return success(true);
    }

    @PostMapping("/profile/upload")
    @Operation(summary = "保存Facebook账号资料上传任务")
    @PreAuthorize("@ss.hasPermission('facebook:fb-account:update')")
    public CommonResult<Boolean> uploadProfile(@Valid @RequestBody FbAccountProfileUploadReqVO reqVO) {
        fbAccountService.saveProfileUpload(reqVO);
        return success(true);
    }

    @PostMapping("/profile/report")
    @Operation(summary = "回报Facebook账号资料上传结果")
    @PreAuthorize("@ss.hasPermission('facebook:fb-account:update')")
    public CommonResult<Boolean> reportProfile(@Valid @RequestBody FbAccountProfileReportReqVO reqVO) {
        fbAccountService.reportProfileUpload(reqVO);
        return success(true);
    }

}
