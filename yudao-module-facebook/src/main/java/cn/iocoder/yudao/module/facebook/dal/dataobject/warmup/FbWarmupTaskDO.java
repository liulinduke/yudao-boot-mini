package cn.iocoder.yudao.module.facebook.dal.dataobject.warmup;

import cn.iocoder.yudao.framework.tenant.core.db.TenantBaseDO;
import com.baomidou.mybatisplus.annotation.KeySequence;
import com.baomidou.mybatisplus.annotation.TableId;
import com.baomidou.mybatisplus.annotation.TableName;
import lombok.*;

import java.time.LocalDateTime;

@TableName("fb_warmup_task")
@KeySequence("fb_warmup_task_seq")
@Data
@EqualsAndHashCode(callSuper = true)
@Builder
@NoArgsConstructor
@AllArgsConstructor
public class FbWarmupTaskDO extends TenantBaseDO {
    @TableId
    private Long id;
    private String taskName;
    private LocalDateTime scheduleTime;
    private String warmupConfig;
    private Integer status;
    private Integer accountCount;
    private LocalDateTime readyTime;
    private LocalDateTime startTime;
    private LocalDateTime endTime;
    private String errorMessage;
}
