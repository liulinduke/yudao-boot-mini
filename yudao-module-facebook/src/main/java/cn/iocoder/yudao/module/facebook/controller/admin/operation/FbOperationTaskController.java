package cn.iocoder.yudao.module.facebook.controller.admin.operation;

import cn.iocoder.yudao.framework.common.pojo.CommonResult;
import cn.iocoder.yudao.framework.common.pojo.PageResult;
import cn.iocoder.yudao.module.facebook.controller.admin.operation.vo.*;
import cn.iocoder.yudao.module.facebook.service.operation.FbOperationTaskService;
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

@Tag(name = "管理后台 - 运营任务")
@RestController
@RequestMapping("/facebook/fb-operation-task")
@Validated
public class FbOperationTaskController {

    @Resource
    private FbOperationTaskService operationTaskService;

    @PostMapping("/create")
    @Operation(summary = "创建运营任务")
    @PreAuthorize("@ss.hasPermission('facebook:operation-task:create')")
    public CommonResult<Long> createOperationTask(@Valid @RequestBody FbOperationTaskSaveReqVO createReqVO) {
        return success(operationTaskService.createOperationTask(createReqVO));
    }

    @PutMapping("/update")
    @Operation(summary = "更新运营任务")
    @PreAuthorize("@ss.hasPermission('facebook:operation-task:update')")
    public CommonResult<Boolean> updateOperationTask(@Valid @RequestBody FbOperationTaskSaveReqVO updateReqVO) {
        operationTaskService.updateOperationTask(updateReqVO);
        return success(true);
    }

    @DeleteMapping("/delete")
    @Operation(summary = "删除运营任务")
    @Parameter(name = "id", description = "编号", required = true)
    @PreAuthorize("@ss.hasPermission('facebook:operation-task:delete')")
    public CommonResult<Boolean> deleteOperationTask(@RequestParam("id") Long id) {
        operationTaskService.deleteOperationTask(id);
        return success(true);
    }

    @GetMapping("/get")
    @Operation(summary = "获得运营任务详情")
    @Parameter(name = "id", description = "编号", required = true, example = "1024")
    @PreAuthorize("@ss.hasPermission('facebook:operation-task:query')")
    public CommonResult<FbOperationTaskDetailRespVO> getOperationTask(@RequestParam("id") Long id) {
        return success(operationTaskService.getOperationTask(id));
    }

    @GetMapping("/page")
    @Operation(summary = "获得运营任务分页")
    @PreAuthorize("@ss.hasPermission('facebook:operation-task:query')")
    public CommonResult<PageResult<FbOperationTaskRespVO>> getOperationTaskPage(@Valid FbOperationTaskPageReqVO pageReqVO) {
        PageResult<FbOperationTaskRespVO> pageResult = operationTaskService.getOperationTaskPage(pageReqVO);
        return success(pageResult);
    }

    @PostMapping("/batch-save-add-group-result")
    @Operation(summary = "批量保存链接加组结果")
    @PreAuthorize("@ss.hasPermission('facebook:operation-task:update')")
    public CommonResult<Boolean> batchSaveAddGroupResult(@Valid @RequestBody FbOperationAddGroupResultBatchSaveReqVO batchSaveReqVO) {
        operationTaskService.batchSaveAddGroupResult(batchSaveReqVO);
        return success(true);
    }

    @GetMapping("/pending-details")
    @Operation(summary = "获取待执行的明细列表")
    @Parameter(name = "accountId", description = "Facebook账号ID", required = true)
    @PreAuthorize("@ss.hasPermission('facebook:operation-task:query')")
    public CommonResult<List<FbOperationTaskDetailItemRespVO>> getPendingDetails(@RequestParam("accountId") String accountId) {
        return success(operationTaskService.getPendingDetails(accountId));
    }

}
