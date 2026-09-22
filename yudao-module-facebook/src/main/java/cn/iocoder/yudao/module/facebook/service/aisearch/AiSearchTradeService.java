package cn.iocoder.yudao.module.facebook.service.aisearch;
import cn.iocoder.yudao.module.facebook.controller.admin.aisearch.vo.AiSearchTradeVerifyRespVO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.aisearch.AiSearchTradeEvidenceDO;
public interface AiSearchTradeService {
    AiSearchTradeVerifyRespVO verify(Long companyId, String hsCode, String product);
    Long saveEvidence(AiSearchTradeEvidenceDO evidence);
}
