package cn.iocoder.yudao.module.facebook.controller.admin.resourcegroup.vo;

import lombok.Data;

@Data
public class FbResourceGroupRespVO {
    private Long id;
    private String name;
    private String resourceType;
    private Boolean isDefault;
}
