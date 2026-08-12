package cn.iocoder.yudao.module.facebook.controller.admin.warmup.vo;

import com.fasterxml.jackson.databind.annotation.JsonSerialize;
import com.fasterxml.jackson.databind.ser.std.ToStringSerializer;
import lombok.Data;

import java.time.LocalDateTime;
import java.util.List;

@Data
public class FbWarmupTaskRespVO {
    @JsonSerialize(using = ToStringSerializer.class)
    private Long id;
    private String taskName;
    private LocalDateTime scheduleTime;
    private String warmupConfig;
    private Integer status;
    private Integer accountCount;
    private List<String> accountIds;
    private LocalDateTime readyTime;
    private LocalDateTime startTime;
    private LocalDateTime endTime;
    private String errorMessage;
}
