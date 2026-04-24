package cn.iocoder.yudao.module.system.controller.admin.script.vo;

import io.swagger.v3.oas.annotations.media.Schema;
import lombok.*;
import cn.iocoder.yudao.framework.common.pojo.PageParam;

@Schema(description = "管理后台 - 话术分组分页 Request VO")
@Data
public class FbScriptGroupPageReqVO extends PageParam {

    @Schema(description = "分组名称", example = "默认话术")
    private String groupName;

    @Schema(description = "分组类型", example = "1")
    private Integer groupType;

}
