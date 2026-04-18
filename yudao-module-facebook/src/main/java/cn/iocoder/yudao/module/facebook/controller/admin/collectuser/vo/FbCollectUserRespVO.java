package cn.iocoder.yudao.module.facebook.controller.admin.collectuser.vo;

import io.swagger.v3.oas.annotations.media.Schema;
import lombok.*;
import java.util.*;
import org.springframework.format.annotation.DateTimeFormat;
import java.time.LocalDateTime;
import cn.idev.excel.annotation.*;
import cn.iocoder.yudao.framework.excel.core.annotations.DictFormat;
import cn.iocoder.yudao.framework.excel.core.convert.DictConvert;

@Schema(description = "管理后台 - FB用户采集结果 Response VO")
@Data
@ExcelIgnoreUnannotated
public class FbCollectUserRespVO {

    @Schema(description = "结果ID", requiredMode = Schema.RequiredMode.REQUIRED, example = "31032")
    @ExcelProperty("结果ID")
    private Long id;

    @Schema(description = "任务ID", requiredMode = Schema.RequiredMode.REQUIRED, example = "15717")
    @ExcelProperty("任务ID")
    private Long taskId;

    @Schema(description = "系统用户ID", example = "6714")
    @ExcelProperty("系统用户ID")
    private Long userId;

    @Schema(description = "部门ID", example = "9848")
    @ExcelProperty("部门ID")
    private Long deptId;

    @Schema(description = "FB账号", requiredMode = Schema.RequiredMode.REQUIRED, example = "20683")
    @ExcelProperty("FB账号")
    private String fbAccount;

    @Schema(description = "Facebook用户ID", example = "17574")
    @ExcelProperty("Facebook用户ID")
    private String fbUserId;

    @Schema(description = "用户名称", example = "李四")
    @ExcelProperty("用户名称")
    private String userName;

    @Schema(description = "主页链接", example = "https://www.iocoder.cn")
    @ExcelProperty("主页链接")
    private String url;

    @Schema(description = "数据类型(0个人 1公共)", example = "2")
    @ExcelProperty(value = "数据类型(0个人 1公共)", converter = DictConvert.class)
    @DictFormat("fb_page_type") // TODO 代码优化：建议设置到对应的 DictTypeConstants 枚举类中
    private Integer dataType;

    @Schema(description = "粉丝数")
    @ExcelProperty("粉丝数")
    private Integer followers;

    @Schema(description = "所在地")
    @ExcelProperty("所在地")
    private String city;

    @Schema(description = "居住地")
    @ExcelProperty("居住地")
    private String location;

    @Schema(description = "家乡")
    @ExcelProperty("家乡")
    private String hometown;

    @Schema(description = "手机1")
    @ExcelProperty("手机1")
    private String phonenumber;

    @Schema(description = "手机2")
    @ExcelProperty("手机2")
    private String phonenumber2;

    @Schema(description = "邮箱1")
    @ExcelProperty("邮箱1")
    private String email;

    @Schema(description = "邮箱2")
    @ExcelProperty("邮箱2")
    private String email2;

    @Schema(description = "微信")
    @ExcelProperty("微信")
    private String wechat;

    @Schema(description = "WhatsApp")
    @ExcelProperty("WhatsApp")
    private String whatsapp;

    @Schema(description = "Line")
    @ExcelProperty("Line")
    private String line;

    @Schema(description = "社交网站")
    @ExcelProperty("社交网站")
    private String website;

    @Schema(description = "签名/状态", example = "2")
    @ExcelProperty("签名/状态")
    private String profileStatus;

    @Schema(description = "语言")
    @ExcelProperty("语言")
    private String language;

    @Schema(description = "性别")
    @ExcelProperty("性别")
    private String gender;

    @Schema(description = "婚姻状况")
    @ExcelProperty("婚姻状况")
    private String relationship;

    @Schema(description = "工作经历")
    @ExcelProperty("工作经历")
    private String workExperience;

    @Schema(description = "学历")
    @ExcelProperty("学历")
    private String education;

    @Schema(description = "最近发帖时间")
    @ExcelProperty("最近发帖时间")
    private LocalDateTime lastPostTime;

    @Schema(description = "最近帖子摘要")
    @ExcelProperty("最近帖子摘要")
    private String lastPostSummary;

    @Schema(description = "分组ID", example = "2232")
    @ExcelProperty("分组ID")
    private Long groupId;

    @Schema(description = "数据来源")
    @ExcelProperty("数据来源")
    private String fromResource;

    @Schema(description = "配置信息")
    @ExcelProperty("配置信息")
    private String config;

    @Schema(description = "同步时间")
    @ExcelProperty("同步时间")
    private LocalDateTime syncTime;

    @Schema(description = "创建时间")
    @ExcelProperty("创建时间")
    private LocalDateTime createTime;

}