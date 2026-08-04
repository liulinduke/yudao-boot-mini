package cn.iocoder.yudao.module.facebook.controller.admin.dmtask.vo;

import io.swagger.v3.oas.annotations.media.Schema;
import lombok.Data;

import java.time.LocalDateTime;

@Schema(description = "管理后台 - Facebook 群发私信任务明细 Response VO")
@Data
public class FbDmTaskDetailRespVO {

    @Schema(description = "主键ID", requiredMode = Schema.RequiredMode.REQUIRED, example = "1")
    private Long id;

    @Schema(description = "任务ID", requiredMode = Schema.RequiredMode.REQUIRED, example = "100")
    private Long taskId;

    @Schema(description = "执行账号ID", requiredMode = Schema.RequiredMode.REQUIRED)
    private String accountId;

    @Schema(description = "目标用户FB ID", requiredMode = Schema.RequiredMode.REQUIRED)
    private String targetUserId;

    @Schema(description = "使用的话术")
    private String scriptContent;

    @Schema(description = "账号Cookie（用于WPF调用）")
    private String cookie;

    @Schema(description = "账号密码（用于WPF复用账号登录流程）")
    private String password;

    @Schema(description = "账号2FA配置（用于WPF复用账号登录流程）")
    private String tfa;

    @Schema(description = "状态：0待执行 1成功 2失败", example = "0")
    private Integer status;

    @Schema(description = "错误信息")
    private String errorMsg;

    @Schema(description = "发送时间")
    private LocalDateTime sendTime;

}
