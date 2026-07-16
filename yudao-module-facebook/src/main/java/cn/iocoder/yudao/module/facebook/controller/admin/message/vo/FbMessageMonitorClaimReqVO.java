package cn.iocoder.yudao.module.facebook.controller.admin.message.vo;

import lombok.Data;

import java.util.List;

@Data
public class FbMessageMonitorClaimReqVO {
    private Integer limit;
    private List<String> excludeAccounts;
    private List<String> accountIds;
    private Boolean manual;
}
