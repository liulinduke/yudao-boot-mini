package cn.iocoder.yudao.module.facebook.controller.admin.agent;

import cn.iocoder.yudao.framework.common.pojo.CommonResult;
import cn.iocoder.yudao.framework.common.pojo.PageResult;
import cn.iocoder.yudao.framework.common.util.object.BeanUtils;
import cn.iocoder.yudao.module.facebook.controller.admin.agent.vo.*;
import cn.iocoder.yudao.module.facebook.dal.dataobject.agent.FbAiAgentConfigDO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.agent.FbAiAgentDiscoveryLogDO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.agent.FbAiAgentRunLogDO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.agent.FbAiTouchRecordDO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.collectuser.FbCollectUserDO;
import cn.iocoder.yudao.module.facebook.controller.admin.collectuser.vo.FbCollectUserRespVO;
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
    @Operation(summary = "获得默认Agent配置")
    @PreAuthorize("@ss.hasPermission('facebook:operation-task:query')")
    public CommonResult<FbAiAgentConfigRespVO> getConfig() {
        FbAiAgentConfigDO config = aiAgentService.getConfig();
        if (config == null) {
            return success(null);
        }
        return success(BeanUtils.toBean(config, FbAiAgentConfigRespVO.class));
    }

    @GetMapping("/page")
    @Operation(summary = "获得Agent分页")
    @PreAuthorize("@ss.hasPermission('facebook:operation-task:query')")
    public CommonResult<PageResult<FbAiAgentConfigRespVO>> getConfigPage(@Valid FbAiAgentConfigPageReqVO pageReqVO) {
        PageResult<FbAiAgentConfigDO> pageResult = aiAgentService.getConfigPage(pageReqVO);
        PageResult<FbAiAgentConfigRespVO> resp = BeanUtils.toBean(pageResult, FbAiAgentConfigRespVO.class);
        return success(resp);
    }

    @GetMapping("/get")
    @Operation(summary = "获得Agent详情")
    @PreAuthorize("@ss.hasPermission('facebook:operation-task:query')")
    public CommonResult<FbAiAgentConfigRespVO> getConfig(@RequestParam("id") Long id) {
        FbAiAgentConfigDO config = aiAgentService.getConfig(id);
        return success(config == null ? null : BeanUtils.toBean(config, FbAiAgentConfigRespVO.class));
    }

    @PostMapping("/config/save")
    @Operation(summary = "保存Agent配置")
    @PreAuthorize("@ss.hasPermission('facebook:operation-task:update')")
    public CommonResult<Long> saveConfig(@Valid @RequestBody FbAiAgentConfigSaveReqVO saveReqVO) {
        return success(aiAgentService.saveConfig(saveReqVO));
    }

    @PutMapping("/update-status")
    @Operation(summary = "更新Agent状态")
    @PreAuthorize("@ss.hasPermission('facebook:operation-task:update')")
    public CommonResult<Boolean> updateStatus(@Valid @RequestBody FbAiAgentStatusUpdateReqVO reqVO) {
        aiAgentService.updateStatus(reqVO);
        return success(true);
    }

    @DeleteMapping("/delete")
    @Operation(summary = "删除Agent")
    @PreAuthorize("@ss.hasPermission('facebook:operation-task:update')")
    public CommonResult<Boolean> deleteConfig(@RequestParam("id") Long id) {
        aiAgentService.deleteConfig(id);
        return success(true);
    }

    @PostMapping("/generate-keywords")
    @Operation(summary = "生成AI扩展关键词")
    @PreAuthorize("@ss.hasPermission('facebook:operation-task:update')")
    public CommonResult<FbAiKeywordGenerateRespVO> generateKeywords(@Valid @RequestBody FbAiKeywordGenerateReqVO reqVO) {
        return success(aiAgentService.generateKeywords(reqVO));
    }

    @GetMapping("/discovery-log/page")
    @Operation(summary = "获得客户发现记录分页")
    @PreAuthorize("@ss.hasPermission('facebook:operation-task:query')")
    public CommonResult<PageResult<FbAiAgentDiscoveryLogRespVO>> getDiscoveryLogPage(@Valid FbAiAgentDiscoveryLogPageReqVO pageReqVO) {
        PageResult<FbAiAgentDiscoveryLogDO> pageResult = aiAgentService.getDiscoveryLogPage(pageReqVO);
        return success(BeanUtils.toBean(pageResult, FbAiAgentDiscoveryLogRespVO.class));
    }

    @GetMapping("/touch-record/page")
    @Operation(summary = "获得AI触达记录分页")
    @PreAuthorize("@ss.hasPermission('facebook:operation-task:query')")
    public CommonResult<PageResult<FbAiTouchRecordRespVO>> getTouchRecordPage(@Valid FbAiTouchRecordPageReqVO pageReqVO) {
        PageResult<FbAiTouchRecordDO> pageResult = aiAgentService.getTouchRecordPage(pageReqVO);
        return success(BeanUtils.toBean(pageResult, FbAiTouchRecordRespVO.class));
    }

    @GetMapping("/lead/page")
    @Operation(summary = "获得Agent线索分页")
    @PreAuthorize("@ss.hasPermission('facebook:operation-task:query')")
    public CommonResult<PageResult<FbCollectUserRespVO>> getLeadPage(@Valid FbAiAgentLeadPageReqVO pageReqVO) {
        PageResult<FbCollectUserDO> pageResult = aiAgentService.getLeadPage(pageReqVO);
        return success(BeanUtils.toBean(pageResult, FbCollectUserRespVO.class));
    }

    @GetMapping("/run-log/page")
    @Operation(summary = "获得运行日志分页")
    @PreAuthorize("@ss.hasPermission('facebook:operation-task:query')")
    public CommonResult<PageResult<FbAiAgentRunLogRespVO>> getRunLogPage(@Valid FbAiAgentRunLogPageReqVO pageReqVO) {
        PageResult<FbAiAgentRunLogDO> pageResult = aiAgentService.getRunLogPage(pageReqVO);
        return success(BeanUtils.toBean(pageResult, FbAiAgentRunLogRespVO.class));
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

    @PostMapping("/execute-now")
    @Operation(summary = "立即执行选中的AI主页获客Agent")
    @PreAuthorize("@ss.hasPermission('facebook:operation-task:update')")
    public CommonResult<FbAiAgentDispatchRespVO> executeNow(@Valid @RequestBody FbAiAgentExecuteNowReqVO reqVO) {
        return success(aiAgentService.executeNow(reqVO.getIds()));
    }

}
