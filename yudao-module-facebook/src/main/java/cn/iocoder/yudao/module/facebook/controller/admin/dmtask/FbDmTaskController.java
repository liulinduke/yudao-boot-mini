package cn.iocoder.yudao.module.facebook.controller.admin.dmtask;

import cn.iocoder.yudao.framework.common.pojo.CommonResult;
import cn.iocoder.yudao.framework.common.pojo.PageParam;
import cn.iocoder.yudao.framework.common.pojo.PageResult;
import cn.iocoder.yudao.module.facebook.controller.admin.dmtask.vo.FbDmTaskSaveReqVO;
import cn.iocoder.yudao.module.facebook.controller.admin.dmtask.vo.FbDmTaskRespVO;
import cn.iocoder.yudao.module.facebook.controller.admin.dmtask.vo.FbDmTaskPageReqVO;
import cn.iocoder.yudao.module.facebook.service.dmtask.FbDmTaskService;
import io.swagger.v3.oas.annotations.Operation;
import io.swagger.v3.oas.annotations.Parameter;
import io.swagger.v3.oas.annotations.tags.Tag;
import javax.annotation.Resource;
import javax.validation.Valid;
import org.springframework.security.access.prepost.PreAuthorize;
import org.springframework.validation.annotation.Validated;
import org.springframework.web.bind.annotation.*;

import static cn.iocoder.yudao.framework.common.pojo.CommonResult.success;

@Tag(name = "管理后台 - Facebook群发私信任务")
@RestController
@RequestMapping("/facebook/dm-task")
@Validated
public class FbDmTaskController {

    @Resource
    private FbDmTaskService dmTaskService;

    @PostMapping("/create")
    @Operation(summary = "创建群发私信任务")
    @PreAuthorize("@ss.hasPermission('facebook:dm-task:create')")
    public CommonResult<Long> createDmTask(@Valid @RequestBody FbDmTaskSaveReqVO saveReqVO) {
        return success(dmTaskService.createDmTask(saveReqVO));
    }

    @PutMapping("/update")
    @Operation(summary = "更新群发私信任务")
    @PreAuthorize("@ss.hasPermission('facebook:dm-task:update')")
    public CommonResult<Boolean> updateDmTask(@Valid @RequestBody FbDmTaskSaveReqVO saveReqVO) {
        dmTaskService.updateDmTask(saveReqVO);
        return success(true);
    }

    @DeleteMapping("/delete")
    @Operation(summary = "删除群发私信任务")
    @Parameter(name = "id", description = "编号", required = true)
    @PreAuthorize("@ss.hasPermission('facebook:dm-task:delete')")
    public CommonResult<Boolean> deleteDmTask(@RequestParam("id") Long id) {
        dmTaskService.deleteDmTask(id);
        return success(true);
    }

    @GetMapping("/get")
    @Operation(summary = "获得群发私信任务")
    @Parameter(name = "id", description = "编号", required = true, example = "1024")
    @PreAuthorize("@ss.hasPermission('facebook:dm-task:query')")
    public CommonResult<FbDmTaskRespVO> getDmTask(@RequestParam("id") Long id) {
        return success(dmTaskService.getDmTask(id));
    }

    @GetMapping("/page")
    @Operation(summary = "获得群发私信任务分页")
    @PreAuthorize("@ss.hasPermission('facebook:dm-task:query')")
    public CommonResult<PageResult<FbDmTaskRespVO>> getDmTaskPage(@Valid FbDmTaskPageReqVO pageReqVO) {
        return success(dmTaskService.getDmTaskPage(pageReqVO));
    }

    @PostMapping("/start/{id}")
    @Operation(summary = "启动任务")
    @Parameter(name = "id", description = "任务ID", required = true)
    @PreAuthorize("@ss.hasPermission('facebook:dm-task:update')")
    public CommonResult<Boolean> startTask(@PathVariable("id") Long id) {
        dmTaskService.startTask(id);
        return success(true);
    }

    @PostMapping("/cancel/{id}")
    @Operation(summary = "取消任务")
    @Parameter(name = "id", description = "任务ID", required = true)
    @PreAuthorize("@ss.hasPermission('facebook:dm-task:update')")
    public CommonResult<Boolean> cancelTask(@PathVariable("id") Long id) {
        dmTaskService.cancelTask(id);
        return success(true);
    }

}
