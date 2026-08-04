package cn.iocoder.yudao.module.facebook.controller.admin.operation.vo;

import io.swagger.v3.oas.annotations.media.Schema;
import lombok.Data;

import java.util.List;

@Schema(description = "管理后台 - 已加入群组操作账号解析 Request VO")
@Data
public class FbOperationGroupSelectorAccountReqVO {

    @Schema(description = "账号分配模式：AUTO/MANUAL")
    private String accountSelectionMode;

    @Schema(description = "手动选择的账号ID")
    private List<String> accountIds;

    @Schema(description = "自动选择账号数量")
    private Integer targetAccountCount;

    @Schema(description = "每个账号至少需要的群组数")
    private Integer minGroupCount;

    @Schema(description = "加组时间早于指定天数，0为不限")
    private Integer joinedBeforeDays;

    @Schema(description = "群组资源分组ID")
    private Long resourceGroupId;

    @Schema(description = "群组名称关键词")
    private String groupName;

    @Schema(description = "操作类型：group_post/repost")
    private String actionType;
}
