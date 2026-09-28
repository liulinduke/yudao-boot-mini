package cn.iocoder.yudao.module.facebook.dal.dataobject.aisearch;

import cn.iocoder.yudao.framework.tenant.core.db.TenantBaseDO;
import com.baomidou.mybatisplus.annotation.IdType;
import com.baomidou.mybatisplus.annotation.TableId;
import com.baomidou.mybatisplus.annotation.TableName;
import lombok.Data;
import lombok.EqualsAndHashCode;

@TableName("ai_search_task")
@Data
@EqualsAndHashCode(callSuper = true)
public class AiSearchTaskDO extends TenantBaseDO {
    @TableId(type = IdType.ASSIGN_ID)
    private Long id;
    private String name;
    private String userGoal;
    private String companyProduct;
    private String leadKeywords;
    private String targetCountry;
    private Integer targetCount;
    private String customerType;
    private String referenceWebsite;
    private String hsCodes;
    private Boolean aiKeywordExpand;
    private Boolean contactEnrichment;
    private String positionStrategy;
    private String status;
    private String scheduleType;
    private String scheduleInterval;
    private String searchSnapshotJson;
    private java.time.LocalDateTime startedAt;
    private java.time.LocalDateTime completedAt;
}
