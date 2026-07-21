package cn.iocoder.yudao.module.facebook.controller.admin.account.vo;

import io.swagger.v3.oas.annotations.media.Schema;
import jakarta.validation.constraints.NotNull;
import lombok.Data;

@Schema(description = "管理后台 - Facebook账号资料上传结果 Request VO")
@Data
public class FbAccountProfileReportReqVO {

    @NotNull(message = "账号ID不能为空")
    private Long accountId;

    @Schema(description = "PENDING/RUNNING/SUCCESS/FAILED")
    private String status;

    private String errorMessage;
    private String avatarUrl;
    private String coverUrl;
    private String nickname;
    private String signature;
}
