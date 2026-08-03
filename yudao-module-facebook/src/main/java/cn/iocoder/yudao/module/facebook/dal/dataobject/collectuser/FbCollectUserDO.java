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
    @TableId(type = IdType.ASSIGN_ID)
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
    private Long followers;
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
     * 是否已深度采集
     */
    private Boolean deepCollected;
    /**
     * 分组ID
     */
    private Long groupId;
    /** 资源分组ID（潜客分组） */
    private Long resourceGroupId;
    /**
     * 数据来源
     */
    private String fromResource;
    /**
     * 配置信息
     */
    private String config;
    /**
     * 评论内容
     */
    private String commentContent;
    /**
     * 来源帖子ID
     */
    private Long sourcePostId;
    /**
     * 来源帖子URL
     */
    private String sourcePostUrl;

    /**
     * 评论截流详情展示用的来源帖子内容（非 fb_collect_user 表字段）。
     */
    @TableField(exist = false)
    private String postContent;

    /** 评论截流详情展示用的来源帖子创建时间（非 fb_collect_user 表字段）。 */
    @TableField(exist = false)
    private LocalDateTime postCreateTime;

    /**
     * 同步时间
     */
    private LocalDateTime syncTime;

    /**
     * AI标签，逗号分隔
     */
    private String aiTags;
    /**
     * 意向等级：high/medium/low/unknown
     */
    private String intentLevel;
    /**
     * 意向判断理由
     */
    private String intentReason;
    /**
     * 情绪：positive/neutral/negative
     */
    private String sentiment;
    /**
     * 线索类型
     */
    private String leadType;
    /**
     * 国家
     */
    private String country;
    /**
     * 产品相关度 0-100
     */
    private Integer productRelevanceScore;
    /**
     * AI摘要
     */
    private String aiSummary;
    /**
     * 最近AI分析时间
     */
    private LocalDateTime lastAiAnalyzeTime;
    /**
     * 触达状态：not_touched/touched/replied/done
     */
    private String touchStatus;
    /**
     * 最近触达时间
     */
    private LocalDateTime lastTouchTime;


}
