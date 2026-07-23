package cn.iocoder.yudao.module.facebook.controller.admin.collectdetail.vo;

import com.fasterxml.jackson.databind.ser.std.ToStringSerializer;
import com.fasterxml.jackson.databind.annotation.JsonSerialize;
import io.swagger.v3.oas.annotations.media.Schema;
import lombok.Data;

@Schema(description = "管理后台 - WPF待执行采集明细 Response VO")
@Data
public class FbCollectPendingDetailRespVO {

    @Schema(description = "采集任务ID")
    @JsonSerialize(using = ToStringSerializer.class)
    private Long taskId;

    @Schema(description = "采集明细ID")
    @JsonSerialize(using = ToStringSerializer.class)
    private Long detailId;

    @Schema(description = "FB账号")
    private String fbAccount;

    @Schema(description = "账号Cookie")
    private String cookie;

    @Schema(description = "采集链接")
    private String searchUrl;

    @Schema(description = "来源资源库用户ID")
    @JsonSerialize(using = ToStringSerializer.class)
    private Long sourceUserId;

    @Schema(description = "期望采集数量")
    private Integer expectedCount;

    @Schema(description = "任务类型")
    private Integer taskType;

    @Schema(description = "队列来源：collect/dm/operation")
    private String sourceType;

    @Schema(description = "执行账号ID")
    private String accountId;

    @Schema(description = "私信目标用户ID")
    private String targetUserId;

    @Schema(description = "私信话术")
    private String scriptContent;

    @Schema(description = "最小间隔秒")
    private Integer minIntervalSeconds;

    @Schema(description = "最大间隔秒")
    private Integer maxIntervalSeconds;

    @Schema(description = "运营任务配置")
    private String actionConfig;
}
