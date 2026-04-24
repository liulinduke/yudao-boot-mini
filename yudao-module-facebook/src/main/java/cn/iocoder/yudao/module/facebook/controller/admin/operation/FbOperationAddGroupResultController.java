package cn.iocoder.yudao.module.facebook.controller.admin.operation;

import cn.iocoder.yudao.framework.common.pojo.CommonResult;
import cn.iocoder.yudao.framework.common.pojo.PageResult;
import cn.iocoder.yudao.module.facebook.controller.admin.operation.vo.FbOperationAddGroupResultPageReqVO;
import cn.iocoder.yudao.module.facebook.controller.admin.operation.vo.FbOperationAddGroupResultRespVO;
import cn.iocoder.yudao.module.facebook.service.operation.FbOperationAddGroupResultService;
import io.swagger.v3.oas.annotations.Operation;
import io.swagger.v3.oas.annotations.Parameter;
import io.swagger.v3.oas.annotations.tags.Tag;
import org.springframework.security.access.prepost.PreAuthorize;
import org.springframework.validation.annotation.Validated;
import org.springframework.web.bind.annotation.*;

import javax.annotation.Resource;
import javax.validation.Valid;
import java.util.List;

import static cn.iocoder.yudao.framework.common.pojo.CommonResult.success;

/**
 * 链接加组结果 Controller
 *
 * @author 芋道源码
 */
@Tag(name = "管理后台 - 链接加组结果")
@RestController
@RequestMapping("/facebook/fb-operation-add-group-result")
@Validated
public class FbOperationAddGroupResultController {

    @Resource
    private FbOperationAddGroupResultService addGroupResultService;

    @GetMapping("/page")
    @Operation(summary = "获得加组结果分页")
    @PreAuthorize("@ss.hasPermission('facebook:operation-task:query')")
    public CommonResult<PageResult<FbOperationAddGroupResultRespVO>> getAddGroupResultPage(@Valid FbOperationAddGroupResultPageReqVO pageReqVO) {
        PageResult<FbOperationAddGroupResultRespVO> pageResult = addGroupResultService.getAddGroupResultPage(pageReqVO);
        return success(pageResult);
    }

    @GetMapping("/list-by-task")
    @Operation(summary = "根据任务ID获得加组结果列表")
    @Parameter(name = "taskId", description = "任务ID", required = true)
    @PreAuthorize("@ss.hasPermission('facebook:operation-task:query')")
    public CommonResult<List<FbOperationAddGroupResultRespVO>> getAddGroupResultByTaskId(@RequestParam("taskId") Long taskId) {
        List<FbOperationAddGroupResultRespVO> list = addGroupResultService.getAddGroupResultByTaskId(taskId);
        return success(list);
    }

}
