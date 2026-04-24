package cn.iocoder.yudao.module.facebook.controller.admin.dailylimit;

import cn.iocoder.yudao.framework.common.pojo.CommonResult;
import cn.iocoder.yudao.module.facebook.enums.OperationTypeEnum;
import cn.iocoder.yudao.module.facebook.service.dailylimit.FacebookDailyLimitService;
import io.swagger.v3.oas.annotations.Operation;
import io.swagger.v3.oas.annotations.Parameter;
import io.swagger.v3.oas.annotations.tags.Tag;
import javax.annotation.Resource;
import org.springframework.security.access.prepost.PreAuthorize;
import org.springframework.validation.annotation.Validated;
import org.springframework.web.bind.annotation.*;

import java.util.HashMap;
import java.util.Map;

import static cn.iocoder.yudao.framework.common.pojo.CommonResult.success;

@Tag(name = "管理后台 - Facebook每日限制")
@RestController
@RequestMapping("/facebook/daily-limit")
@Validated
public class FacebookDailyLimitController {

    @Resource
    private FacebookDailyLimitService dailyLimitService;

    @GetMapping("/config")
    @Operation(summary = "获取所有限制配置")
    @PreAuthorize("@ss.hasPermission('facebook:global-config:query')")
    public CommonResult<Map<String, Object>> getConfig() {
        Map<String, Object> config = new HashMap<>();
        for (OperationTypeEnum type : OperationTypeEnum.values()) {
            Map<String, Object> item = new HashMap<>();
            item.put("name", type.getName());
            item.put("code", type.getCode());
            item.put("limit", dailyLimitService.getConfiguredLimit(type));
            config.put(type.getCode(), item);
        }
        return success(config);
    }

    @GetMapping("/remaining/{accountId}/{type}")
    @Operation(summary = "获取剩余次数")
    @Parameter(name = "accountId", description = "账号ID", required = true)
    @Parameter(name = "type", description = "操作类型", required = true)
    @PreAuthorize("@ss.hasPermission('facebook:account:query')")
    public CommonResult<Integer> getRemainingCount(
            @PathVariable String accountId,
            @PathVariable String type) {
        OperationTypeEnum operationType = OperationTypeEnum.getByCode(type);
        if (operationType == null) {
            return success(0);
        }
        return success(dailyLimitService.getRemainingCount(accountId, operationType));
    }

    @GetMapping("/all-remaining/{accountId}")
    @Operation(summary = "获取所有操作的剩余次数")
    @Parameter(name = "accountId", description = "账号ID", required = true)
    @PreAuthorize("@ss.hasPermission('facebook:account:query')")
    public CommonResult<Map<String, Integer>> getAllRemainingCounts(@PathVariable String accountId) {
        return success(dailyLimitService.getAllRemainingCounts(accountId));
    }

}
