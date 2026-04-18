package cn.iocoder.yudao.module.facebook.controller.admin.collect.vo;

import io.swagger.v3.oas.annotations.media.Schema;
import lombok.*;
import java.util.*;
import javax.validation.constraints.*;
import org.springframework.format.annotation.DateTimeFormat;
import java.time.LocalDateTime;

@Schema(description = "管理后台 - FB采集任务新增/修改 Request VO")
@Data
public class FbCollectSaveReqVO {

    @Schema(description = "任务ID", requiredMode = Schema.RequiredMode.REQUIRED, example = "24906")
    private Long id;

    @Schema(description = "FB账号", example = "29913")
    private String fbAccount;

    @Schema(description = "任务类型 (pages/posts/users)", example = "2")
    private Integer taskType;

    @Schema(description = "采集链接", requiredMode = Schema.RequiredMode.REQUIRED, example = "https://www.iocoder.cn")
    @NotEmpty(message = "采集链接不能为空")
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
    private LocalDateTime startTime;

    @Schema(description = "结束时间")
    private LocalDateTime endTime;

    @Schema(description = "备注", example = "你说的对")
    private String remark;

}