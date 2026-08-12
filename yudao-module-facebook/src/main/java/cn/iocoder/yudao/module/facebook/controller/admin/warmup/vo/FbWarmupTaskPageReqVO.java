package cn.iocoder.yudao.module.facebook.controller.admin.warmup.vo;

import cn.iocoder.yudao.framework.common.pojo.PageParam;
import lombok.Data;
import lombok.EqualsAndHashCode;

@Data
@EqualsAndHashCode(callSuper = true)
public class FbWarmupTaskPageReqVO extends PageParam {
    private Integer status;
}
