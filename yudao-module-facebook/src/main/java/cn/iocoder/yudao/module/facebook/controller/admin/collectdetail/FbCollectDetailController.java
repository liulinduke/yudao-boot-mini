package cn.iocoder.yudao.module.facebook.controller.admin.collectdetail;

import cn.iocoder.yudao.framework.common.pojo.CommonResult;
import cn.iocoder.yudao.module.facebook.controller.admin.collectdetail.vo.FbCollectPendingDetailRespVO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.collectdetail.FbCollectDetailDO;
import cn.iocoder.yudao.module.facebook.service.collectdetail.FbCollectDetailService;
import io.swagger.v3.oas.annotations.Operation;
import io.swagger.v3.oas.annotations.Parameter;
import io.swagger.v3.oas.annotations.tags.Tag;
import org.springframework.security.access.prepost.PreAuthorize;
import org.springframework.validation.annotation.Validated;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RequestParam;
import org.springframework.web.bind.annotation.RestController;

import jakarta.annotation.Resource;
import java.util.List;

import static cn.iocoder.yudao.framework.common.pojo.CommonResult.success;

/**
 * FB采集任务明细 Controller
 *
 * @author jacky
 */
@Tag(name = "管理后台 - FB采集任务明细")
@RestController
@RequestMapping("/facebook/fb-collect-detail")
@Validated
public class FbCollectDetailController {

    @Resource
    private FbCollectDetailService fbCollectDetailService;

    @GetMapping("/pending")
    @Operation(summary = "查询账号的待执行明细")
    @Parameter(name = "fbAccount", description = "FB账号", required = true, example = "29913")
    @PreAuthorize("@ss.hasPermission('facebook:fb-collect:query')")
    public CommonResult<List<FbCollectDetailDO>> getPendingDetails(@RequestParam("fbAccount") String fbAccount,
                                                                   @RequestParam(value = "taskId", required = false) String taskId) {
        List<FbCollectDetailDO> details = fbCollectDetailService.getPendingDetailsByAccount(fbAccount,
                taskId == null ? null : Long.parseLong(taskId));
        return success(details);
    }

    @GetMapping("/get")
    @Operation(summary = "查询采集明细")
    @Parameter(name = "id", description = "明细ID", required = true, example = "1024")
    @PreAuthorize("@ss.hasPermission('facebook:fb-collect:query')")
    public CommonResult<FbCollectDetailDO> getDetail(@RequestParam("id") String id) {
        return success(fbCollectDetailService.getDetail(Long.parseLong(id)));
    }

    @GetMapping("/list-by-task")
    @Operation(summary = "根据任务ID查询明细列表")
    @Parameter(name = "taskId", description = "任务ID", required = true, example = "1024")
    @PreAuthorize("@ss.hasPermission('facebook:fb-collect:query')")
    public CommonResult<List<FbCollectDetailDO>> getDetailListByTaskId(@RequestParam("taskId") String taskId) {
        // 将字符串转换为 Long，避免前端精度丢失
        Long taskIdLong = Long.parseLong(taskId);
        List<FbCollectDetailDO> list = fbCollectDetailService.getDetailListByTaskId(taskIdLong);
        return success(list);
    }

    @GetMapping("/claim-pending")
    @Operation(summary = "WPF领取待执行采集明细")
    @PreAuthorize("@ss.hasPermission('facebook:fb-collect:query')")
    public CommonResult<List<FbCollectPendingDetailRespVO>> claimPendingDetails(
            @RequestParam(value = "limit", defaultValue = "3") Integer limit) {
        return success(fbCollectDetailService.claimPendingDetails(limit));
    }
}
