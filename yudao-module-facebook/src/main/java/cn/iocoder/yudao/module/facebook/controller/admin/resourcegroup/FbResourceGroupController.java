package cn.iocoder.yudao.module.facebook.controller.admin.resourcegroup;

import cn.iocoder.yudao.framework.common.pojo.CommonResult;
import cn.iocoder.yudao.module.facebook.controller.admin.resourcegroup.vo.*;
import cn.iocoder.yudao.module.facebook.service.resourcegroup.FbResourceGroupService;
import io.swagger.v3.oas.annotations.Operation;
import io.swagger.v3.oas.annotations.tags.Tag;
import jakarta.annotation.Resource;
import jakarta.validation.Valid;
import org.springframework.validation.annotation.Validated;
import org.springframework.web.bind.annotation.*;
import java.util.List;
import static cn.iocoder.yudao.framework.common.pojo.CommonResult.success;

@Tag(name = "管理后台 - Facebook资源分组")
@RestController
@RequestMapping("/facebook/resource-group")
@Validated
public class FbResourceGroupController {
    @Resource
    private FbResourceGroupService service;

    @GetMapping("/list")
    @Operation(summary = "获得指定资源类型的分组")
    public CommonResult<List<FbResourceGroupRespVO>> getList(@RequestParam String resourceType) {
        return success(service.getList(resourceType));
    }

    @PostMapping("/create")
    public CommonResult<Long> create(@Valid @RequestBody FbResourceGroupSaveReqVO reqVO) {
        return success(service.create(reqVO));
    }

    @PutMapping("/update")
    public CommonResult<Boolean> update(@Valid @RequestBody FbResourceGroupSaveReqVO reqVO) {
        service.update(reqVO);
        return success(true);
    }

    @DeleteMapping("/delete")
    public CommonResult<Boolean> delete(@RequestParam Long id) {
        service.delete(id);
        return success(true);
    }
}
