package cn.iocoder.yudao.module.facebook.controller.admin.agent;

import cn.iocoder.yudao.framework.common.pojo.CommonResult;
import cn.iocoder.yudao.framework.common.pojo.PageResult;
import cn.iocoder.yudao.framework.common.util.object.BeanUtils;
import cn.iocoder.yudao.module.facebook.controller.admin.agent.vo.*;
import cn.iocoder.yudao.module.facebook.dal.dataobject.agent.FbAiAgentConfigDO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.agent.FbAiTouchRecordDO;
import cn.iocoder.yudao.module.facebook.service.agent.FbAiAgentService;
import io.swagger.v3.oas.annotations.Operation;
import io.swagger.v3.oas.annotations.tags.Tag;
import jakarta.annotation.Resource;
import jakarta.validation.Valid;
import org.springframework.security.access.prepost.PreAuthorize;
import org.springframework.validation.annotation.Validated;
import org.springframework.web.bind.annotation.*;

import static cn.iocoder.yudao.framework.common.pojo.CommonResult.success;

@Tag(name = "管理后台 - Facebook AI获客Agent")
@RestController
@RequestMapping("/facebook/ai-agent")
@Validated
public class FbAiAgentController {

    @Resource
    private FbAiAgentService aiAgentService;

    @GetMapping("/config")
    @Operation(summary = "获得租户全局Agent配置")
    @PreAuthorize("@ss.hasPermission('facebook:operation-task:query')")
    public CommonResult<FbAiAgentConfigRespVO> getConfig() {
        FbAiAgentConfigDO config = aiAgentService.getConfig();
        if (config == null) {
            return success(null);
        }
        return success(BeanUtils.toBean(config, FbAiAgentConfigRespVO.class));
    }

    @PostMapping("/config/save")
    @Operation(summary = "保存租户全局Agent配置")
    @PreAuthorize("@ss.hasPermission('facebook:operation-task:update')")
    public CommonResult<Long> saveConfig(@Valid @RequestBody FbAiAgentConfigSaveReqVO saveReqVO) {
        return success(aiAgentService.saveConfig(saveReqVO));
    }

    @GetMapping("/touch-record/page")
    @Operation(summary = "获得AI触达记录分页")
    @PreAuthorize("@ss.hasPermission('facebook:operation-task:query')")
    public CommonResult<PageResult<FbAiTouchRecordRespVO>> getTouchRecordPage(@Valid FbAiTouchRecordPageReqVO pageReqVO) {
        PageResult<FbAiTouchRecordDO> pageResult = aiAgentService.getTouchRecordPage(pageReqVO);
        return success(BeanUtils.toBean(pageResult, FbAiTouchRecordRespVO.class));
    }

    @PostMapping("/touch-record/create")
    @Operation(summary = "创建AI触达记录")
    @PreAuthorize("@ss.hasPermission('facebook:operation-task:update')")
    public CommonResult<Long> createTouchRecord(@Valid @RequestBody FbAiTouchRecordSaveReqVO saveReqVO) {
        FbAiTouchRecordDO touchRecord = BeanUtils.toBean(saveReqVO, FbAiTouchRecordDO.class);
        return success(aiAgentService.createTouchRecord(touchRecord));
    }

    @PutMapping("/touch-record/update-result")
    @Operation(summary = "更新AI触达结果")
    @PreAuthorize("@ss.hasPermission('facebook:operation-task:update')")
    public CommonResult<Boolean> updateTouchRecordResult(@Valid @RequestBody FbAiTouchRecordResultReqVO reqVO) {
        aiAgentService.updateTouchRecordResult(reqVO.getId(), reqVO.getStatus(), reqVO.getFailReason());
        return success(true);
    }

    @PostMapping("/lead-analysis/save")
    @Operation(summary = "保存AI线索分析结果")
    @PreAuthorize("@ss.hasPermission('facebook:operation-task:update')")
    public CommonResult<Boolean> saveLeadAnalysis(@Valid @RequestBody FbAiLeadAnalysisSaveReqVO saveReqVO) {
        aiAgentService.saveLeadAnalysis(saveReqVO);
        return success(true);
    }

    @PostMapping("/dispatch-once")
    @Operation(summary = "手动触发一次Agent调度")
    @PreAuthorize("@ss.hasPermission('facebook:operation-task:update')")
    public CommonResult<FbAiAgentDispatchRespVO> dispatchOnce() {
        return success(aiAgentService.dispatchOnce());
    }

}
