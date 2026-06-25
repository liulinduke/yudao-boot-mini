package cn.iocoder.yudao.module.facebook.controller.admin.agent.vo;

import cn.iocoder.yudao.framework.common.pojo.PageParam;
import io.swagger.v3.oas.annotations.media.Schema;
import lombok.Data;
import org.springframework.format.annotation.DateTimeFormat;

import java.time.LocalDateTime;

import static cn.iocoder.yudao.framework.common.util.date.DateUtils.FORMAT_YEAR_MONTH_DAY_HOUR_MINUTE_SECOND;

@Schema(description = "管理后台 - Facebook AI触达记录分页 Request VO")
@Data
public class FbAiTouchRecordPageReqVO extends PageParam {

    @Schema(description = "Agent配置ID")
    private Long agentConfigId;

    @Schema(description = "线索类型：user/post/comment")
    private String leadType;

    @Schema(description = "线索ID")
    private Long leadId;

    @Schema(description = "触达类型：comment/dm")
    private String touchType;

    @Schema(description = "状态")
    private Integer status;

    @Schema(description = "创建时间")
    @DateTimeFormat(pattern = FORMAT_YEAR_MONTH_DAY_HOUR_MINUTE_SECOND)
    private LocalDateTime[] createTime;

}
