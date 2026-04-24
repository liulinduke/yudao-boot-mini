package cn.iocoder.yudao.module.system.controller.admin.script.vo;

import io.swagger.v3.oas.annotations.media.Schema;
import lombok.*;
import javax.validation.constraints.*;

@Schema(description = "管理后台 - 话术分组新增/修改 Request VO")
@Data
public class FbScriptGroupSaveReqVO {

    @Schema(description = "分组ID", example = "1")
    private Long id;

    @Schema(description = "分组名称", requiredMode = Schema.RequiredMode.REQUIRED, example = "默认话术")
    @NotBlank(message = "分组名称不能为空")
    private String groupName;

    @Schema(description = "分组类型", example = "1")
    private Integer groupType;

    @Schema(description = "排序", example = "0")
    private Integer sort;

    @Schema(description = "备注", example = "备注信息")
    private String remark;

}
