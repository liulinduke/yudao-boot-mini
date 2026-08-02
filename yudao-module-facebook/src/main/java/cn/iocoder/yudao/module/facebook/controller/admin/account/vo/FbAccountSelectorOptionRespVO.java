package cn.iocoder.yudao.module.facebook.controller.admin.account.vo;

import io.swagger.v3.oas.annotations.media.Schema;
import lombok.Data;

import java.time.LocalDateTime;
import java.util.LinkedHashMap;
import java.util.Map;

@Schema(description = "FB账号智能分配候选")
@Data
public class FbAccountSelectorOptionRespVO {
    private Long id;
    private String fbAccount;
    private Long groupId;
    private Boolean status;
    private String loginStatus;
    private Boolean eligible;
    private String disabledReason;
    private Map<String, Integer> today = new LinkedHashMap<>();
    private Map<String, Integer> limits = new LinkedHashMap<>();
    private Map<String, Long> total = new LinkedHashMap<>();
    private LocalDateTime lastExecuteTime;
}
