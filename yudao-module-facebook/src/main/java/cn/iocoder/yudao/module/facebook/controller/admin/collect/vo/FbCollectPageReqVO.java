package cn.iocoder.yudao.module.facebook.controller.admin.collect.vo;

import lombok.*;
import java.util.*;
import io.swagger.v3.oas.annotations.media.Schema;
import cn.iocoder.yudao.framework.common.pojo.PageParam;
import org.springframework.format.annotation.DateTimeFormat;
import java.time.LocalDateTime;

import static cn.iocoder.yudao.framework.common.util.date.DateUtils.FORMAT_YEAR_MONTH_DAY_HOUR_MINUTE_SECOND;

@Schema(description = "管理后台 - FB采集任务分页 Request VO")
@Data
public class FbCollectPageReqVO extends PageParam {

    @Schema(description = "FB账号", example = "29913")
    private String fbAccount;

    @Schema(description = "任务类型 (pages/posts/users)", example = "2")
    private Integer taskType;

    @Schema(description = "采集链接", example = "https://www.iocoder.cn")
    private String searchUrl;

    @Schema(description = "搜索类型(0链接 1关键词 )", example = "2")
    private Integer searchType;

    @Schema(description = "期望采集数量", example = "18285")
    private Integer expectedCount;

    @Schema(description = "采集间隔(秒)")
    private Integer intervalSeconds;

    @Schema(description = "状态（0待执行 1采集中 2已完成 3已失败 4已取消）")
    private Integer status;

    @Schema(description = "已采集数量", example = "3037")
    private Integer collectedCount;

    @Schema(description = "错误信息")
    private String errorMessage;

    @Schema(description = "开始时间")
    @DateTimeFormat(pattern = FORMAT_YEAR_MONTH_DAY_HOUR_MINUTE_SECOND)
    private LocalDateTime[] startTime;

    @Schema(description = "结束时间")
    @DateTimeFormat(pattern = FORMAT_YEAR_MONTH_DAY_HOUR_MINUTE_SECOND)
    private LocalDateTime[] endTime;

    @Schema(description = "备注", example = "你说的对")
    private String remark;

    @Schema(description = "创建时间")
    @DateTimeFormat(pattern = FORMAT_YEAR_MONTH_DAY_HOUR_MINUTE_SECOND)
    private LocalDateTime[] createTime;

}