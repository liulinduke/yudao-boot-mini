package cn.iocoder.yudao.module.facebook.controller.admin.aisearch.vo;

import cn.iocoder.yudao.module.facebook.dal.dataobject.aisearch.AiSearchTradeEvidenceDO;
import lombok.Data;

import java.math.BigDecimal;
import java.time.LocalDate;

@Data
public class AiSearchTradeEvidenceRespVO {
    private Long id; private Long companyId; private String url; private String hsCode; private String product;
    private String tradeType; private LocalDate tradeDate; private String partnerCountry; private String evidenceText; private BigDecimal confidence;
    public static AiSearchTradeEvidenceRespVO from(AiSearchTradeEvidenceDO d) {
        AiSearchTradeEvidenceRespVO v = new AiSearchTradeEvidenceRespVO();
        v.id=d.getId(); v.companyId=d.getCompanyId(); v.url=d.getUrl(); v.hsCode=d.getHsCode(); v.product=d.getProduct();
        v.tradeType=d.getTradeType(); v.tradeDate=d.getTradeDate(); v.partnerCountry=d.getPartnerCountry(); v.evidenceText=d.getEvidenceText(); v.confidence=d.getConfidence();
        return v;
    }
}
