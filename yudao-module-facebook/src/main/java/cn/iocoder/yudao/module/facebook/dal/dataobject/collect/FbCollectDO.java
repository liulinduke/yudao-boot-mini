package cn.iocoder.yudao.module.facebook.dal.dataobject.collect;

import lombok.*;
import java.util.*;
import java.time.LocalDateTime;
import java.time.LocalDateTime;
import java.time.LocalDateTime;
import java.time.LocalDateTime;
import com.baomidou.mybatisplus.annotation.*;
import cn.iocoder.yudao.framework.mybatis.core.dataobject.BaseDO;

/**
 * FB采集任务 DO
 *
 * @author jacky
 */
@TableName("facebook_collect")
@KeySequence("facebook_collect_seq") // 用于 Oracle、PostgreSQL、Kingbase、DB2、H2 数据库的主键自增。如果是 MySQL 等数据库，可不写。
@Data
@EqualsAndHashCode(callSuper = true)
@ToString(callSuper = true)
@Builder
@NoArgsConstructor
@AllArgsConstructor
public class FbCollectDO extends BaseDO {

    /**
     * 任务ID
     */
    @TableId
    private Long id;
    /**
     * FB账号
     */
    private String fbAccount;
    /**
     * 任务类型 (pages/posts/users)
     *
     * 枚举 {@link TODO fb_search_type 对应的类}
     */
    private Integer taskType;
    /**
     * 采集链接
     */
    private String searchUrl;
    /**
     * 搜索类型(0链接 1关键词 )
     */
    private Integer searchType;
    /**
     * 期望采集数量
     */
    private Integer expectedCount;
    /**
     * 采集间隔(秒)
     */
    private Integer intervalSeconds;
    /**
     * 状态（0待执行 1采集中 2已完成 3已失败 4已取消）
     *
     * 枚举 {@link TODO sys_collect_status 对应的类}
     */
    private Integer status;
    /**
     * 已采集数量
     */
    private Integer collectedCount;
    /**
     * 错误信息
     */
    private String errorMessage;
    /**
     * 开始时间
     */
    private LocalDateTime startTime;
    /**
     * 结束时间
     */
    private LocalDateTime endTime;
    /**
     * 备注
     */
    private String remark;


}