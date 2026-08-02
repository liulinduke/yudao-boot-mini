package cn.iocoder.yudao.module.facebook.controller.admin.account.vo;

import io.swagger.v3.oas.annotations.media.Schema;
import lombok.Data;

import java.util.ArrayList;
import java.util.List;

@Schema(description = "FB账号智能分配候选查询")
@Data
public class FbAccountSelectorOptionReqVO {
    @Schema(description = "场景：collect/operation/agent")
    private String scene;

    @Schema(description = "操作类型：dm/repost/join_group/comment/follow/collect")
    private List<String> actionTypes = new ArrayList<>();

    @Schema(description = "本次任务单元数量")
    private Integer targetCount;

    @Schema(description = "手动选择的账号ID")
    private List<Long> accountIds = new ArrayList<>();
}
