package cn.iocoder.yudao.module.facebook.controller.admin.message.vo;

import cn.iocoder.yudao.framework.common.pojo.PageParam;
import lombok.Data;

@Data
public class FbMessagePageReqVO extends PageParam {
    private Long conversationId;
    private Boolean unreadOnly;
}
