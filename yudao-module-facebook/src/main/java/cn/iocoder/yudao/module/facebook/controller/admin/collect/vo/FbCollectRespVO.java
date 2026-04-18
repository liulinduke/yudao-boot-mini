package cn.iocoder.yudao.module.facebook.controller.admin.collect.vo;

import io.swagger.v3.oas.annotations.media.Schema;
import lombok.*;
import java.util.*;
import org.springframework.format.annotation.DateTimeFormat;
import java.time.LocalDateTime;
import cn.idev.excel.annotation.*;
import cn.iocoder.yudao.framework.excel.core.annotations.DictFormat;
import cn.iocoder.yudao.framework.excel.core.convert.DictConvert;

@Schema(description = "管理后台 - FB采集任务 Response VO")
@Data
@ExcelIgnoreUnannotated
public class FbCollectRespVO {

    @Schema(description = "任务ID", requiredMode = Schema.RequiredMode.REQUIRED, example = "24906")
    @ExcelProperty("任务ID")
    private Long id;

    @Schema(description = "FB账号", example = "29913")
    @ExcelProperty("FB账号")
    private String fbAccount;

    @Schema(description = "任务类型 (pages/posts/users)", example = "2")
    @ExcelProperty(value = "任务类型 (pages/posts/users)", converter = DictConvert.class)
    @DictFormat("fb_search_type") // TODO 代码优化：建议设置到对应的 DictTypeConstants 枚举类中
    private Integer taskType;

    @Schema(description = "采集链接", requiredMode = Schema.RequiredMode.REQUIRED, example = "https://www.iocoder.cn")
    @ExcelProperty("采集链接")
    private String searchUrl;

    @Schema(description = "搜索类型(0链接 1关键词 )", example = "2")
    @ExcelProperty("搜索类型(0链接 1关键词 )")
    private Integer searchType;

    @Schema(description = "期望采集数量", example = "18285")
    @ExcelProperty("期望采集数量")
    private Integer expectedCount;

    @Schema(description = "采集间隔(秒)")
    @ExcelProperty("采集间隔(秒)")
    private Integer intervalSeconds;

    @Schema(description = "状态（0待执行 1采集中 2已完成 3已失败 4已取消）")
    @ExcelProperty(value = "状态（0待执行 1采集中 2已完成 3已失败 4已取消）", converter = DictConvert.class)
    @DictFormat("sys_collect_status") // TODO 代码优化：建议设置到对应的 DictTypeConstants 枚举类中
    private Integer status;

    @Schema(description = "已采集数量", example = "3037")
    @ExcelProperty("已采集数量")
    private Integer collectedCount;

    @Schema(description = "错误信息")
    @ExcelProperty("错误信息")
    private String errorMessage;

    @Schema(description = "开始时间")
    @ExcelProperty("开始时间")
    private LocalDateTime startTime;

    @Schema(description = "结束时间")
    @ExcelProperty("结束时间")
    private LocalDateTime endTime;

    @Schema(description = "备注", example = "你说的对")
    @ExcelProperty("备注")
    private String remark;

    @Schema(description = "创建时间")
    @ExcelProperty("创建时间")
    private LocalDateTime createTime;

}