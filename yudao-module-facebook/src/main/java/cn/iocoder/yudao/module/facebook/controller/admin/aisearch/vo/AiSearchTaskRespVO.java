package cn.iocoder.yudao.module.facebook.controller.admin.aisearch.vo;
import cn.iocoder.yudao.module.facebook.dal.dataobject.aisearch.AiSearchTaskDO;
import lombok.Data;
@Data
public class AiSearchTaskRespVO {
    private Long id; private String name; private String userGoal; private String company; private String keywords; private String targetCountry;
    private Integer targetCount; private String customerType; private String referenceWebsite;
    private String hsCodes; private Boolean aiKeywordExpand; private Boolean contactEnrichment;
    private String positionStrategy; private String status; private String scheduleType; private String scheduleInterval;
    private AiSearchSnapshotVO searchSnapshot;
    public static AiSearchTaskRespVO from(AiSearchTaskDO d) {
        AiSearchTaskRespVO v = new AiSearchTaskRespVO();
        v.id=d.getId(); v.name=d.getName(); v.userGoal=d.getUserGoal(); v.company=d.getCompanyProduct(); v.keywords=d.getLeadKeywords(); v.targetCountry=d.getTargetCountry();
        v.targetCount=d.getTargetCount(); v.customerType=d.getCustomerType(); v.referenceWebsite=d.getReferenceWebsite();
        v.hsCodes=d.getHsCodes(); v.aiKeywordExpand=d.getAiKeywordExpand(); v.contactEnrichment=d.getContactEnrichment();
        v.positionStrategy=d.getPositionStrategy(); v.status=d.getStatus(); v.scheduleType=d.getScheduleType(); v.scheduleInterval=d.getScheduleInterval();
        v.searchSnapshot=d.getSearchSnapshotJson() == null || d.getSearchSnapshotJson().isBlank()
                ? null : cn.hutool.json.JSONUtil.toBean(d.getSearchSnapshotJson(), AiSearchSnapshotVO.class);
        return v;
    }
}
