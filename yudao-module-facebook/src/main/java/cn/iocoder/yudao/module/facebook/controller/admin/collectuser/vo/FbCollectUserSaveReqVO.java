package cn.iocoder.yudao.module.facebook.controller.admin.collectuser.vo;

import io.swagger.v3.oas.annotations.media.Schema;
import lombok.*;
import java.util.*;
import javax.validation.constraints.*;
import org.springframework.format.annotation.DateTimeFormat;
import java.time.LocalDateTime;

@Schema(description = "管理后台 - FB用户采集结果新增/修改 Request VO")
@Data
public class FbCollectUserSaveReqVO {

    @Schema(description = "Facebook用户ID(业务ID)", requiredMode = Schema.RequiredMode.REQUIRED, example = "61578035879774")
    private String id;

    @Schema(description = "用户名称(Facebook返回的name)", example = "ADC")
    private String name;

    @Schema(description = "任务ID", example = "15717")
    private Long taskId;

    @Schema(description = "系统用户ID", example = "6714")
    private Long userId;

    @Schema(description = "部门ID", example = "9848")
    private Long deptId;

    @Schema(description = "FB账号", example = "20683")
    private String fbAccount;

    @Schema(description = "Facebook用户ID", example = "17574")
    private String fbUserId;

    @Schema(description = "用户名称", example = "李四")
    private String userName;

    @Schema(description = "头像URL")
    private String avatar;

    @Schema(description = "主页链接", example = "https://www.iocoder.cn")
    private String url;

    @Schema(description = "数据类型(0个人 1公共)", example = "2")
    private Integer dataType;

    @Schema(description = "粉丝数")
    private Integer followers;

    @Schema(description = "所在地")
    private String city;

    @Schema(description = "居住地")
    private String location;

    @Schema(description = "家乡")
    private String hometown;

    @Schema(description = "手机1")
    private String phonenumber;

    @Schema(description = "手机2")
    private String phonenumber2;

    @Schema(description = "邮箱1")
    private String email;

    @Schema(description = "邮箱2")
    private String email2;

    @Schema(description = "微信")
    private String wechat;

    @Schema(description = "WhatsApp")
    private String whatsapp;

    @Schema(description = "Line")
    private String line;

    @Schema(description = "社交网站")
    private String website;

    @Schema(description = "签名/状态", example = "2")
    private String profileStatus;

    @Schema(description = "片段文本(Facebook返回的snippet)", example = "Ikuti")
    private String snippet;

    @Schema(description = "类别/认证信息(Facebook返回的category)", example = "Organisasi Politik")
    private String category;

    @Schema(description = "语言")
    private String language;

    @Schema(description = "性别")
    private String gender;

    @Schema(description = "婚姻状况")
    private String relationship;

    @Schema(description = "工作经历")
    private String workExperience;

    @Schema(description = "学历")
    private String education;

    @Schema(description = "最近发帖时间")
    private LocalDateTime lastPostTime;

    @Schema(description = "最近帖子摘要")
    private String lastPostSummary;

    @Schema(description = "分组ID", example = "2232")
    private Long groupId;

    @Schema(description = "数据来源")
    private String fromResource;

    @Schema(description = "配置信息")
    private String config;

    @Schema(description = "同步时间")
    private LocalDateTime syncTime;

}