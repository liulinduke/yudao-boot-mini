package cn.iocoder.yudao.module.system.controller.admin.notice;

import cn.hutool.core.lang.Assert;
import cn.hutool.core.util.StrUtil;
import cn.iocoder.yudao.framework.common.enums.CommonStatusEnum;
import cn.iocoder.yudao.framework.common.enums.UserTypeEnum;
import cn.iocoder.yudao.framework.common.pojo.CommonResult;
import cn.iocoder.yudao.framework.common.pojo.PageResult;
import cn.iocoder.yudao.framework.common.util.object.BeanUtils;
import cn.iocoder.yudao.module.infra.api.websocket.WebSocketSenderApi;
import cn.iocoder.yudao.module.system.controller.admin.notice.vo.NoticePageReqVO;
import cn.iocoder.yudao.module.system.controller.admin.notice.vo.NoticeRespVO;
import cn.iocoder.yudao.module.system.controller.admin.notice.vo.NoticeSaveReqVO;
import cn.iocoder.yudao.module.system.dal.dataobject.notice.NoticeDO;
import cn.iocoder.yudao.module.system.dal.dataobject.notify.NotifyTemplateDO;
import cn.iocoder.yudao.module.system.service.notify.NotifyMessageService;
import cn.iocoder.yudao.module.system.service.notify.NotifyTemplateService;
import cn.iocoder.yudao.module.system.service.notice.NoticeService;
import cn.iocoder.yudao.module.system.service.user.AdminUserService;
import io.swagger.v3.oas.annotations.Operation;
import io.swagger.v3.oas.annotations.Parameter;
import io.swagger.v3.oas.annotations.tags.Tag;
import jakarta.annotation.Resource;
import jakarta.validation.Valid;
import org.springframework.security.access.prepost.PreAuthorize;
import org.springframework.validation.annotation.Validated;
import org.springframework.web.bind.annotation.*;
import org.springframework.transaction.annotation.Transactional;

import java.util.Arrays;
import java.util.HashMap;
import java.util.List;
import java.util.Map;

import static cn.iocoder.yudao.framework.common.pojo.CommonResult.success;

@Tag(name = "管理后台 - 通知公告")
@RestController
@RequestMapping("/system/notice")
@Validated
public class NoticeController {

    private static final String NOTICE_NOTIFY_TEMPLATE_CODE = "SYSTEM_NOTICE_PUSH";
    private static final int NOTIFY_CONTENT_MAX_LENGTH = 1024;

    @Resource
    private NoticeService noticeService;

    @Resource
    private WebSocketSenderApi webSocketSenderApi;

    @Resource
    private AdminUserService adminUserService;

    @Resource
    private NotifyTemplateService notifyTemplateService;

    @Resource
    private NotifyMessageService notifyMessageService;

    @PostMapping("/create")
    @Operation(summary = "创建通知公告")
    @PreAuthorize("@ss.hasPermission('system:notice:create')")
    public CommonResult<Long> createNotice(@Valid @RequestBody NoticeSaveReqVO createReqVO) {
        Long noticeId = noticeService.createNotice(createReqVO);
        return success(noticeId);
    }

    @PutMapping("/update")
    @Operation(summary = "修改通知公告")
    @PreAuthorize("@ss.hasPermission('system:notice:update')")
    public CommonResult<Boolean> updateNotice(@Valid @RequestBody NoticeSaveReqVO updateReqVO) {
        noticeService.updateNotice(updateReqVO);
        return success(true);
    }

    @DeleteMapping("/delete")
    @Operation(summary = "删除通知公告")
    @Parameter(name = "id", description = "编号", required = true, example = "1024")
    @PreAuthorize("@ss.hasPermission('system:notice:delete')")
    public CommonResult<Boolean> deleteNotice(@RequestParam("id") Long id) {
        noticeService.deleteNotice(id);
        return success(true);
    }

    @DeleteMapping("/delete-list")
    @Operation(summary = "批量删除通知公告")
    @Parameter(name = "ids", description = "编号列表", required = true)
    @PreAuthorize("@ss.hasPermission('system:notice:delete')")
    public CommonResult<Boolean> deleteNoticeList(@RequestParam("ids") List<Long> ids) {
        noticeService.deleteNoticeList(ids);
        return success(true);
    }

