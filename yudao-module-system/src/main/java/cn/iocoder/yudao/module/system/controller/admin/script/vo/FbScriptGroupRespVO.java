package cn.iocoder.yudao.module.system.controller.admin.script.vo;

import io.swagger.v3.oas.annotations.media.Schema;
import lombok.*;
import java.time.LocalDateTime;

@Schema(description = "管理后台 - 话术分组 Response VO")
@Data
public class FbScriptGroupRespVO {

    @Schema(description = "分组ID", requiredMode = Schema.RequiredMode.REQUIRED, example = "1")
    private Long id;

    @Schema(description = "分组名称", requiredMode = Schema.RequiredMode.REQUIRED, example = "")
    private String groupName;

    @Schema(description = "分组类型", example = "1")
    private Integer groupType;

    @Schema(description = "排序", example = "0")
    private Integer sort;

    @Schema(description = "备注", example = "备注信息")
    private String remark;

    @Schema(description = "创建时间", requiredMode = Schema.RequiredMode.REQUIRED)
    private LocalDateTime createTime;

}
