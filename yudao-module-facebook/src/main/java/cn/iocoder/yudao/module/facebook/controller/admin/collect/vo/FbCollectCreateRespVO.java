package cn.iocoder.yudao.module.facebook.controller.admin.collect.vo;

import io.swagger.v3.oas.annotations.media.Schema;
import lombok.*;

import java.util.List;

@Schema(description = "管理后台 - FB采集任务创建响应 VO")
@Data
@NoArgsConstructor
@AllArgsConstructor
public class FbCollectCreateRespVO {

    @Schema(description = "主表任务ID", requiredMode = Schema.RequiredMode.REQUIRED, example = "100")
    private Long taskId;

    @Schema(description = "明细ID列表", requiredMode = Schema.RequiredMode.REQUIRED)
    private List<DetailInfo> details;

    @Schema(description = "明细信息")
    @Data
    @NoArgsConstructor
    @AllArgsConstructor
    public static class DetailInfo {
        @Schema(description = "明细ID", requiredMode = Schema.RequiredMode.REQUIRED, example = "1001")
        private Long detailId;

        @Schema(description = "FB账号", requiredMode = Schema.RequiredMode.REQUIRED, example = "29913")
        private String fbAccount;

        @Schema(description = "采集链接", requiredMode = Schema.RequiredMode.REQUIRED, example = "https://www.facebook.com/search/users?q=xxx")
        private String searchUrl;
    }
}
