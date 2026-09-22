package cn.iocoder.yudao.module.facebook.controller.admin.aisearch.vo;

import jakarta.validation.constraints.NotNull;
import lombok.Data;

import java.math.BigDecimal;
import java.time.LocalDate;
import java.time.LocalDateTime;

@Data
public class AiSearchTradeEvidenceSaveReqVO {
    @NotNull
    private Long companyId;
    private String url;
    private String hsCode;
    private String product;
    private String tradeType;
    private LocalDate tradeDate;
    private String partnerCountry;
    private String evidenceText;
    private BigDecimal confidence;
    private LocalDateTime collectedAt;
}
