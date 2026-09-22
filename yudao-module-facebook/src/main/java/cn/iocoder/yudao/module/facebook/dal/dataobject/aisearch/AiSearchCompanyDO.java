package cn.iocoder.yudao.module.facebook.dal.dataobject.aisearch;

import cn.iocoder.yudao.framework.tenant.core.db.TenantBaseDO;
import com.baomidou.mybatisplus.annotation.IdType;
import com.baomidou.mybatisplus.annotation.TableId;
import com.baomidou.mybatisplus.annotation.TableName;
import lombok.Data;
import lombok.EqualsAndHashCode;

@TableName("ai_search_company")
@Data
@EqualsAndHashCode(callSuper = true)
public class AiSearchCompanyDO extends TenantBaseDO {
    @TableId(type = IdType.ASSIGN_ID) private Long id;
    private Long taskId;
    private String name;
    private String normalizedName;
    private String legalName;
    private String country;
    private String region;
    private String website;
    private String domain;
    private String industry;
    private String customerType;
    private String description;
    private String address;
    private String postalCode;
    private String phone;
    private String fax;
    private String email;
    private String linkedinUrl;
    private String facebookUrl;
    private Integer employeeCount;
    private String companySize;
    private String mainProducts;
    private String purchasingProducts;
    private Integer foundedYear;
    private String revenue;
    private String companyNote;
    private String customerStage;
    private String customerTags;
    private Integer icpScore;
    private String icpLevel;
    private String verificationStatus;
    private java.math.BigDecimal confidence;
}
