package cn.iocoder.yudao.module.facebook.controller.admin.fbcollectgroup.vo;

import io.swagger.v3.oas.annotations.media.Schema;
import lombok.*;
import java.util.*;
import javax.validation.constraints.*;

@Schema(description = "管理后台 - FB群组采集结果新增/修改 Request VO")
@Data
public class FbCollectGroupSaveReqVO {

    @Schema(description = "id", requiredMode = Schema.RequiredMode.REQUIRED, example = "11890")
    private Long id;

    @Schema(description = "任务id", example = "1954")
    private Long taskId;

    @Schema(description = "群组名称", requiredMode = Schema.RequiredMode.REQUIRED, example = "芋艿")
    @NotEmpty(message = "群组名称不能为空")
    private String groupName;

    @Schema(description = "群组链接")
    private String url;

    @Schema(description = "是否公开", example = "2")
    private String type;

    @Schema(description = "成员数量")
    private Long memberQuantity;

    @Schema(description = "群活跃")
    private String activeQuantity;

    @Schema(description = "分组", example = "4309")
    private Long sysGroupId;

    @Schema(description = "备注", example = "随便")
    private String remark;

    @Schema(description = "系统用户", example = "14745")
    private Long userId;

    @Schema(description = "部门", example = "19686")
    private Long deptId;

    @Schema(description = "采集回来的群id", example = "4168")
    private Long groupId;

    @Schema(description = "加组次数")
    private Integer joinGroupTimes;

    @Schema(description = "评论次数")
    private Integer commentTimes;

    @Schema(description = "发帖次数")
    private Integer postTimes;

}