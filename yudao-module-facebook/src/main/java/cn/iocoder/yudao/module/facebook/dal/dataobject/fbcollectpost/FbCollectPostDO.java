package cn.iocoder.yudao.module.facebook.dal.dataobject.fbcollectpost;

import lombok.*;
import java.util.*;
import java.time.LocalDateTime;
import com.baomidou.mybatisplus.annotation.*;
import cn.iocoder.yudao.framework.mybatis.core.dataobject.BaseDO;

/**
 * FB帖子采集结果 DO
 *
 * @author jacky
 */
@TableName("fb_collect_post")
@KeySequence("fb_collect_post_seq") // 用于 Oracle、PostgreSQL、Kingbase、DB2、H2 数据库的主键自增。如果是 MySQL 等数据库，可不写。
@Data
@EqualsAndHashCode(callSuper = true)
@ToString(callSuper = true)
@Builder
@NoArgsConstructor
@AllArgsConstructor
public class FbCollectPostDO extends BaseDO {

    /**
     * id
     */
    @TableId
    private Long id;
    /**
     * 任务id
     */
    private Long taskId;
    /**
     * 帖子唯一id
     */
    private String itemId;
    /**
     * 发贴人
     */
    private String postUser;
    /**
     * 帖子链接
     */
    private String url;
    /**
     * 帖子来源
     */
    private String fromResource;
    /**
     * 群名
     */
    private String groupName;
    /**
     * 系统分组
     */
    private Long sysGroupId;
    /**
     * 系统用户id
     */
    private Long userId;
    /**
     * 部门id
     */
    private Long deptId;
    /**
     * 采集回来的群id
     */
    private Long groupId;
    /**
     * 转发数
     */
    private Integer reshareCount;
    /**
     * 评论数
     */
    private Integer commentCount;
    /**
     * 点赞数
     */
    private Integer reactionCount;
    /**
     * 截流次数
     */
    private Integer usedCount;
    /**
     * 帖子内容
     */
    private String postContent;
    /**
     * FB账号
     */
    private String fbAccount;
    /**
     * 帖子创建时间
     */
    private LocalDateTime postCreateTime;


}