package cn.iocoder.yudao.module.facebook.dal.dataobject.collectuser;

import lombok.*;
import java.util.*;
import java.time.LocalDateTime;
import java.time.LocalDateTime;
import java.time.LocalDateTime;
import java.time.LocalDateTime;
import com.baomidou.mybatisplus.annotation.*;
import cn.iocoder.yudao.framework.mybatis.core.dataobject.BaseDO;

/**
 * FB用户采集结果 DO
 *
 * @author jacky
 */
@TableName("fb_collect_user")
@KeySequence("fb_collect_user_seq") // 用于 Oracle、PostgreSQL、Kingbase、DB2、H2 数据库的主键自增。如果是 MySQL 等数据库，可不写。
@Data
@EqualsAndHashCode(callSuper = true)
@ToString(callSuper = true)
@Builder
@NoArgsConstructor
@AllArgsConstructor
public class FbCollectUserDO extends BaseDO {

    /**
     * 结果ID
     */
    @TableId
    private Long id;
    /**
     * 任务ID
     */
    private Long taskId;
    /**
     * 系统用户ID
     */
    private Long userId;
    /**
     * 部门ID
     */
    private Long deptId;
    /**
     * FB账号
     */
    private String fbAccount;
    /**
     * Facebook用户ID
     */
    private String fbUserId;
    /**
     * 账户类别(Facebook返回的category)
     */
    private String category;
    /**
     * 用户名称
     */
    private String userName;
    /**
     * 头像URL
     */
    private String avatar;
    /**
     * 主页链接
     */
    private String url;
    /**
     * 数据类型(0个人 1公共)
     *
     * 枚举 {@link TODO fb_page_type 对应的类}
     */
    private Integer dataType;
    /**
     * 粉丝数
     */
    private Integer followers;
    /**
     * 所在地
     */
    private String city;
    /**
     * 居住地
     */
    private String location;
    /**
     * 家乡
     */
    private String hometown;
    /**
     * 手机1
     */
    private String phonenumber;
    /**
     * 手机2
     */
    private String phonenumber2;
    /**
     * 邮箱1
     */
    private String email;
    /**
     * 邮箱2
     */
    private String email2;
    /**
     * 微信
     */
    private String wechat;
    /**
     * WhatsApp
     */
    private String whatsapp;
    /**
     * Line
     */
    private String line;
    /**
     * 社交网站
     */
    private String website;
    /**
     * 签名/状态
     */
    private String profileStatus;
    /**
     * 语言
     */
    private String language;
    /**
     * 性别
     */
    private String gender;
    /**
     * 婚姻状况
     */
    private String relationship;
    /**
     * 工作经历
     */
    private String workExperience;
    /**
     * 学历
     */
    private String education;
    /**
     * 最近发帖时间
     */
    private LocalDateTime lastPostTime;
    /**
     * 最近帖子摘要
     */
    private String lastPostSummary;
    /**
     * 分组ID
     */
    private Long groupId;
    /**
     * 数据来源
     */
    private String fromResource;
    /**
     * 配置信息
     */
    private String config;
    /**
     * 同步时间
     */
    private LocalDateTime syncTime;


}
