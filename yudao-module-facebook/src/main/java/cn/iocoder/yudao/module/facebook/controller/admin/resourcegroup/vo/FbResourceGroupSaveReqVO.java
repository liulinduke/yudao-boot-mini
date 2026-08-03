package cn.iocoder.yudao.module.facebook.controller.admin.resourcegroup.vo;

import io.swagger.v3.oas.annotations.media.Schema;
import jakarta.validation.constraints.NotBlank;
import lombok.Data;

@Data
public class FbResourceGroupSaveReqVO {
    private Long id;
    @NotBlank(message = "分组名称不能为空")
    private String name;
    @Schema(description = "LEAD/GROUP/POST")
    @NotBlank(message = "资源类型不能为空")
    private String resourceType;
}
