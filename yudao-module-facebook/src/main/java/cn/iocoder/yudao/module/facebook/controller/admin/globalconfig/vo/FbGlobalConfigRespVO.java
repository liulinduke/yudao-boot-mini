package cn.iocoder.yudao.module.facebook.controller.admin.globalconfig.vo;

import io.swagger.v3.oas.annotations.media.Schema;
import lombok.Data;

import java.time.LocalDateTime;

@Schema(description = "管理后台 - Facebook 全局配置 Response VO")
@Data
public class FbGlobalConfigRespVO {

    @Schema(description = "主键ID", requiredMode = Schema.RequiredMode.REQUIRED, example = "1")
    private Long id;

    @Schema(description = "配置键", requiredMode = Schema.RequiredMode.REQUIRED, example = "dm_daily_limit")
    private String configKey;

    @Schema(description = "配置值", requiredMode = Schema.RequiredMode.REQUIRED, example = "100")
    private String configValue;

    @Schema(description = "配置描述", example = "每日私信次数限制")
    private String description;

    @Schema(description = "创建时间", requiredMode = Schema.RequiredMode.REQUIRED)
    private LocalDateTime createTime;

}
