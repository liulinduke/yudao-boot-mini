package cn.iocoder.yudao.module.system.controller.admin.script.vo;

import io.swagger.v3.oas.annotations.media.Schema;
import lombok.*;
import cn.iocoder.yudao.framework.common.pojo.PageParam;

@Schema(description = "管理后台 - 话术内容分页 Request VO")
@Data
public class FbScriptPageReqVO extends PageParam {

    @Schema(description = "分组ID", example = "1")
    private Long groupId;

    @Schema(description = "话术标题", example = "欢迎话术")
    private String scriptTitle;

    @Schema(description = "话术内容", example = "您好")
    private String scriptContent;

    @Schema(description = "内容类型", example = "1")
    private Integer contentType;

}
