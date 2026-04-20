package cn.iocoder.yudao.module.facebook.controller.admin.collectdetail;

import cn.iocoder.yudao.framework.common.pojo.CommonResult;
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

import javax.annotation.Resource;
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
    public CommonResult<List<FbCollectDetailDO>> getPendingDetails(@RequestParam("fbAccount") String fbAccount) {
        List<FbCollectDetailDO> details = fbCollectDetailService.getPendingDetailsByAccount(fbAccount);
        return success(details);
    }

    @GetMapping("/list-by-task")
    @Operation(summary = "根据任务ID查询明细列表")
    @Parameter(name = "taskId", description = "任务ID", required = true, example = "1024")
    @PreAuthorize("@ss.hasPermission('facebook:fb-collect:query')")
    public CommonResult<List<FbCollectDetailDO>> getDetailListByTaskId(@RequestParam("taskId") Long taskId) {
        List<FbCollectDetailDO> list = fbCollectDetailService.getDetailListByTaskId(taskId);
        return success(list);
    }
}
