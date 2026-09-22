package cn.iocoder.yudao.module.facebook.controller.admin.aisearch.vo;
import cn.iocoder.yudao.framework.common.pojo.PageParam;
import lombok.Data;
@Data public class AiSearchContactPageReqVO extends PageParam { private Long taskId; private String companyName; private String contactName; private String country; private String companySize; private String icpLevel; private Boolean hasEmail; private Boolean hasPhone; }
