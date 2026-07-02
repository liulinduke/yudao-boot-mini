package cn.iocoder.yudao.module.facebook.controller.admin.fbcollectpost.vo;

import io.swagger.v3.oas.annotations.media.Schema;
import lombok.*;
import java.util.*;
import org.springframework.format.annotation.DateTimeFormat;
import java.time.LocalDateTime;
import cn.idev.excel.annotation.*;

@Schema(description = "管理后台 - FB帖子采集结果 Response VO")
@Data
@ExcelIgnoreUnannotated
public class FbCollectPostRespVO {

    @Schema(description = "id", requiredMode = Schema.RequiredMode.REQUIRED, example = "21780")
    @ExcelProperty("id")
    private Long id;

    @Schema(description = "任务id", example = "32429")
    @ExcelProperty("任务id")
    private Long taskId;

    @Schema(description = "帖子唯一id", example = "6254")
    @ExcelProperty("帖子唯一id")
    private String itemId;

    @Schema(description = "发贴人")
    @ExcelProperty("发贴人")
    private String postUser;

    @Schema(description = "发帖人 Facebook 用户ID")
    @ExcelProperty("发帖人 Facebook 用户ID")
    private String postAuthorId;

    @Schema(description = "发帖人主页链接")
    @ExcelProperty("发帖人主页链接")
    private String postAuthorUrl;

    @Schema(description = "帖子链接", example = "https://www.iocoder.cn")
    @ExcelProperty("帖子链接")
    private String url;

    @Schema(description = "帖子来源")
    @ExcelProperty("帖子来源")
    private String fromResource;

    @Schema(description = "群名", example = "王五")
    @ExcelProperty("群名")
    private String groupName;

    @Schema(description = "系统分组", example = "13685")
    @ExcelProperty("系统分组")
    private Long sysGroupId;

    @Schema(description = "系统用户id", example = "25890")
    @ExcelProperty("系统用户id")
    private Long userId;

    @Schema(description = "部门id", example = "2830")
    @ExcelProperty("部门id")
    private Long deptId;

    @Schema(description = "采集回来的群id", example = "17546")
    @ExcelProperty("采集回来的群id")
    private Long groupId;

    @Schema(description = "转发数", example = "6926")
    @ExcelProperty("转发数")
    private Integer reshareCount;

    @Schema(description = "评论数", example = "16490")
    @ExcelProperty("评论数")
    private Integer commentCount;

    @Schema(description = "点赞数", example = "32737")
    @ExcelProperty("点赞数")
    private Integer reactionCount;

    @Schema(description = "截流次数", example = "18927")
    @ExcelProperty("截流次数")
    private Integer usedCount;

    @Schema(description = "帖子内容")
    @ExcelProperty("帖子内容")
    private String postContent;

    @Schema(description = "FB账号", example = "14230")
    @ExcelProperty("FB账号")
    private String fbAccount;

    @Schema(description = "帖子创建时间")
    @ExcelProperty("帖子创建时间")
    private LocalDateTime postCreateTime;

    @Schema(description = "AI标签")
    @ExcelProperty("AI标签")
    private String aiTags;

    @Schema(description = "意向等级")
    @ExcelProperty("意向等级")
    private String intentLevel;

    @Schema(description = "意向判断理由")
    @ExcelProperty("意向判断理由")
    private String intentReason;

    @Schema(description = "情绪")
    @ExcelProperty("情绪")
    private String sentiment;

    @Schema(description = "线索类型")
    @ExcelProperty("线索类型")
    private String leadType;

    @Schema(description = "国家")
    @ExcelProperty("国家")
    private String country;

    @Schema(description = "语言")
    @ExcelProperty("语言")
    private String language;

    @Schema(description = "产品相关度")
    @ExcelProperty("产品相关度")
    private Integer productRelevanceScore;

    @Schema(description = "AI摘要")
    @ExcelProperty("AI摘要")
    private String aiSummary;

    @Schema(description = "最近AI分析时间")
    @ExcelProperty("最近AI分析时间")
    private LocalDateTime lastAiAnalyzeTime;

    @Schema(description = "触达状态")
    @ExcelProperty("触达状态")
    private String touchStatus;

    @Schema(description = "最近触达时间")
    @ExcelProperty("最近触达时间")
    private LocalDateTime lastTouchTime;

}
