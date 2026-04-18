package cn.iocoder.yudao.module.facebook.controller.admin.collectuser;

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

import cn.iocoder.yudao.module.facebook.controller.admin.collectuser.vo.*;
import cn.iocoder.yudao.module.facebook.dal.dataobject.collectuser.FbCollectUserDO;
import cn.iocoder.yudao.module.facebook.service.collectuser.FbCollectUserService;

@Tag(name = "管理后台 - FB用户采集结果")
@RestController
@RequestMapping("/facebook/fb-collect-user")
@Validated
public class FbCollectUserController {

    @Resource
    private FbCollectUserService fbCollectUserService;

    @PostMapping("/create")
    @Operation(summary = "创建FB用户采集结果")
    @PreAuthorize("@ss.hasPermission('facebook:fb-collect-user:create')")
    public CommonResult<Long> createFbCollectUser(@Valid @RequestBody FbCollectUserSaveReqVO createReqVO) {
        return success(fbCollectUserService.createFbCollectUser(createReqVO));
    }

    @PutMapping("/update")
    @Operation(summary = "更新FB用户采集结果")
    @PreAuthorize("@ss.hasPermission('facebook:fb-collect-user:update')")
    public CommonResult<Boolean> updateFbCollectUser(@Valid @RequestBody FbCollectUserSaveReqVO updateReqVO) {
        fbCollectUserService.updateFbCollectUser(updateReqVO);
        return success(true);
    }

    @DeleteMapping("/delete")
    @Operation(summary = "删除FB用户采集结果")
    @Parameter(name = "id", description = "编号", required = true)
    @PreAuthorize("@ss.hasPermission('facebook:fb-collect-user:delete')")
    public CommonResult<Boolean> deleteFbCollectUser(@RequestParam("id") Long id) {
        fbCollectUserService.deleteFbCollectUser(id);
        return success(true);
    }

    @DeleteMapping("/delete-list")
    @Parameter(name = "ids", description = "编号", required = true)
    @Operation(summary = "批量删除FB用户采集结果")
                @PreAuthorize("@ss.hasPermission('facebook:fb-collect-user:delete')")
    public CommonResult<Boolean> deleteFbCollectUserList(@RequestParam("ids") List<Long> ids) {
        fbCollectUserService.deleteFbCollectUserListByIds(ids);
        return success(true);
    }

    @GetMapping("/get")
    @Operation(summary = "获得FB用户采集结果")
    @Parameter(name = "id", description = "编号", required = true, example = "1024")
    @PreAuthorize("@ss.hasPermission('facebook:fb-collect-user:query')")
    public CommonResult<FbCollectUserRespVO> getFbCollectUser(@RequestParam("id") Long id) {
        FbCollectUserDO fbCollectUser = fbCollectUserService.getFbCollectUser(id);
        return success(BeanUtils.toBean(fbCollectUser, FbCollectUserRespVO.class));
    }

    @GetMapping("/page")
    @Operation(summary = "获得FB用户采集结果分页")
    @PreAuthorize("@ss.hasPermission('facebook:fb-collect-user:query')")
    public CommonResult<PageResult<FbCollectUserRespVO>> getFbCollectUserPage(@Valid FbCollectUserPageReqVO pageReqVO) {
        PageResult<FbCollectUserDO> pageResult = fbCollectUserService.getFbCollectUserPage(pageReqVO);
        return success(BeanUtils.toBean(pageResult, FbCollectUserRespVO.class));
    }

    @GetMapping("/export-excel")
    @Operation(summary = "导出FB用户采集结果 Excel")
    @PreAuthorize("@ss.hasPermission('facebook:fb-collect-user:export')")
    @ApiAccessLog(operateType = EXPORT)
    public void exportFbCollectUserExcel(@Valid FbCollectUserPageReqVO pageReqVO,
              HttpServletResponse response) throws IOException {
        pageReqVO.setPageSize(PageParam.PAGE_SIZE_NONE);
        List<FbCollectUserDO> list = fbCollectUserService.getFbCollectUserPage(pageReqVO).getList();
        // 导出 Excel
        ExcelUtils.write(response, "FB用户采集结果.xls", "数据", FbCollectUserRespVO.class,
                        BeanUtils.toBean(list, FbCollectUserRespVO.class));
    }

    @PostMapping("/batch-save")
    @Operation(summary = "批量保存FB用户采集结果")
    @PreAuthorize("@ss.hasPermission('facebook:fb-collect-user:create')")
    public CommonResult<Integer> batchSaveFbCollectUser(@Valid @RequestBody FbCollectUserBatchSaveReqVO batchSaveReqVO) {
        Integer count = fbCollectUserService.batchSaveFbCollectUser(batchSaveReqVO.getTaskId(), batchSaveReqVO.getResults());
        return success(count);
    }

}