    @GetMapping("/page")
    @Operation(summary = "获取通知公告列表")
    @PreAuthorize("@ss.hasPermission('system:notice:query')")
    public CommonResult<PageResult<NoticeRespVO>> getNoticePage(@Validated NoticePageReqVO pageReqVO) {
        PageResult<NoticeDO> pageResult = noticeService.getNoticePage(pageReqVO);
        return success(BeanUtils.toBean(pageResult, NoticeRespVO.class));
    }

    @GetMapping("/get")
    @Operation(summary = "获得通知公告")
    @Parameter(name = "id", description = "编号", required = true, example = "1024")
    @PreAuthorize("@ss.hasPermission('system:notice:query')")
    public CommonResult<NoticeRespVO> getNotice(@RequestParam("id") Long id) {
        NoticeDO notice = noticeService.getNotice(id);
        return success(BeanUtils.toBean(notice, NoticeRespVO.class));
    }

    @PostMapping("/push")
    @Operation(summary = "推送通知公告", description = "保存为站内消息，并实时推送给在线用户")
    @Parameter(name = "id", description = "编号", required = true, example = "1024")
    @PreAuthorize("@ss.hasPermission('system:notice:update')")
    @Transactional(rollbackFor = Exception.class)
    public CommonResult<Boolean> push(@RequestParam("id") Long id) {
        NoticeDO notice = noticeService.getNotice(id);
        Assert.notNull(notice, "公告不能为空");

        // 站内消息必须落库，离线用户登录后才能看到；WebSocket 只负责在线实时提醒。
        NotifyTemplateDO template = getOrCreateNoticeNotifyTemplate();
        Map<String, Object> templateParams = new HashMap<>();
        templateParams.put("title", notice.getTitle());
        String messageContent = "【" + StrUtil.nullToEmpty(notice.getTitle()) + "】\n"
                + toPlainText(notice.getContent());
        messageContent = StrUtil.subWithLength(messageContent, 0, NOTIFY_CONTENT_MAX_LENGTH);
        for (var user : adminUserService.getUserListByStatus(CommonStatusEnum.ENABLE.getStatus())) {
            notifyMessageService.createNotifyMessage(user.getId(), UserTypeEnum.ADMIN.getValue(),
                    template, messageContent, templateParams);
        }

        // 通过 websocket 推送给在线的用户
        webSocketSenderApi.sendObject(UserTypeEnum.ADMIN.getValue(), "notice-push", notice);
        return success(true);
    }

    /**
     * 公告推送使用固定模板，兼容没有预置站内信模板的历史数据库。
     * synchronized 只保护同一 JVM 内的首次初始化，后续查询不会重复创建。
     */
    private synchronized NotifyTemplateDO getOrCreateNoticeNotifyTemplate() {
        NotifyTemplateDO template = notifyTemplateService
                .getNotifyTemplateByCodeFromCache(NOTICE_NOTIFY_TEMPLATE_CODE);
        if (template != null) {
            return template;
        }
        NotifyTemplateDO newTemplate = new NotifyTemplateDO()
                .setName("公告推送")
                .setCode(NOTICE_NOTIFY_TEMPLATE_CODE)
                .setType(1)
                .setNickname("系统公告")
                .setContent("【{title}】")
                .setParams(Arrays.asList("title"))
                .setStatus(CommonStatusEnum.ENABLE.getStatus())
                .setRemark("通知公告推送自动创建的站内信模板");
        // 直接使用模板 Mapper 的写入能力不暴露在 Service API 中，因此通过模板服务创建后重新读取缓存。
        notifyTemplateService.createNotifyTemplate(BeanUtils.toBean(newTemplate,
                cn.iocoder.yudao.module.system.controller.admin.notify.vo.template.NotifyTemplateSaveReqVO.class));
        return notifyTemplateService.getNotifyTemplateByCodeFromCache(NOTICE_NOTIFY_TEMPLATE_CODE);
    }

    /** 站内消息按纯文本展示，避免富文本编辑器的标签直接显示出来。 */
    private String toPlainText(String html) {
        if (StrUtil.isEmpty(html)) {
            return "";
        }
        return html
                .replaceAll("(?i)<br\\s*/?>", "\\n")
                .replaceAll("(?i)</p\\s*>", "\\n")
                .replaceAll("<[^>]+>", "")
                .replace("&nbsp;", " ")
                .replace("&amp;", "&")
                .replace("&lt;", "<")
                .replace("&gt;", ">")
                .replace("&quot;", "\"")
                .replace("&#39;", "'")
                .trim();
    }

}
