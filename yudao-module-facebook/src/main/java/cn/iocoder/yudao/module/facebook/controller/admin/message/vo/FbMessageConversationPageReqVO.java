package cn.iocoder.yudao.module.facebook.controller.admin.message.vo;

import cn.iocoder.yudao.framework.common.pojo.PageParam;
import lombok.Data;

@Data
public class FbMessageConversationPageReqVO extends PageParam {
    private Long accountId;
    private String sourceType;
    private String keyword;
}
