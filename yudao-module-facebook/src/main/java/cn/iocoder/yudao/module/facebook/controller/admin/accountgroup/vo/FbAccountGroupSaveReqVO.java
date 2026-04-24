package cn.iocoder.yudao.module.facebook.controller.admin.accountgroup.vo;

import io.swagger.v3.oas.annotations.media.Schema;
import lombok.Data;

import javax.validation.constraints.NotBlank;

@Schema(description = "管理后台 - Facebook 账号分组保存 Request VO")
@Data
public class FbAccountGroupSaveReqVO {

    @Schema(description = "主键ID", example = "1")
    private Long id;

    @Schema(description = "分组名称", requiredMode = Schema.RequiredMode.REQUIRED, example = "测试分组")
    @NotBlank(message = "分组名称不能为空")
    private String groupName;

    @Schema(description = "分组描述", example = "这是一个测试分组")
    private String description;

    @Schema(description = "排序", example = "1")
    private Integer sort;

    @Schema(description = "状态：0禁用 1启用", example = "1")
    private Integer status;

}
