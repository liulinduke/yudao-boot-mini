package cn.iocoder.yudao.module.facebook.dal.dataobject.collectdetail;

import com.baomidou.mybatisplus.annotation.KeySequence;
import com.baomidou.mybatisplus.annotation.TableId;
import com.baomidou.mybatisplus.annotation.TableName;
import cn.iocoder.yudao.framework.mybatis.core.dataobject.BaseDO;
import lombok.*;

import java.time.LocalDateTime;

/**
 * FB采集任务明细 DO
 *
 * @author jacky
 */
@TableName("facebook_collect_detail")
@KeySequence("facebook_collect_detail_seq")
@Data
@EqualsAndHashCode(callSuper = true)
@ToString(callSuper = true)
@Builder
@NoArgsConstructor
@AllArgsConstructor
public class FbCollectDetailDO extends BaseDO {

    /**
     * 明细ID
     */
    @TableId
    private Long id;
    
    /**
     * 任务ID(主表)
     */
    private Long taskId;
    
    /**
     * FB账号
     */
    private String fbAccount;
    
    /**
     * 采集链接
     */
    private String searchUrl;
    
    /**
     * 期望采集数量
     */
    private Integer expectedCount;
    
    /**
     * 已采集数量
     */
    private Integer collectedCount;
    
    /**
     * 状态(0待执行 1采集中 2已完成 3失败)
     */
    private Integer status;
    
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

}
