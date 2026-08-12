package cn.iocoder.yudao.module.facebook.dal.dataobject.warmup;

import cn.iocoder.yudao.framework.tenant.core.db.TenantBaseDO;
import com.baomidou.mybatisplus.annotation.KeySequence;
import com.baomidou.mybatisplus.annotation.TableId;
import com.baomidou.mybatisplus.annotation.TableName;
import lombok.*;

import java.time.LocalDateTime;

@TableName("fb_warmup_task_detail")
@KeySequence("fb_warmup_task_detail_seq")
@Data
@EqualsAndHashCode(callSuper = true)
@Builder
@NoArgsConstructor
@AllArgsConstructor
public class FbWarmupTaskDetailDO extends TenantBaseDO {
    @TableId
    private Long id;
    private Long taskId;
    private String accountId;
    private Integer status;
    private LocalDateTime startTime;
    private LocalDateTime endTime;
    private String errorMessage;
}
