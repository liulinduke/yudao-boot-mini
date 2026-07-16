package cn.iocoder.yudao.module.facebook.controller.admin.message;

import cn.iocoder.yudao.framework.common.pojo.CommonResult;
import cn.iocoder.yudao.framework.common.pojo.PageResult;
import cn.iocoder.yudao.framework.common.util.object.BeanUtils;
import cn.iocoder.yudao.module.facebook.controller.admin.message.vo.*;
import cn.iocoder.yudao.module.facebook.dal.dataobject.message.*;
import cn.iocoder.yudao.module.facebook.service.message.FbMessageService;
import io.swagger.v3.oas.annotations.Operation;
import io.swagger.v3.oas.annotations.tags.Tag;
import jakarta.annotation.Resource;
import jakarta.validation.Valid;
import org.springframework.security.access.prepost.PreAuthorize;
import org.springframework.validation.annotation.Validated;
import org.springframework.web.bind.annotation.*;

import java.util.List;
import java.util.Map;

import static cn.iocoder.yudao.framework.common.pojo.CommonResult.success;

@Tag(name = "管理后台 - Facebook消息管理")
@RestController
@RequestMapping("/facebook/message")
@Validated
public class FbMessageController {
    @Resource private FbMessageService messageService;

    @GetMapping("/monitor/accounts")
    @Operation(summary = "获取消息监控账号")
    @PreAuthorize("@ss.hasPermission('facebook:message:query')")
    public CommonResult<List<FbMessageMonitorAccountDO>> getMonitorAccounts() { return success(messageService.getMonitorAccounts()); }

    @GetMapping("/monitor/pool")
    @Operation(summary = "获取消息接收池账号")
    @PreAuthorize("@ss.hasPermission('facebook:message:query')")
    public CommonResult<List<FbMessageMonitorAccountDO>> getMonitorPool() { return success(messageService.getMonitorPool()); }

    @GetMapping("/monitor/candidates")
    @Operation(summary = "获取消息监控可选账号")
    @PreAuthorize("@ss.hasPermission('facebook:message:query')")
    public CommonResult<List<Map<String, String>>> getMonitorCandidates() { return success(messageService.getMonitorCandidates()); }

    @PostMapping("/monitor/accounts/save")
    @Operation(summary = "保存消息监控账号")
    @PreAuthorize("@ss.hasPermission('facebook:message:update')")
    public CommonResult<Long> saveMonitorAccount(@Valid @RequestBody FbMessageMonitorAccountSaveReqVO reqVO) { return success(messageService.saveMonitorAccount(reqVO)); }

    @PostMapping("/monitor/accounts/batch-save")
    @Operation(summary = "批量保存消息监控账号")
    @PreAuthorize("@ss.hasPermission('facebook:message:update')")
    public CommonResult<Boolean> batchSaveMonitorAccounts(@Valid @RequestBody List<FbMessageMonitorAccountSaveReqVO> reqVOList) {
        messageService.batchSaveMonitorAccounts(reqVOList);
        return success(true);
    }

    @PostMapping("/monitor/pool/add")
    @Operation(summary = "加入消息接收池")
    @PreAuthorize("@ss.hasPermission('facebook:message:update')")
    public CommonResult<Boolean> addPool(@Valid @RequestBody FbMessageMonitorPoolReqVO reqVO) { messageService.addMonitorPool(reqVO); return success(true); }

    @PostMapping("/monitor/pool/remove")
    @Operation(summary = "移出消息接收池")
    @PreAuthorize("@ss.hasPermission('facebook:message:update')")
    public CommonResult<Boolean> removePool(@Valid @RequestBody FbMessageMonitorPoolReqVO reqVO) { messageService.removeMonitorPool(reqVO); return success(true); }

    @PostMapping("/monitor/batch-state")
    @Operation(summary = "批量更新消息监控状态")
    @PreAuthorize("@ss.hasPermission('facebook:message:update')")
    public CommonResult<Boolean> batchState(@Valid @RequestBody FbMessageMonitorBatchStateReqVO reqVO) { messageService.batchUpdateMonitorState(reqVO); return success(true); }

    @PostMapping("/monitor/interval")
    @Operation(summary = "保存消息定时接收间隔")
    @PreAuthorize("@ss.hasPermission('facebook:message:update')")
    public CommonResult<Boolean> updateMonitorInterval(@Valid @RequestBody FbMessageMonitorIntervalReqVO reqVO) { messageService.updateMonitorIntervals(reqVO); return success(true); }

    @PostMapping("/monitor/normalize-runtime")
    @Operation(summary = "重置消息监控运行状态")
    @PreAuthorize("@ss.hasPermission('facebook:message:update')")
    public CommonResult<Boolean> normalizeRuntime() { messageService.normalizeMonitorRuntimeStates(); return success(true); }

