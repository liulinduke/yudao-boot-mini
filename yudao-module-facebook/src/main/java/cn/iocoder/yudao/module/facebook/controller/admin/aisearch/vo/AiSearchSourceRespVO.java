package cn.iocoder.yudao.module.facebook.controller.admin.aisearch.vo;
import cn.iocoder.yudao.module.facebook.dal.dataobject.aisearch.AiSearchSourceDO;
import lombok.Data;
@Data public class AiSearchSourceRespVO {
    private Long id; private String name; private String domain; private String sourceType; private String sourceRole;
    private Integer priority; private String status; private Boolean verified; private Integer usageCount;
    private Integer companyCount; private Integer qualifiedCompanyCount; private java.math.BigDecimal yieldScore;
    public static AiSearchSourceRespVO from(AiSearchSourceDO d) { AiSearchSourceRespVO v=new AiSearchSourceRespVO(); v.id=d.getId(); v.name=d.getName(); v.domain=d.getDomain(); v.sourceType=d.getSourceType(); v.sourceRole=d.getSourceRole(); v.priority=d.getPriority(); v.status=d.getStatus(); v.verified=d.getVerified(); v.usageCount=d.getUsageCount(); v.companyCount=d.getCompanyCount(); v.qualifiedCompanyCount=d.getQualifiedCompanyCount(); v.yieldScore=d.getYieldScore(); return v; }
}
