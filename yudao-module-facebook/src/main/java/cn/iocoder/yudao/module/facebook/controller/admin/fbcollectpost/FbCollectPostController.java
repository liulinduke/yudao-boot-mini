package cn.iocoder.yudao.module.facebook.controller.admin.fbcollectpost;

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

import cn.iocoder.yudao.module.facebook.controller.admin.fbcollectpost.vo.*;
import cn.iocoder.yudao.module.facebook.dal.dataobject.fbcollectpost.FbCollectPostDO;
import cn.iocoder.yudao.module.facebook.service.fbcollectpost.FbCollectPostService;

@Tag(name = "管理后台 - FB帖子采集结果")
@RestController
@RequestMapping("/facebook/fb-collect-post")
@Validated
public class FbCollectPostController {

    @Resource
    private FbCollectPostService fbCollectPostService;

    @PostMapping("/create")
    @Operation(summary = "创建FB帖子采集结果")
    @PreAuthorize("@ss.hasPermission('facebook:fb-collect-post:create')")
    public CommonResult<Long> createFbCollectPost(@Valid @RequestBody FbCollectPostSaveReqVO createReqVO) {
        return success(fbCollectPostService.createFbCollectPost(createReqVO));
    }

    @PutMapping("/update")
    @Operation(summary = "更新FB帖子采集结果")
    @PreAuthorize("@ss.hasPermission('facebook:fb-collect-post:update')")
    public CommonResult<Boolean> updateFbCollectPost(@Valid @RequestBody FbCollectPostSaveReqVO updateReqVO) {
        fbCollectPostService.updateFbCollectPost(updateReqVO);
        return success(true);
    }

    @DeleteMapping("/delete")
    @Operation(summary = "删除FB帖子采集结果")
    @Parameter(name = "id", description = "编号", required = true)
    @PreAuthorize("@ss.hasPermission('facebook:fb-collect-post:delete')")
    public CommonResult<Boolean> deleteFbCollectPost(@RequestParam("id") Long id) {
        fbCollectPostService.deleteFbCollectPost(id);
        return success(true);
    }

    @DeleteMapping("/delete-list")
    @Parameter(name = "ids", description = "编号", required = true)
    @Operation(summary = "批量删除FB帖子采集结果")
                @PreAuthorize("@ss.hasPermission('facebook:fb-collect-post:delete')")
    public CommonResult<Boolean> deleteFbCollectPostList(@RequestParam("ids") List<Long> ids) {
        fbCollectPostService.deleteFbCollectPostListByIds(ids);
        return success(true);
    }

    @GetMapping("/get")
    @Operation(summary = "获得FB帖子采集结果")
    @Parameter(name = "id", description = "编号", required = true, example = "1024")
    @PreAuthorize("@ss.hasPermission('facebook:fb-collect-post:query')")
    public CommonResult<FbCollectPostRespVO> getFbCollectPost(@RequestParam("id") Long id) {
        FbCollectPostDO fbCollectPost = fbCollectPostService.getFbCollectPost(id);
        return success(BeanUtils.toBean(fbCollectPost, FbCollectPostRespVO.class));
    }

    @GetMapping("/page")
    @Operation(summary = "获得FB帖子采集结果分页")
    @PreAuthorize("@ss.hasPermission('facebook:fb-collect-post:query')")
    public CommonResult<PageResult<FbCollectPostRespVO>> getFbCollectPostPage(@Valid FbCollectPostPageReqVO pageReqVO) {
        PageResult<FbCollectPostDO> pageResult = fbCollectPostService.getFbCollectPostPage(pageReqVO);
        return success(BeanUtils.toBean(pageResult, FbCollectPostRespVO.class));
    }

    @GetMapping("/export-excel")
    @Operation(summary = "导出FB帖子采集结果 Excel")
    @PreAuthorize("@ss.hasPermission('facebook:fb-collect-post:export')")
    @ApiAccessLog(operateType = EXPORT)
    public void exportFbCollectPostExcel(@Valid FbCollectPostPageReqVO pageReqVO,
              HttpServletResponse response) throws IOException {
        pageReqVO.setPageSize(PageParam.PAGE_SIZE_NONE);
        List<FbCollectPostDO> list = fbCollectPostService.getFbCollectPostPage(pageReqVO).getList();
        // 导出 Excel
        ExcelUtils.write(response, "FB帖子采集结果.xls", "数据", FbCollectPostRespVO.class,
                        BeanUtils.toBean(list, FbCollectPostRespVO.class));
    }

}