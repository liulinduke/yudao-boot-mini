package cn.iocoder.yudao.module.facebook.controller.admin.globalconfig.vo;

import io.swagger.v3.oas.annotations.media.Schema;
import lombok.Data;

import javax.validation.constraints.NotBlank;

@Schema(description = "管理后台 - Facebook 全局配置保存 Request VO")
@Data
public class FbGlobalConfigSaveReqVO {

    @Schema(description = "主键ID", example = "1")
    private Long id;

    @Schema(description = "配置键", requiredMode = Schema.RequiredMode.REQUIRED, example = "dm_daily_limit")
    @NotBlank(message = "配置键不能为空")
    private String configKey;

    @Schema(description = "配置值", requiredMode = Schema.RequiredMode.REQUIRED, example = "100")
    @NotBlank(message = "配置值不能为空")
    private String configValue;

    @Schema(description = "配置描述", example = "每日私信次数限制")
    private String description;

}
