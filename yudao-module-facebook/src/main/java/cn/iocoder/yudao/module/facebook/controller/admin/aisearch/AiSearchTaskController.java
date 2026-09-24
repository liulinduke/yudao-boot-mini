package cn.iocoder.yudao.module.facebook.controller.admin.aisearch;

import cn.iocoder.yudao.framework.common.pojo.CommonResult;
import cn.iocoder.yudao.module.facebook.controller.admin.aisearch.vo.*;
import cn.iocoder.yudao.module.facebook.service.aisearch.AiSearchTaskService;
import io.swagger.v3.oas.annotations.tags.Tag;
import jakarta.annotation.Resource;
import jakarta.validation.Valid;
import org.springframework.web.bind.annotation.*;

import java.util.List;

import static cn.iocoder.yudao.framework.common.pojo.CommonResult.success;

@Tag(name = "管理后台 - AI全网企业获客")
@RestController
@RequestMapping("/facebook/ai-search/task")
public class AiSearchTaskController {
    @Resource
    private AiSearchTaskService service;

    @PostMapping("/create")
    public CommonResult<Long> create(@Valid @RequestBody AiSearchTaskSaveReqVO req) {
        return success(service.create(req));
    }

    @PutMapping("/update")
    public CommonResult<Boolean> update(@Valid @RequestBody AiSearchTaskSaveReqVO req) {
        service.update(req);
        return success(true);
    }

    @DeleteMapping("/delete")
    public CommonResult<Boolean> delete(@RequestParam Long id) {
        service.delete(id);
        return success(true);
    }

    @GetMapping("/get")
    public CommonResult<AiSearchTaskRespVO> get(@RequestParam Long id) {
        return success(service.get(id));
    }

    @GetMapping("/list")
    public CommonResult<List<AiSearchTaskRespVO>> list() {
        return success(service.list());
    }

    @PostMapping("/start")
    public CommonResult<Boolean> start(@RequestParam Long id) {
        service.start(id);
        return success(true);
    }

    @PutMapping("/status")
    public CommonResult<Boolean> status(@RequestParam Long id, @RequestParam String status) {
        service.updateStatus(id, status);
        return success(true);
    }

    @PostMapping("/expand")
    public CommonResult<AiSearchExpansionRespVO> expand(@Valid @RequestBody AiSearchExpansionReqVO req) {
        return success(service.expand(req));
    }
}
