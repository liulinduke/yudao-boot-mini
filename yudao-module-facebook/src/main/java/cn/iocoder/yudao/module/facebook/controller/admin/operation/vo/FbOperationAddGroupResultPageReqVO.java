package cn.iocoder.yudao.module.facebook.controller.admin.operation.vo;

import cn.iocoder.yudao.framework.common.pojo.PageParam;
import io.swagger.v3.oas.annotations.media.Schema;
import lombok.*;

@Schema(description = "管理后台 - 加组结果分页 Request VO")
@Data
@EqualsAndHashCode(callSuper = true)
public class FbOperationAddGroupResultPageReqVO extends PageParam {

    @Schema(description = "任务ID", example = "1024")
    private Long taskId;

    @Schema(description = "明细ID", example = "2048")
    private Long detailId;

    @Schema(description = "Facebook账号ID", example = "10001234567890")
    private String accountId;

    @Schema(description = "加组状态（0-待处理 1-成功 2-失败 3-已加入/待审核）", example = "1")
    private Integer joinStatus;

    @Schema(description = "群组ID", example = "987654321")
    private String groupId;

    @Schema(description = "群组名称", example = "测试群组")
    private String groupName;

}
