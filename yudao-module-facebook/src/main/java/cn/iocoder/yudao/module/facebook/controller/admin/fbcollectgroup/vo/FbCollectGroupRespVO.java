package cn.iocoder.yudao.module.facebook.controller.admin.fbcollectgroup.vo;

import io.swagger.v3.oas.annotations.media.Schema;
import lombok.*;
import java.util.*;
import org.springframework.format.annotation.DateTimeFormat;
import java.time.LocalDateTime;
import cn.idev.excel.annotation.*;
import cn.iocoder.yudao.framework.excel.core.annotations.DictFormat;
import cn.iocoder.yudao.framework.excel.core.convert.DictConvert;

@Schema(description = "管理后台 - FB群组采集结果 Response VO")
@Data
@ExcelIgnoreUnannotated
public class FbCollectGroupRespVO {

    @Schema(description = "id", requiredMode = Schema.RequiredMode.REQUIRED, example = "11890")
    @ExcelProperty("id")
    private Long id;

    @Schema(description = "任务id", example = "1954")
    @ExcelProperty("任务id")
    private Long taskId;

    @Schema(description = "群组名称", requiredMode = Schema.RequiredMode.REQUIRED, example = "芋艿")
    @ExcelProperty("群组名称")
    private String groupName;

    @Schema(description = "群组链接")
    @ExcelProperty("群组链接")
    private String url;

    @Schema(description = "是否公开", example = "2")
    @ExcelProperty(value = "是否公开", converter = DictConvert.class)
    @DictFormat("fb_group_type") // TODO 代码优化：建议设置到对应的 DictTypeConstants 枚举类中
    private String type;

    @Schema(description = "成员数量")
    @ExcelProperty("成员数量")
    private Long memberQuantity;

    @Schema(description = "群活跃")
    @ExcelProperty("群活跃")
    private String activeQuantity;

    @Schema(description = "分组", example = "4309")
    @ExcelProperty("分组")
    private Long sysGroupId;

    @Schema(description = "备注", example = "随便")
    @ExcelProperty("备注")
    private String remark;

    @Schema(description = "系统用户", example = "14745")
    @ExcelProperty("系统用户")
    private Long userId;

    @Schema(description = "部门", example = "19686")
    @ExcelProperty("部门")
    private Long deptId;

    @Schema(description = "采集回来的群id", example = "4168")
    @ExcelProperty("采集回来的群id")
    private Long groupId;

    @Schema(description = "加组次数")
    @ExcelProperty("加组次数")
    private Integer joinGroupTimes;

    @Schema(description = "评论次数")
    @ExcelProperty("评论次数")
    private Integer commentTimes;

    @Schema(description = "发帖次数")
    @ExcelProperty("发帖次数")
    private Integer postTimes;

    @Schema(description = "创建时间")
    @ExcelProperty("创建时间")
    private LocalDateTime createTime;

}