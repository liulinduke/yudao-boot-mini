package cn.iocoder.yudao.module.facebook.controller.admin.fbcollectgroup.vo;

import lombok.*;
import java.util.*;
import io.swagger.v3.oas.annotations.media.Schema;
import cn.iocoder.yudao.framework.common.pojo.PageParam;
import org.springframework.format.annotation.DateTimeFormat;
import java.time.LocalDateTime;

import static cn.iocoder.yudao.framework.common.util.date.DateUtils.FORMAT_YEAR_MONTH_DAY_HOUR_MINUTE_SECOND;

@Schema(description = "管理后台 - FB群组采集结果分页 Request VO")
@Data
public class FbCollectGroupPageReqVO extends PageParam {

    @Schema(description = "任务id", example = "1954")
    private Long taskId;

    @Schema(description = "群组名称", example = "芋艿")
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

    @Schema(description = "创建时间")
    @DateTimeFormat(pattern = FORMAT_YEAR_MONTH_DAY_HOUR_MINUTE_SECOND)
    private LocalDateTime[] createTime;

}