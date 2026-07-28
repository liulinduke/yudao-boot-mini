package cn.iocoder.yudao.module.facebook.controller.admin.account.vo;

import com.fasterxml.jackson.databind.annotation.JsonSerialize;
import com.fasterxml.jackson.databind.ser.std.ToStringSerializer;
import io.swagger.v3.oas.annotations.media.Schema;
import lombok.*;
import java.util.*;
import org.springframework.format.annotation.DateTimeFormat;
import java.time.LocalDateTime;
import cn.idev.excel.annotation.*;

@Schema(description = "管理后台 - FB账号 Response VO")
@Data
@ExcelIgnoreUnannotated
public class FbAccountRespVO {

    @Schema(description = "id", requiredMode = Schema.RequiredMode.REQUIRED, example = "9733")
    @ExcelProperty("id")
    @JsonSerialize(using = ToStringSerializer.class)
    private Long id;

    @Schema(description = "FB账号", requiredMode = Schema.RequiredMode.REQUIRED, example = "17839")
    @ExcelProperty("FB账号")
    private String fbAccount;

    @Schema(description = "密码")
    @ExcelProperty("密码")
    private String password;

    @Schema(description = "地区")
    @ExcelProperty("地区")
    private String area;

    @Schema(description = "好友数")
    @ExcelProperty("好友数")
    private Integer friends;

    @Schema(description = "账户分组", example = "29576")
    @ExcelProperty("账户分组")
    private Long groupId;

    @Schema(description = "账户状态", example = "2")
    @ExcelProperty("账户状态")
    private Boolean status;

    @Schema(description = "备注", example = "你说的对")
    @ExcelProperty("备注")
    private String remark;

    @Schema(description = "cookie")
    @ExcelProperty("cookie")
    private String cookie;

    @Schema(description = "用户代理")
    @ExcelProperty("用户代理")
    private String userAgent;

    @Schema(description = "2FA")
    @ExcelProperty("2FA")
    private String tfa;
    private String loginStatus;
    private String loginErrorReason;
    private LocalDateTime lastLoginTime;
    private String avatarUrl;
    private String coverUrl;
    private String profileNickname;
    private String profileSignature;
    private String profileUpdateStatus;
    private LocalDateTime profileUpdateTime;
    private String profileUpdateError;

    @Schema(description = "邮件信息")
    @ExcelProperty("邮件信息")
    private String email;

    @Schema(description = "邮箱密码")
    @ExcelProperty("邮箱密码")
    private String emailPassword;

    @Schema(description = "设备ID", example = "31519")
    @ExcelProperty("设备ID")
    @JsonSerialize(using = ToStringSerializer.class)
    private Long deviceId;

    @Schema(description = "设备名称", example = "芋艿")
    @ExcelProperty("设备名称")
    private String deviceName;

    @Schema(description = "异常原因", example = "不对")
    @ExcelProperty("异常原因")
    private String reason;

    @Schema(description = "代理")
    @ExcelProperty("代理")
    private String proxy;

    @Schema(description = "代理ID", example = "11034")
    @ExcelProperty("代理ID")
    private Long proxyId;

    @Schema(description = "语言设置：1-英文，2-中文", example = "1")
    @ExcelProperty("语言设置")
    private Integer language;

    @Schema(description = "Facebook界面语言代码", example = "en_US")
    @ExcelProperty("Facebook界面语言代码")
    private String languageCode;

    @Schema(description = "注册日期")
    @ExcelProperty("注册日期")
    private LocalDateTime creationDate;

    @Schema(description = "创建时间")
    @ExcelProperty("创建时间")
    private LocalDateTime createTime;

}
