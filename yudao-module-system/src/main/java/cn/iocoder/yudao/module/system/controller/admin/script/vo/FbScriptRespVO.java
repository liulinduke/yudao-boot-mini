package cn.iocoder.yudao.module.system.controller.admin.script.vo;

import io.swagger.v3.oas.annotations.media.Schema;
import lombok.*;
import java.time.LocalDateTime;

@Schema(description = "管理后台 - 话术内容 Response VO")
@Data
public class FbScriptRespVO {

    @Schema(description = "话术ID", requiredMode = Schema.RequiredMode.REQUIRED, example = "1")
    private Long id;

    @Schema(description = "分组ID", requiredMode = Schema.RequiredMode.REQUIRED, example = "1")
    private Long groupId;

    @Schema(description = "话术标题", example = "欢迎话术")
    private String scriptTitle;

    @Schema(description = "话术内容", requiredMode = Schema.RequiredMode.REQUIRED, example = "您好")
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

    @Schema(description = "创建时间", requiredMode = Schema.RequiredMode.REQUIRED)
    private LocalDateTime createTime;

}
