package cn.iocoder.yudao.module.facebook.dal.dataobject.aisearch;
import cn.iocoder.yudao.framework.tenant.core.db.TenantBaseDO;
import com.baomidou.mybatisplus.annotation.*; import lombok.Data; import lombok.EqualsAndHashCode;
@Data @EqualsAndHashCode(callSuper=true) @TableName("ai_search_memory")
public class AiSearchMemoryDO extends TenantBaseDO { @TableId(type=IdType.ASSIGN_ID) private Long id; private String scopeType; private Long scopeId; private String memoryKey; private String memoryValue; private java.math.BigDecimal score; private Integer usageCount; }
