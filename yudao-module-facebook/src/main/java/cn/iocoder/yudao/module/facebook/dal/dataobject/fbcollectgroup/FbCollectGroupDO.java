package cn.iocoder.yudao.module.facebook.dal.dataobject.fbcollectgroup;

import lombok.*;
import java.util.*;
import java.time.LocalDateTime;
import java.time.LocalDateTime;
import com.baomidou.mybatisplus.annotation.*;
import cn.iocoder.yudao.framework.mybatis.core.dataobject.BaseDO;

/**
 * FB群组采集结果 DO
 *
 * @author jacky
 */
@TableName("fb_collect_group")
@KeySequence("fb_collect_group_seq") // 用于 Oracle、PostgreSQL、Kingbase、DB2、H2 数据库的主键自增。如果是 MySQL 等数据库，可不写。
@Data
@EqualsAndHashCode(callSuper = true)
@ToString(callSuper = true)
@Builder
@NoArgsConstructor
@AllArgsConstructor
public class FbCollectGroupDO extends BaseDO {

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
     * 群组名称
     */
    private String groupName;
    /**
     * 群组链接
     */
    private String url;
    /**
     * 是否公开
     *
     * 枚举 {@link TODO fb_group_type 对应的类}
     */
    private String type;
    /**
     * 成员数量
     */
    private Long memberQuantity;
    /**
     * 群活跃
     */
    private String activeQuantity;
    /**
     * 分组
     */
    private Long sysGroupId;
    /**
     * 备注
     */
    private String remark;
    /**
     * 系统用户
     */
    private Long userId;
    /**
     * 部门
     */
    private Long deptId;
    /**
     * 采集回来的群id
     */
    private Long groupId;
    /**
     * 加组次数
     */
    private Integer joinGroupTimes;
    /**
     * 评论次数
     */
    private Integer commentTimes;
    /**
     * 发帖次数
     */
    private Integer postTimes;


}