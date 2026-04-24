package cn.iocoder.yudao.module.facebook.controller.admin.dmtask.vo;

import cn.iocoder.yudao.framework.common.pojo.PageParam;
import io.swagger.v3.oas.annotations.media.Schema;
import lombok.Data;
import lombok.EqualsAndHashCode;

@Schema(description = "管理后台 - Facebook 群发私信任务分页 Request VO")
@Data
@EqualsAndHashCode(callSuper = true)
public class FbDmTaskPageReqVO extends PageParam {

    @Schema(description = "任务名称", example = "测试任务")
    private String taskName;

    @Schema(description = "状态：0待执行 1执行中 2已完成 3失败 4已取消", example = "0")
    private Integer status;

}
