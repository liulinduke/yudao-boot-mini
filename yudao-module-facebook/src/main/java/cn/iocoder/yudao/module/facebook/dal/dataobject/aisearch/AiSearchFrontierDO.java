package cn.iocoder.yudao.module.facebook.dal.dataobject.aisearch;
import cn.iocoder.yudao.framework.tenant.core.db.TenantBaseDO;
import com.baomidou.mybatisplus.annotation.*; import lombok.Data; import lombok.EqualsAndHashCode;
@Data @EqualsAndHashCode(callSuper=true) @TableName("ai_search_frontier")
public class AiSearchFrontierDO extends TenantBaseDO { @TableId(type=IdType.ASSIGN_ID) private Long id; private Long taskId; private String country; private String product; private String customerType; private String language; private String keyword; private Long sourceId; private String status; private Integer lastRoundNo; }
