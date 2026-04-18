package cn.iocoder.yudao.module.facebook.controller.admin.account.vo;

import lombok.*;
import java.util.*;
import io.swagger.v3.oas.annotations.media.Schema;
import cn.iocoder.yudao.framework.common.pojo.PageParam;
import org.springframework.format.annotation.DateTimeFormat;
import java.time.LocalDateTime;

import static cn.iocoder.yudao.framework.common.util.date.DateUtils.FORMAT_YEAR_MONTH_DAY_HOUR_MINUTE_SECOND;

@Schema(description = "管理后台 - FB账号分页 Request VO")
@Data
public class FbAccountPageReqVO extends PageParam {

    @Schema(description = "FB账号", example = "17839")
    private String fbAccount;

    @Schema(description = "密码")
    private String password;

    @Schema(description = "地区")
    private String area;

    @Schema(description = "好友数")
    private Integer friends;

    @Schema(description = "账户分组", example = "29576")
    private Long groupId;

    @Schema(description = "账户状态", example = "2")
    private Boolean status;

    @Schema(description = "备注", example = "你说的对")
    private String remark;

    @Schema(description = "cookie")
    private String cookie;

    @Schema(description = "用户代理")
    private String userAgent;

    @Schema(description = "2FA")
    private String tfa;

    @Schema(description = "邮件信息")
    private String email;

    @Schema(description = "邮箱密码")
    private String emailPassword;

    @Schema(description = "设备ID", example = "31519")
    private Long deviceId;

    @Schema(description = "设备名称", example = "芋艿")
    private String deviceName;

    @Schema(description = "异常原因", example = "不对")
    private String reason;

    @Schema(description = "代理")
    private String proxy;

    @Schema(description = "代理ID", example = "11034")
    private Long proxyId;

    @Schema(description = "注册日期")
    @DateTimeFormat(pattern = FORMAT_YEAR_MONTH_DAY_HOUR_MINUTE_SECOND)
    private LocalDateTime[] creationDate;

    @Schema(description = "创建时间")
    @DateTimeFormat(pattern = FORMAT_YEAR_MONTH_DAY_HOUR_MINUTE_SECOND)
    private LocalDateTime[] createTime;

}