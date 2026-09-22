package cn.iocoder.yudao.module.facebook.dal.dataobject.aisearch;

import cn.iocoder.yudao.framework.tenant.core.db.TenantBaseDO;
import com.baomidou.mybatisplus.annotation.IdType;
import com.baomidou.mybatisplus.annotation.TableId;
import com.baomidou.mybatisplus.annotation.TableName;
import lombok.Data;
import lombok.EqualsAndHashCode;

@TableName("ai_search_contact")
@Data
@EqualsAndHashCode(callSuper = true)
public class AiSearchContactDO extends TenantBaseDO {
    @TableId(type = IdType.ASSIGN_ID) private Long id;
    private Long companyId;
    private Long taskId;
    private String name;
    private String normalizedName;
    private String jobTitle;
    private String email;
    private String phone;
    private String linkedinUrl;
    private String otherSocialUrl;
    private String gender;
    private String note;
    private Integer priorityScore;
    private Boolean aiRecommended;
    private String verificationStatus;
    private java.math.BigDecimal confidence;
}
