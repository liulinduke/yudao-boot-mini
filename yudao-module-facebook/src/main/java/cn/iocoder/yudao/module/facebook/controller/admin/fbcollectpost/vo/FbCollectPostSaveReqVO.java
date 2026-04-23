package cn.iocoder.yudao.module.facebook.controller.admin.fbcollectpost.vo;

import io.swagger.v3.oas.annotations.media.Schema;
import lombok.*;
import java.util.*;
import javax.validation.constraints.*;
import org.springframework.format.annotation.DateTimeFormat;
import java.time.LocalDateTime;

@Schema(description = "管理后台 - FB帖子采集结果新增/修改 Request VO")
@Data
public class FbCollectPostSaveReqVO {

    @Schema(description = "id", requiredMode = Schema.RequiredMode.REQUIRED, example = "21780")
    private Long id;

    @Schema(description = "任务id", example = "32429")
    private Long taskId;

    @Schema(description = "帖子唯一id", example = "6254")
    private String itemId;

    @Schema(description = "发贴人")
    private String postUser;

    @Schema(description = "帖子链接", example = "https://www.iocoder.cn")
    private String url;

    @Schema(description = "帖子来源")
    private String fromResource;

    @Schema(description = "群名", example = "王五")
    private String groupName;

    @Schema(description = "系统分组", example = "13685")
    private Long sysGroupId;

    @Schema(description = "系统用户id", example = "25890")
    private Long userId;

    @Schema(description = "部门id", example = "2830")
    private Long deptId;

    @Schema(description = "采集回来的群id", example = "17546")
    private Long groupId;

    @Schema(description = "转发数", example = "6926")
    private Integer reshareCount;

    @Schema(description = "评论数", example = "16490")
    private Integer commentCount;

    @Schema(description = "点赞数", example = "32737")
    private Integer reactionCount;

    @Schema(description = "截流次数", example = "18927")
    private Integer usedCount;

    @Schema(description = "帖子内容")
    private String postContent;

    @Schema(description = "FB账号", example = "14230")
    private String fbAccount;

    @Schema(description = "帖子创建时间")
    private LocalDateTime postCreateTime;

}