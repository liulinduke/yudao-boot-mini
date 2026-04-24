package cn.iocoder.yudao.module.facebook.controller.admin.accountgroup;

import cn.iocoder.yudao.framework.common.pojo.CommonResult;
import cn.iocoder.yudao.framework.common.pojo.PageParam;
import cn.iocoder.yudao.framework.common.pojo.PageResult;
import cn.iocoder.yudao.module.facebook.controller.admin.accountgroup.vo.FbAccountGroupSaveReqVO;
import cn.iocoder.yudao.module.facebook.controller.admin.accountgroup.vo.FbAccountGroupRespVO;
import cn.iocoder.yudao.module.facebook.controller.admin.accountgroup.vo.FbAccountGroupPageReqVO;
import cn.iocoder.yudao.module.facebook.service.accountgroup.FbAccountGroupService;
import io.swagger.v3.oas.annotations.Operation;
import io.swagger.v3.oas.annotations.Parameter;
import io.swagger.v3.oas.annotations.tags.Tag;
import javax.annotation.Resource;
import javax.validation.Valid;
import org.springframework.security.access.prepost.PreAuthorize;
import org.springframework.validation.annotation.Validated;
import org.springframework.web.bind.annotation.*;

import java.util.List;

import static cn.iocoder.yudao.framework.common.pojo.CommonResult.success;

@Tag(name = "管理后台 - Facebook账号分组")
@RestController
@RequestMapping("/facebook/account-group")
@Validated
public class FbAccountGroupController {

    @Resource
    private FbAccountGroupService accountGroupService;

    @PostMapping("/create")
    @Operation(summary = "创建账号分组")
    @PreAuthorize("@ss.hasPermission('facebook:account-group:create')")
    public CommonResult<Long> createAccountGroup(@Valid @RequestBody FbAccountGroupSaveReqVO saveReqVO) {
        return success(accountGroupService.createAccountGroup(saveReqVO));
    }

    @PutMapping("/update")
    @Operation(summary = "更新账号分组")
    @PreAuthorize("@ss.hasPermission('facebook:account-group:update')")
    public CommonResult<Boolean> updateAccountGroup(@Valid @RequestBody FbAccountGroupSaveReqVO saveReqVO) {
        accountGroupService.updateAccountGroup(saveReqVO);
        return success(true);
    }

    @DeleteMapping("/delete")
    @Operation(summary = "删除账号分组")
    @Parameter(name = "id", description = "编号", required = true)
    @PreAuthorize("@ss.hasPermission('facebook:account-group:delete')")
    public CommonResult<Boolean> deleteAccountGroup(@RequestParam("id") Long id) {
        accountGroupService.deleteAccountGroup(id);
        return success(true);
    }

    @GetMapping("/get")
    @Operation(summary = "获得账号分组")
    @Parameter(name = "id", description = "编号", required = true, example = "1024")
    @PreAuthorize("@ss.hasPermission('facebook:account-group:query')")
    public CommonResult<FbAccountGroupRespVO> getAccountGroup(@RequestParam("id") Long id) {
        FbAccountGroupRespVO group = accountGroupService.getAccountGroup(id);
        return success(group);
    }

    @GetMapping("/page")
    @Operation(summary = "获得账号分组分页")
    @PreAuthorize("@ss.hasPermission('facebook:account-group:query')")
    public CommonResult<PageResult<FbAccountGroupRespVO>> getAccountGroupPage(@Valid FbAccountGroupPageReqVO pageReqVO) {
        PageResult<FbAccountGroupRespVO> page = accountGroupService.getAccountGroupPage(pageReqVO);
        return success(page);
    }

    @GetMapping("/all-enabled")
    @Operation(summary = "获得所有启用的账号分组")
    @PreAuthorize("@ss.hasPermission('facebook:account-group:query')")
    public CommonResult<List<FbAccountGroupRespVO>> getAllEnabledGroups() {
        List<FbAccountGroupRespVO> groups = accountGroupService.getAllEnabledGroups();
        return success(groups);
    }

}
