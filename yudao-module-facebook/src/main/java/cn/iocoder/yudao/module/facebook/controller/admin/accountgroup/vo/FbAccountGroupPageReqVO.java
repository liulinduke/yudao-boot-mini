package cn.iocoder.yudao.module.facebook.controller.admin.accountgroup.vo;

import cn.iocoder.yudao.framework.common.pojo.PageParam;
import io.swagger.v3.oas.annotations.media.Schema;
import lombok.Data;
import lombok.EqualsAndHashCode;

@Schema(description = "管理后台 - Facebook 账号分组分页 Request VO")
@Data
@EqualsAndHashCode(callSuper = true)
public class FbAccountGroupPageReqVO extends PageParam {

    @Schema(description = "分组名称", example = "测试分组")
    private String groupName;

    @Schema(description = "状态：0禁用 1启用", example = "1")
    private Integer status;

}
