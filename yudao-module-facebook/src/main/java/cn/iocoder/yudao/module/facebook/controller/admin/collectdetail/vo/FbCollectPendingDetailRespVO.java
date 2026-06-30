package cn.iocoder.yudao.module.facebook.controller.admin.collectdetail.vo;

import io.swagger.v3.oas.annotations.media.Schema;
import lombok.Data;

@Schema(description = "管理后台 - WPF待执行采集明细 Response VO")
@Data
public class FbCollectPendingDetailRespVO {

    @Schema(description = "采集任务ID")
    private Long taskId;

    @Schema(description = "采集明细ID")
    private Long detailId;

    @Schema(description = "FB账号")
    private String fbAccount;

    @Schema(description = "账号Cookie")
    private String cookie;

    @Schema(description = "采集链接")
    private String searchUrl;

    @Schema(description = "来源资源库用户ID")
    private Long sourceUserId;

    @Schema(description = "期望采集数量")
    private Integer expectedCount;

    @Schema(description = "任务类型")
    private Integer taskType;
}
