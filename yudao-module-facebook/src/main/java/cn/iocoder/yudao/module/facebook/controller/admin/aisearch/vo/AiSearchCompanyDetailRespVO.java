package cn.iocoder.yudao.module.facebook.controller.admin.aisearch.vo;
import lombok.Data; import java.util.List;
@Data public class AiSearchCompanyDetailRespVO { private AiSearchCompanyRespVO company; private List<AiSearchContactRespVO> contacts; private List<AiSearchEvidenceRespVO> evidences; private List<AiSearchTradeEvidenceRespVO> tradeEvidences; }