    @PostMapping("/monitor/claim")
    @Operation(summary = "领取消息检查账号")
    @PreAuthorize("@ss.hasPermission('facebook:message:update')")
    public CommonResult<List<FbMessageMonitorClaimRespVO>> claim(@RequestBody FbMessageMonitorClaimReqVO reqVO) { return success(messageService.claimMonitorAccounts(reqVO)); }

    @PostMapping("/monitor/heartbeat")
    @Operation(summary = "刷新实时消息监控账号锁")
    @PreAuthorize("@ss.hasPermission('facebook:message:update')")
    public CommonResult<Boolean> heartbeat(@RequestParam Long monitorId) { return success(messageService.refreshMonitor(monitorId)); }

    @PostMapping("/monitor/report")
    @Operation(summary = "回传消息检查结果")
    @PreAuthorize("@ss.hasPermission('facebook:message:update')")
    public CommonResult<Boolean> report(@RequestParam Long monitorId, @RequestParam boolean success, @RequestParam(required = false) String errorMessage) { messageService.reportMonitor(monitorId, success, errorMessage); return success(true); }

    @PostMapping("/monitor/badge-report")
    @Operation(summary = "回传Facebook未读红圈")
    @PreAuthorize("@ss.hasPermission('facebook:message:update')")
    public CommonResult<Boolean> reportUnreadBadges(@Valid @RequestBody FbMessageMonitorBadgeReportReqVO reqVO) {
        messageService.reportUnreadBadges(reqVO);
        return success(true);
    }

    @PostMapping("/ingest")
    @Operation(summary = "保存WPF回传消息")
    @PreAuthorize("@ss.hasPermission('facebook:message:update')")
    public CommonResult<Long> ingest(@Valid @RequestBody FbMessageIngestReqVO reqVO) { return success(messageService.ingest(reqVO)); }

    @GetMapping("/conversation/page")
    @Operation(summary = "会话分页")
    @PreAuthorize("@ss.hasPermission('facebook:message:query')")
    public CommonResult<PageResult<FbMessageConversationDO>> conversationPage(@Valid FbMessageConversationPageReqVO reqVO) { return success(messageService.getConversationPage(reqVO)); }

    @GetMapping("/conversation/{id}/messages")
    @Operation(summary = "获得会话消息")
    @PreAuthorize("@ss.hasPermission('facebook:message:query')")
    public CommonResult<List<FbMessageDO>> conversationMessages(@PathVariable Long id) { return success(messageService.getConversationMessages(id)); }

    @GetMapping("/conversation/{id}")
    @Operation(summary = "获得会话")
    @PreAuthorize("@ss.hasPermission('facebook:message:query')")
    public CommonResult<FbMessageConversationDO> conversation(@PathVariable Long id) { return success(messageService.getConversation(id)); }

    @PostMapping("/conversation/{id}/read")
    @Operation(summary = "标记会话已读")
    @PreAuthorize("@ss.hasPermission('facebook:message:update')")
    public CommonResult<Boolean> markRead(@PathVariable Long id) { messageService.markConversationRead(id); return success(true); }

    @PostMapping("/conversation/{id}/translate-unread")
    @Operation(summary = "翻译会话未读消息")
    @PreAuthorize("@ss.hasPermission('facebook:message:query')")
    public CommonResult<List<FbMessageDO>> translateUnread(@PathVariable Long id) { return success(messageService.translateUnread(id)); }

    @GetMapping("/unread/summary")
    @Operation(summary = "获取账号未读消息汇总")
    @PreAuthorize("@ss.hasPermission('facebook:message:query')")
    public CommonResult<List<Map<String, Object>>> unreadSummary() { return success(messageService.getUnreadSummary()); }

    @GetMapping("/page")
    @Operation(summary = "消息分页")
    @PreAuthorize("@ss.hasPermission('facebook:message:query')")
    public CommonResult<PageResult<FbMessageDO>> messagePage(@Valid FbMessagePageReqVO reqVO) { return success(messageService.getMessagePage(reqVO)); }

    @PostMapping("/translate")
    @Operation(summary = "消息翻译")
    @PreAuthorize("@ss.hasPermission('facebook:message:query')")
    public CommonResult<Map<String, Object>> translate(@Valid @RequestBody FbMessageTranslateReqVO reqVO) { return success(messageService.translate(reqVO)); }

    @PostMapping("/retry-translation")
    @Operation(summary = "重试消息翻译")
    @PreAuthorize("@ss.hasPermission('facebook:message:query')")
    public CommonResult<Map<String, Object>> retryTranslation(@Valid @RequestBody FbMessageTranslateReqVO reqVO) { return success(messageService.translate(reqVO)); }

    @PostMapping("/send")
    @Operation(summary = "发送消息")
    @PreAuthorize("@ss.hasPermission('facebook:message:update')")
    public CommonResult<Long> send(@Valid @RequestBody FbMessageSendReqVO reqVO) { return success(messageService.send(reqVO)); }
}
