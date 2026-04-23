package cn.iocoder.yudao.module.facebook.controller.admin.fbcollectgroup;

import org.springframework.web.bind.annotation.*;
import javax.annotation.Resource;
import org.springframework.validation.annotation.Validated;
import org.springframework.security.access.prepost.PreAuthorize;
import io.swagger.v3.oas.annotations.tags.Tag;
import io.swagger.v3.oas.annotations.Parameter;
import io.swagger.v3.oas.annotations.Operation;

import javax.validation.constraints.*;
import javax.validation.*;
import javax.servlet.http.*;
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

import cn.iocoder.yudao.module.facebook.controller.admin.fbcollectgroup.vo.*;
import cn.iocoder.yudao.module.facebook.dal.dataobject.fbcollectgroup.FbCollectGroupDO;
import cn.iocoder.yudao.module.facebook.service.fbcollectgroup.FbCollectGroupService;

@Tag(name = "管理后台 - FB群组采集结果")
@RestController
@RequestMapping("/facebook/fb-collect-group")
@Validated
public class FbCollectGroupController {

    @Resource
    private FbCollectGroupService fbCollectGroupService;

    @PostMapping("/create")
    @Operation(summary = "创建FB群组采集结果")
    @PreAuthorize("@ss.hasPermission('facebook:fb-collect-group:create')")
    public CommonResult<Long> createFbCollectGroup(@Valid @RequestBody FbCollectGroupSaveReqVO createReqVO) {
        return success(fbCollectGroupService.createFbCollectGroup(createReqVO));
    }

    @PutMapping("/update")
    @Operation(summary = "更新FB群组采集结果")
    @PreAuthorize("@ss.hasPermission('facebook:fb-collect-group:update')")
    public CommonResult<Boolean> updateFbCollectGroup(@Valid @RequestBody FbCollectGroupSaveReqVO updateReqVO) {
        fbCollectGroupService.updateFbCollectGroup(updateReqVO);
        return success(true);
    }

    @DeleteMapping("/delete")
    @Operation(summary = "删除FB群组采集结果")
    @Parameter(name = "id", description = "编号", required = true)
    @PreAuthorize("@ss.hasPermission('facebook:fb-collect-group:delete')")
    public CommonResult<Boolean> deleteFbCollectGroup(@RequestParam("id") Long id) {
        fbCollectGroupService.deleteFbCollectGroup(id);
        return success(true);
    }

    @DeleteMapping("/delete-list")
    @Parameter(name = "ids", description = "编号", required = true)
    @Operation(summary = "批量删除FB群组采集结果")
                @PreAuthorize("@ss.hasPermission('facebook:fb-collect-group:delete')")
    public CommonResult<Boolean> deleteFbCollectGroupList(@RequestParam("ids") List<Long> ids) {
        fbCollectGroupService.deleteFbCollectGroupListByIds(ids);
        return success(true);
    }

    @GetMapping("/get")
    @Operation(summary = "获得FB群组采集结果")
    @Parameter(name = "id", description = "编号", required = true, example = "1024")
    @PreAuthorize("@ss.hasPermission('facebook:fb-collect-group:query')")
    public CommonResult<FbCollectGroupRespVO> getFbCollectGroup(@RequestParam("id") Long id) {
        FbCollectGroupDO fbCollectGroup = fbCollectGroupService.getFbCollectGroup(id);
        return success(BeanUtils.toBean(fbCollectGroup, FbCollectGroupRespVO.class));
    }

    @GetMapping("/page")
    @Operation(summary = "获得FB群组采集结果分页")
    @PreAuthorize("@ss.hasPermission('facebook:fb-collect-group:query')")
    public CommonResult<PageResult<FbCollectGroupRespVO>> getFbCollectGroupPage(@Valid FbCollectGroupPageReqVO pageReqVO) {
        PageResult<FbCollectGroupDO> pageResult = fbCollectGroupService.getFbCollectGroupPage(pageReqVO);
        return success(BeanUtils.toBean(pageResult, FbCollectGroupRespVO.class));
    }

    @GetMapping("/export-excel")
    @Operation(summary = "导出FB群组采集结果 Excel")
    @PreAuthorize("@ss.hasPermission('facebook:fb-collect-group:export')")
    @ApiAccessLog(operateType = EXPORT)
    public void exportFbCollectGroupExcel(@Valid FbCollectGroupPageReqVO pageReqVO,
              HttpServletResponse response) throws IOException {
        pageReqVO.setPageSize(PageParam.PAGE_SIZE_NONE);
        List<FbCollectGroupDO> list = fbCollectGroupService.getFbCollectGroupPage(pageReqVO).getList();
        // 导出 Excel
        ExcelUtils.write(response, "FB群组采集结果.xls", "数据", FbCollectGroupRespVO.class,
                        BeanUtils.toBean(list, FbCollectGroupRespVO.class));
    }

}