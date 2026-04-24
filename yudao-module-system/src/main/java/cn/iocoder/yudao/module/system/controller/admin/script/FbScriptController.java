package cn.iocoder.yudao.module.system.controller.admin.script;

import cn.iocoder.yudao.framework.apilog.core.annotation.ApiAccessLog;
import cn.iocoder.yudao.framework.common.pojo.CommonResult;
import cn.iocoder.yudao.framework.common.pojo.PageParam;
import cn.iocoder.yudao.framework.common.pojo.PageResult;
import cn.iocoder.yudao.framework.common.util.object.BeanUtils;
import cn.iocoder.yudao.framework.excel.core.util.ExcelUtils;
import cn.iocoder.yudao.module.system.controller.admin.script.vo.*;
import cn.iocoder.yudao.module.system.dal.dataobject.script.FbScriptDO;
import cn.iocoder.yudao.module.system.service.script.FbScriptService;
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

@Tag(name = "管理后台 - 话术内容")
@RestController
@RequestMapping("/social-matrix/script")
@Validated
public class FbScriptController {

    @Resource
    private FbScriptService scriptService;

    @PostMapping("/create")
    @Operation(summary = "创建话术内容")
    @PreAuthorize("@ss.hasPermission('social-matrix:script:create')")
    public CommonResult<Long> createScript(@Valid @RequestBody FbScriptSaveReqVO createReqVO) {
        return success(scriptService.createScript(createReqVO));
    }

    @PutMapping("/update")
    @Operation(summary = "更新话术内容")
    @PreAuthorize("@ss.hasPermission('social-matrix:script:update')")
    public CommonResult<Boolean> updateScript(@Valid @RequestBody FbScriptSaveReqVO updateReqVO) {
        scriptService.updateScript(updateReqVO);
        return success(true);
    }

    @DeleteMapping("/delete")
    @Operation(summary = "删除话术内容")
    @Parameter(name = "id", description = "编号", required = true)
    @PreAuthorize("@ss.hasPermission('social-matrix:script:delete')")
    public CommonResult<Boolean> deleteScript(@RequestParam("id") Long id) {
        scriptService.deleteScript(id);
        return success(true);
    }

    @DeleteMapping("/delete-list")
    @Parameter(name = "ids", description = "编号", required = true)
    @Operation(summary = "批量删除话术内容")
    @PreAuthorize("@ss.hasPermission('social-matrix:script:delete')")
    public CommonResult<Boolean> deleteScriptList(@RequestParam("ids") List<Long> ids) {
        scriptService.deleteScriptListByIds(ids);
        return success(true);
    }

    @GetMapping("/get")
    @Operation(summary = "获得话术内容")
    @Parameter(name = "id", description = "编号", required = true, example = "1024")
    @PreAuthorize("@ss.hasPermission('social-matrix:script:query')")
    public CommonResult<FbScriptRespVO> getScript(@RequestParam("id") Long id) {
        FbScriptDO script = scriptService.getScript(id);
        return success(BeanUtils.toBean(script, FbScriptRespVO.class));
    }

    @GetMapping("/page")
    @Operation(summary = "获得话术内容分页")
    @PreAuthorize("@ss.hasPermission('social-matrix:script:query')")
    public CommonResult<PageResult<FbScriptRespVO>> getScriptPage(@Valid FbScriptPageReqVO pageReqVO) {
        PageResult<FbScriptDO> pageResult = scriptService.getScriptPage(pageReqVO);
        return success(BeanUtils.toBean(pageResult, FbScriptRespVO.class));
    }

    @GetMapping("/list-by-group")
    @Operation(summary = "根据分组获得话术列表")
    @Parameter(name = "groupId", description = "分组ID", required = true)
    @PreAuthorize("@ss.hasPermission('social-matrix:script:query')")
    public CommonResult<List<FbScriptRespVO>> getScriptListByGroup(@RequestParam("groupId") Long groupId) {
        List<FbScriptDO> list = scriptService.getScriptListByGroupId(groupId);
        return success(BeanUtils.toBean(list, FbScriptRespVO.class));
    }

    @GetMapping("/export-excel")
    @Operation(summary = "导出话术内容 Excel")
    @PreAuthorize("@ss.hasPermission('social-matrix:script:export')")
    @ApiAccessLog(operateType = EXPORT)
    public void exportScriptExcel(@Valid FbScriptPageReqVO pageReqVO,
              HttpServletResponse response) throws IOException {
        pageReqVO.setPageSize(PageParam.PAGE_SIZE_NONE);
        List<FbScriptDO> list = scriptService.getScriptPage(pageReqVO).getList();
        ExcelUtils.write(response, "话术内容.xls", "数据", FbScriptRespVO.class,
                        BeanUtils.toBean(list, FbScriptRespVO.class));
    }

}
