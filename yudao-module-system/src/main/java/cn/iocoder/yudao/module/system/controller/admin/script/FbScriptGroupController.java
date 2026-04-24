package cn.iocoder.yudao.module.system.controller.admin.script;

import cn.iocoder.yudao.framework.apilog.core.annotation.ApiAccessLog;
import cn.iocoder.yudao.framework.common.pojo.CommonResult;
import cn.iocoder.yudao.framework.common.pojo.PageParam;
import cn.iocoder.yudao.framework.common.pojo.PageResult;
import cn.iocoder.yudao.framework.common.util.object.BeanUtils;
import cn.iocoder.yudao.framework.excel.core.util.ExcelUtils;
import cn.iocoder.yudao.module.system.controller.admin.script.vo.*;
import cn.iocoder.yudao.module.system.dal.dataobject.script.FbScriptGroupDO;
import cn.iocoder.yudao.module.system.service.script.FbScriptGroupService;
import io.swagger.v3.oas.annotations.Operation;
import io.swagger.v3.oas.annotations.Parameter;
import io.swagger.v3.oas.annotations.tags.Tag;
import org.springframework.security.access.prepost.PreAuthorize;
import org.springframework.validation.annotation.Validated;
import org.springframework.web.bind.annotation.*;

import javax.annotation.Resource;
import javax.servlet.http.HttpServletResponse;
import javax.validation.Valid;
import java.io.IOException;
import java.util.List;

import static cn.iocoder.yudao.framework.apilog.core.enums.OperateTypeEnum.EXPORT;
import static cn.iocoder.yudao.framework.common.pojo.CommonResult.success;

@Tag(name = "管理后台 - 话术分组")
@RestController
@RequestMapping("/social-matrix/script-group")
@Validated
public class FbScriptGroupController {

    @Resource
    private FbScriptGroupService scriptGroupService;

    @PostMapping("/create")
    @Operation(summary = "创建话术分组")
    @PreAuthorize("@ss.hasPermission('social-matrix:script-group:create')")
    public CommonResult<Long> createScriptGroup(@Valid @RequestBody FbScriptGroupSaveReqVO createReqVO) {
        return success(scriptGroupService.createScriptGroup(createReqVO));
    }

    @PutMapping("/update")
    @Operation(summary = "更新话术分组")
    @PreAuthorize("@ss.hasPermission('social-matrix:script-group:update')")
    public CommonResult<Boolean> updateScriptGroup(@Valid @RequestBody FbScriptGroupSaveReqVO updateReqVO) {
        scriptGroupService.updateScriptGroup(updateReqVO);
        return success(true);
    }

    @DeleteMapping("/delete")
    @Operation(summary = "删除话术分组")
    @Parameter(name = "id", description = "编号", required = true)
    @PreAuthorize("@ss.hasPermission('social-matrix:script-group:delete')")
    public CommonResult<Boolean> deleteScriptGroup(@RequestParam("id") Long id) {
        scriptGroupService.deleteScriptGroup(id);
        return success(true);
    }

    @DeleteMapping("/delete-list")
    @Parameter(name = "ids", description = "编号", required = true)
    @Operation(summary = "批量删除话术分组")
    @PreAuthorize("@ss.hasPermission('social-matrix:script-group:delete')")
    public CommonResult<Boolean> deleteScriptGroupList(@RequestParam("ids") List<Long> ids) {
        scriptGroupService.deleteScriptGroupListByIds(ids);
        return success(true);
    }

    @GetMapping("/get")
    @Operation(summary = "获得话术分组")
    @Parameter(name = "id", description = "编号", required = true, example = "1024")
    @PreAuthorize("@ss.hasPermission('social-matrix:script-group:query')")
    public CommonResult<FbScriptGroupRespVO> getScriptGroup(@RequestParam("id") Long id) {
        FbScriptGroupDO scriptGroup = scriptGroupService.getScriptGroup(id);
        return success(BeanUtils.toBean(scriptGroup, FbScriptGroupRespVO.class));
    }

    @GetMapping("/page")
    @Operation(summary = "获得话术分组分页")
    @PreAuthorize("@ss.hasPermission('social-matrix:script-group:query')")
    public CommonResult<PageResult<FbScriptGroupRespVO>> getScriptGroupPage(@Valid FbScriptGroupPageReqVO pageReqVO) {
        PageResult<FbScriptGroupDO> pageResult = scriptGroupService.getScriptGroupPage(pageReqVO);
        return success(BeanUtils.toBean(pageResult, FbScriptGroupRespVO.class));
    }

    @GetMapping("/list")
    @Operation(summary = "获得话术分组列表")
    @PreAuthorize("@ss.hasPermission('social-matrix:script-group:query')")
    public CommonResult<List<FbScriptGroupRespVO>> getScriptGroupList() {
        List<FbScriptGroupDO> list = scriptGroupService.getScriptGroupList();
        return success(BeanUtils.toBean(list, FbScriptGroupRespVO.class));
    }

    @GetMapping("/export-excel")
    @Operation(summary = "导出话术分组 Excel")
    @PreAuthorize("@ss.hasPermission('social-matrix:script-group:export')")
    @ApiAccessLog(operateType = EXPORT)
    public void exportScriptGroupExcel(@Valid FbScriptGroupPageReqVO pageReqVO,
              HttpServletResponse response) throws IOException {
        pageReqVO.setPageSize(PageParam.PAGE_SIZE_NONE);
        List<FbScriptGroupDO> list = scriptGroupService.getScriptGroupPage(pageReqVO).getList();
        ExcelUtils.write(response, "话术分组.xls", "数据", FbScriptGroupRespVO.class,
                        BeanUtils.toBean(list, FbScriptGroupRespVO.class));
    }

}
