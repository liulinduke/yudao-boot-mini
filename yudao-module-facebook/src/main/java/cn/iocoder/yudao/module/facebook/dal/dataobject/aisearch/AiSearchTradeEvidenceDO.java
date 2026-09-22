package cn.iocoder.yudao.module.facebook.dal.dataobject.aisearch;
import cn.iocoder.yudao.framework.tenant.core.db.TenantBaseDO;
import com.baomidou.mybatisplus.annotation.*; import lombok.Data; import lombok.EqualsAndHashCode;
import java.time.LocalDate; import java.time.LocalDateTime;
@Data @EqualsAndHashCode(callSuper=true) @TableName("ai_search_trade_evidence")
public class AiSearchTradeEvidenceDO extends TenantBaseDO { @TableId(type=IdType.ASSIGN_ID) private Long id; private Long companyId; private String url; private String hsCode; private String product; private String tradeType; private LocalDate tradeDate; private String partnerCountry; private String evidenceText; private java.math.BigDecimal confidence; private LocalDateTime collectedAt; }
