package cn.iocoder.yudao.module.facebook.dal.dataobject.aisearch;

import cn.iocoder.yudao.framework.tenant.core.db.TenantBaseDO;
import com.baomidou.mybatisplus.annotation.IdType;
import com.baomidou.mybatisplus.annotation.TableId;
import com.baomidou.mybatisplus.annotation.TableName;
import lombok.Data;
import lombok.EqualsAndHashCode;

import java.time.LocalDateTime;

@Data
@EqualsAndHashCode(callSuper = true)
@TableName("ai_search_run")
public class AiSearchRunDO extends TenantBaseDO {
    @TableId(type = IdType.ASSIGN_ID)
    private Long id;
    private Long taskId;
    private String status;
    private LocalDateTime startedAt;
    private LocalDateTime completedAt;
    private Integer roundCount;
    private Integer queryCount;
    private Integer sourceCount;
    private Integer newCompanyCount;
    private Integer qualifiedCompanyCount;
    private Integer tradeVerificationCount;
    private Integer tradeFoundCount;
    private Integer contactCount;
    private Integer errorCount;
    private Integer targetQualifiedCount;
    private Integer qualifiedCountBefore;
}
