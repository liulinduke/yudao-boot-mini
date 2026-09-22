package cn.iocoder.yudao.module.facebook.controller.admin.aisearch;
import cn.iocoder.yudao.framework.common.pojo.CommonResult;
import cn.iocoder.yudao.module.facebook.controller.admin.aisearch.vo.*;
import cn.iocoder.yudao.module.facebook.service.aisearch.AiSearchSourceService;
import io.swagger.v3.oas.annotations.tags.Tag; import jakarta.annotation.Resource; import jakarta.validation.Valid; import org.springframework.web.bind.annotation.*;
import java.util.List; import static cn.iocoder.yudao.framework.common.pojo.CommonResult.success;
@Tag(name="管理后台 - AI企业获客渠道") @RestController @RequestMapping("/facebook/ai-search/source")
public class AiSearchSourceController {
    @Resource private AiSearchSourceService service;
    @PostMapping("/create") public CommonResult<Long> create(@Valid @RequestBody AiSearchSourceSaveReqVO req){return success(service.create(req));}
    @PostMapping("/batch") public CommonResult<Integer> createBatch(@Valid @RequestBody List<AiSearchSourceSaveReqVO> requests){return success(service.createBatch(requests));}
    @PutMapping("/update") public CommonResult<Boolean> update(@Valid @RequestBody AiSearchSourceSaveReqVO req){service.update(req);return success(true);}
    @PutMapping("/status") public CommonResult<Boolean> status(@RequestParam Long id,@RequestParam String status){service.updateStatus(id,status);return success(true);}
    @GetMapping("/list") public CommonResult<List<AiSearchSourceRespVO>> list(@RequestParam(required=false) String status){return success(service.list(status));}
}
