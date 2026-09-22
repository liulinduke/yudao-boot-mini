package cn.iocoder.yudao.module.facebook.dal.dataobject.aisearch;

import cn.iocoder.yudao.framework.tenant.core.db.TenantBaseDO;
import com.baomidou.mybatisplus.annotation.*;
import lombok.Data;
import lombok.EqualsAndHashCode;
import java.time.LocalDateTime;

@Data
@EqualsAndHashCode(callSuper = true)
@TableName("ai_search_source_registry")
public class AiSearchSourceDO extends TenantBaseDO {
    @TableId(type = IdType.ASSIGN_ID) private Long id;
    private String name; private String domain; private String sourceType; private String sourceRole;
    private String countries; private String industries; private String customerTypes; private String keywords;
    private String languages; private String entityTypes; private String discoveryMethods; private String capabilities;
    private Integer priority; private String status; private Boolean verified; private Integer usageCount;
    private Integer companyCount; private Integer qualifiedCompanyCount; private Integer duplicateCount;
    private java.math.BigDecimal yieldScore; private java.math.BigDecimal errorRate;
    private java.math.BigDecimal avgNewCompany; private java.math.BigDecimal avgQualifiedCompany;
    private java.math.BigDecimal avgContacts; private LocalDateTime lastTestedAt; private LocalDateTime lastUsedAt;
}
