package cn.iocoder.yudao.module.facebook.controller.admin.account.vo;

import io.swagger.v3.oas.annotations.media.Schema;
import jakarta.validation.Valid;
import jakarta.validation.constraints.NotEmpty;
import lombok.Data;

import java.util.List;

@Schema(description = "管理后台 - Facebook账号资料上传 Request VO")
@Data
public class FbAccountProfileUploadReqVO {

    @NotEmpty(message = "账号资料不能为空")
    @Valid
    private List<Item> items;

    @Data
    public static class Item {
        @Schema(description = "账号ID", requiredMode = Schema.RequiredMode.REQUIRED)
        private Long accountId;

        private String avatarUrl;
        private String coverUrl;
        private String nickname;
        private String signature;
    }
}
