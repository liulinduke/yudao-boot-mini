package cn.iocoder.yudao.module.facebook.dal.dataobject.aisearch;

import cn.iocoder.yudao.framework.tenant.core.db.TenantBaseDO;
import com.baomidou.mybatisplus.annotation.IdType;
import com.baomidou.mybatisplus.annotation.TableId;
import com.baomidou.mybatisplus.annotation.TableName;
import lombok.Data;
import lombok.EqualsAndHashCode;

import java.math.BigDecimal;

@Data
@EqualsAndHashCode(callSuper = true)
@TableName("ai_search_query")
public class AiSearchQueryDO extends TenantBaseDO {
    @TableId(type = IdType.ASSIGN_ID)
    private Long id;
    private Long taskId;
    private Long runId;
    private Integer roundNo;
    private String query;
    private String dimensionsJson;
    private String language;
    private String country;
    private Long sourceId;
    private Integer resultCount;
    private Integer newCompanyCount;
    private Integer qualifiedCount;
    private Integer duplicateCount;
    private BigDecimal yieldScore;
    private String status;
}
