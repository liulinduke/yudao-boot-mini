package cn.iocoder.yudao.module.facebook.controller.admin.collect;

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

import cn.iocoder.yudao.module.facebook.controller.admin.collect.vo.*;
import cn.iocoder.yudao.module.facebook.dal.dataobject.collect.FbCollectDO;
import cn.iocoder.yudao.module.facebook.service.collect.FbCollectService;

@Tag(name = "管理后台 - FB采集任务")
@RestController
@RequestMapping("/facebook/fb-collect")
@Validated
public class FbCollectController {

    @Resource
    private FbCollectService fbCollectService;

    @PostMapping("/create")
    @Operation(summary = "创建FB采集任务")
    @PreAuthorize("@ss.hasPermission('facebook:fb-collect:create')")
    public CommonResult<FbCollectCreateRespVO> createFbCollect(@Valid @RequestBody FbCollectSaveReqVO createReqVO) {
        return success(fbCollectService.createFbCollect(createReqVO));
    }

    @PutMapping("/update")
    @Operation(summary = "更新FB采集任务")
    @PreAuthorize("@ss.hasPermission('facebook:fb-collect:update')")
    public CommonResult<Boolean> updateFbCollect(@Valid @RequestBody FbCollectSaveReqVO updateReqVO) {
        fbCollectService.updateFbCollect(updateReqVO);
        return success(true);
    }

    @DeleteMapping("/delete")
    @Operation(summary = "删除FB采集任务")
    @Parameter(name = "id", description = "编号", required = true)
    @PreAuthorize("@ss.hasPermission('facebook:fb-collect:delete')")
    public CommonResult<Boolean> deleteFbCollect(@RequestParam("id") Long id) {
        fbCollectService.deleteFbCollect(id);
        return success(true);
    }

    @DeleteMapping("/delete-list")
    @Parameter(name = "ids", description = "编号", required = true)
    @Operation(summary = "批量删除FB采集任务")
                @PreAuthorize("@ss.hasPermission('facebook:fb-collect:delete')")
    public CommonResult<Boolean> deleteFbCollectList(@RequestParam("ids") List<Long> ids) {
        fbCollectService.deleteFbCollectListByIds(ids);
        return success(true);
    }

    @GetMapping("/get")
    @Operation(summary = "获得FB采集任务")
    @Parameter(name = "id", description = "编号", required = true, example = "1024")
    @PreAuthorize("@ss.hasPermission('facebook:fb-collect:query')")
    public CommonResult<FbCollectRespVO> getFbCollect(@RequestParam("id") Long id) {
        FbCollectDO fbCollect = fbCollectService.getFbCollect(id);
        return success(BeanUtils.toBean(fbCollect, FbCollectRespVO.class));
    }

    @GetMapping("/page")
    @Operation(summary = "获得FB采集任务分页")
    @PreAuthorize("@ss.hasPermission('facebook:fb-collect:query')")
    public CommonResult<PageResult<FbCollectRespVO>> getFbCollectPage(@Valid FbCollectPageReqVO pageReqVO) {
        PageResult<FbCollectDO> pageResult = fbCollectService.getFbCollectPage(pageReqVO);
        return success(BeanUtils.toBean(pageResult, FbCollectRespVO.class));
    }

    @GetMapping("/export-excel")
    @Operation(summary = "导出FB采集任务 Excel")
    @PreAuthorize("@ss.hasPermission('facebook:fb-collect:export')")
    @ApiAccessLog(operateType = EXPORT)
    public void exportFbCollectExcel(@Valid FbCollectPageReqVO pageReqVO,
              HttpServletResponse response) throws IOException {
        pageReqVO.setPageSize(PageParam.PAGE_SIZE_NONE);
        List<FbCollectDO> list = fbCollectService.getFbCollectPage(pageReqVO).getList();
        // 导出 Excel
        ExcelUtils.write(response, "FB采集任务.xls", "数据", FbCollectRespVO.class,
                        BeanUtils.toBean(list, FbCollectRespVO.class));
    }

}