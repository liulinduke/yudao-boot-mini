package cn.iocoder.yudao.module.facebook.controller.admin.aisearch.vo;
import cn.iocoder.yudao.framework.common.pojo.PageParam; import lombok.Data; import java.time.LocalDateTime;
@Data public class AiSearchCustomerPageReqVO extends PageParam { private String taskName; private LocalDateTime[] createTime; private String companyName; private String country; private String companySize; private String icpLevel; private Boolean hasEmail; private Boolean hasPhone; }
