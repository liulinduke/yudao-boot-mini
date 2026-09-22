package cn.iocoder.yudao.module.facebook.dal.dataobject.aisearch;
import cn.iocoder.yudao.framework.tenant.core.db.TenantBaseDO;
import com.baomidou.mybatisplus.annotation.*; import lombok.Data; import lombok.EqualsAndHashCode;
import java.time.LocalDateTime;
@Data @EqualsAndHashCode(callSuper=true) @TableName("ai_search_contact_evidence")
public class AiSearchContactEvidenceDO extends TenantBaseDO { @TableId(type=IdType.ASSIGN_ID) private Long id; private Long contactId; private String url; private String evidenceText; private java.math.BigDecimal confidence; private LocalDateTime collectedAt; }
