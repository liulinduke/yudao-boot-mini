package cn.iocoder.yudao.module.facebook.controller.admin.agent.vo;

import io.swagger.v3.oas.annotations.media.Schema;
import lombok.Data;

import java.time.LocalDateTime;

@Schema(description = "管理后台 - Facebook AI触达记录 Response VO")
@Data
public class FbAiTouchRecordRespVO {

    @Schema(description = "编号")
    private Long id;

    @Schema(description = "Agent配置ID")
    private Long agentConfigId;

    @Schema(description = "线索类型")
    private String leadType;

    @Schema(description = "线索ID")
    private Long leadId;

    @Schema(description = "目标用户ID")
    private String targetUserId;

    @Schema(description = "目标链接")
    private String targetUrl;

    @Schema(description = "执行账号数据库ID")
    private Long accountDbId;

    @Schema(description = "Facebook账号ID")
    private String accountId;

    @Schema(description = "Facebook账号")
    private String fbAccount;

    @Schema(description = "触达类型")
    private String touchType;

    @Schema(description = "AI生成内容")
    private String generatedContent;

    @Schema(description = "AI判断理由")
    private String aiReason;

    @Schema(description = "状态")
    private Integer status;

    @Schema(description = "失败原因")
    private String failReason;

    @Schema(description = "计划发送时间")
    private LocalDateTime scheduledTime;

    @Schema(description = "实际发送时间")
    private LocalDateTime sentTime;

    @Schema(description = "关联运营任务ID")
    private Long operationTaskId;

    @Schema(description = "关联运营任务明细ID")
    private Long operationDetailId;

    @Schema(description = "创建时间")
    private LocalDateTime createTime;

}
