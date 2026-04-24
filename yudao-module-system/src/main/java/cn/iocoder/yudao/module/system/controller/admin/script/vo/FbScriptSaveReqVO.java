package cn.iocoder.yudao.module.system.controller.admin.script.vo;

import io.swagger.v3.oas.annotations.media.Schema;
import lombok.*;
import javax.validation.constraints.*;

@Schema(description = "管理后台 - 话术内容新增/修改 Request VO")
@Data
public class FbScriptSaveReqVO {

    @Schema(description = "话术ID", example = "1")
    private Long id;

    @Schema(description = "分组ID", requiredMode = Schema.RequiredMode.REQUIRED, example = "1")
    @NotNull(message = "分组ID不能为空")
    private Long groupId;

    @Schema(description = "话术标题", example = "欢迎话术")
    private String scriptTitle;

    @Schema(description = "话术内容", requiredMode = Schema.RequiredMode.REQUIRED, example = "您好")
    @NotBlank(message = "话术内容不能为空")
    private String scriptContent;

    @Schema(description = "内容类型", example = "1")
    private Integer contentType;

    @Schema(description = "附件列表")
    private String attachments;

    @Schema(description = "是否按顺序发", example = "false")
    private Boolean sendSequence;

    @Schema(description = "排序", example = "0")
    private Integer sort;

    @Schema(description = "备注", example = "备注信息")
    private String remark;

}
