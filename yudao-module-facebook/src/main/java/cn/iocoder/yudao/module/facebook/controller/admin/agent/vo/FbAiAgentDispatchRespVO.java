package cn.iocoder.yudao.module.facebook.controller.admin.agent.vo;

import io.swagger.v3.oas.annotations.media.Schema;
import lombok.AllArgsConstructor;
import lombok.Data;
import lombok.NoArgsConstructor;

import java.util.ArrayList;
import java.util.List;

@Schema(description = "管理后台 - Facebook AI获客Agent调度结果 Response VO")
@Data
@NoArgsConstructor
@AllArgsConstructor
public class FbAiAgentDispatchRespVO {

    @Schema(description = "是否已调度")
    private Boolean dispatched;

    @Schema(description = "说明")
    private String message;

    @Schema(description = "本次新建且可启动的采集明细")
    private List<CollectDetail> details = new ArrayList<>();

    public FbAiAgentDispatchRespVO(Boolean dispatched, String message) {
        this.dispatched = dispatched;
        this.message = message;
    }

    @Data
    @NoArgsConstructor
    @AllArgsConstructor
    public static class CollectDetail {

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

        @Schema(description = "期望采集数量")
        private Integer expectedCount;

        @Schema(description = "任务类型")
        private Integer taskType;
    }

}
