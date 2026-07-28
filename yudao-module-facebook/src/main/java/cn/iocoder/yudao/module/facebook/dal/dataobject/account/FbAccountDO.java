package cn.iocoder.yudao.module.facebook.dal.dataobject.account;

import cn.iocoder.yudao.framework.tenant.core.db.TenantBaseDO;
import lombok.*;
import java.util.*;
import java.time.LocalDateTime;
import java.time.LocalDateTime;
import java.time.LocalDateTime;
import com.baomidou.mybatisplus.annotation.*;

/**
 * FB账号 DO
 *
 * @author 芋道源码
 */
@TableName("facebook_account")
@KeySequence("facebook_account_seq") // 用于 Oracle、PostgreSQL、Kingbase、DB2、H2 数据库的主键自增。如果是 MySQL 等数据库，可不写。
@Data
@EqualsAndHashCode(callSuper = true)
@ToString(callSuper = true)
@Builder
@NoArgsConstructor
@AllArgsConstructor
public class FbAccountDO extends TenantBaseDO {

    /**
     * id
     */
    @TableId
    private Long id;
    /**
     * FB账号
     */
    private String fbAccount;
    /**
     * 密码
     */
    private String password;
    /**
     * 地区
     */
    private String area;
    /**
     * 好友数
     */
    private Integer friends;
    /**
     * 账户分组
     */
    private Long groupId;
    /**
     * 账户状态
     */
    private Boolean status;
    /**
     * 备注
     */
    private String remark;
    /**
     * cookie
     */
    private String cookie;
    /**
     * 用户代理
     */
    private String userAgent;
    /**
     * 2FA
     */
    private String tfa;
    private String loginStatus;
    private String loginErrorReason;
    private LocalDateTime lastLoginTime;
    /** Facebook头像地址 */
    private String avatarUrl;
    /** Facebook主页封面地址 */
    private String coverUrl;
    /** Facebook主页昵称 */
    private String profileNickname;
    /** Facebook个人签名 */
    private String profileSignature;
    /** 资料上传状态 */
    private String profileUpdateStatus;
    /** 资料上传时间 */
    private LocalDateTime profileUpdateTime;
    /** 资料上传失败原因 */
    private String profileUpdateError;
    /**
     * 邮件信息
     */
    private String email;
    /**
     * 邮箱密码
     */
    private String emailPassword;
    /**
     * 设备ID
     */
    private Long deviceId;
    /**
     * 设备名称
     */
    private String deviceName;
    /**
     * 异常原因
     */
    private String reason;
    /**
     * 代理
     */
    private String proxy;
    /**
     * 代理ID
     */
    private Long proxyId;
    /**
     * 语言设置：1-英文，2-中文
     */
    private Integer language;
    /** Facebook界面语言代码，例如 en_US、zh_CN。 */
    private String languageCode;
    /**
     * 注册日期
     */
    private LocalDateTime creationDate;


}
