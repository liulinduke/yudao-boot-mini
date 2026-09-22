package cn.iocoder.yudao.module.facebook.service.aisearch;
import cn.iocoder.yudao.module.facebook.controller.admin.aisearch.vo.AiSearchTradeVerifyRespVO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.aisearch.AiSearchCompanyDO;
import cn.iocoder.yudao.module.facebook.dal.mysql.aisearch.AiSearchCompanyMapper;
import cn.iocoder.yudao.module.facebook.dal.mysql.aisearch.AiSearchTradeEvidenceMapper;
import cn.iocoder.yudao.module.facebook.dal.dataobject.aisearch.AiSearchTradeEvidenceDO;
import cn.iocoder.yudao.module.ai.framework.ai.core.webserch.AiWebSearchClient;
import cn.iocoder.yudao.module.ai.framework.ai.core.webserch.AiWebSearchRequest;
import cn.iocoder.yudao.module.ai.framework.ai.core.webserch.AiWebSearchResponse;
import com.baomidou.mybatisplus.core.toolkit.Wrappers; import jakarta.annotation.Resource; import org.springframework.stereotype.Service;
@Service public class AiSearchTradeServiceImpl implements AiSearchTradeService {
    @Resource private AiSearchCompanyMapper companyMapper; @Resource private AiSearchTradeEvidenceMapper evidenceMapper;
    @Resource private AiWebSearchClient webSearchClient;
    @Override public Long saveEvidence(AiSearchTradeEvidenceDO evidence) {
        if (evidence == null || evidence.getCompanyId() == null || companyMapper.selectById(evidence.getCompanyId()) == null) {
            throw new IllegalArgumentException("企业不存在");
        }
        if (evidence.getCollectedAt() == null) evidence.setCollectedAt(java.time.LocalDateTime.now());
        evidenceMapper.insert(evidence);
        return evidence.getId();
    }
    public AiSearchTradeVerifyRespVO verify(Long companyId,String hsCode,String product) {
        AiSearchTradeVerifyRespVO result=new AiSearchTradeVerifyRespVO(); result.setCompanyId(companyId);
        AiSearchCompanyDO company=companyMapper.selectById(companyId);
        var evidenceQuery = Wrappers.lambdaQuery(AiSearchTradeEvidenceDO.class).eq(AiSearchTradeEvidenceDO::getCompanyId,companyId)
                .eq(hsCode != null && !hsCode.isBlank(), AiSearchTradeEvidenceDO::getHsCode, hsCode)
                .like(product != null && !product.isBlank(), AiSearchTradeEvidenceDO::getProduct, product);
        int count=Math.toIntExact(evidenceMapper.selectCount(evidenceQuery));
        result.setEvidenceCount(count);
        if (company == null || (hsCode == null && product == null)) { result.setStatus("UNKNOWN"); result.setNote("缺少企业或核验条件"); return result; }
        if (count > 0) { result.setStatus("FOUND"); result.setNote("已存在贸易证据"); }
        else {
            try {
                String query = company.getName() + " " + (company.getCountry() == null ? "" : company.getCountry())
                        + " " + (product == null ? "" : product) + " " + (hsCode == null ? "" : hsCode)
                        + " import shipment customs trade";
                AiWebSearchResponse response = webSearchClient == null ? null : webSearchClient.search(new AiWebSearchRequest()
                        .setQuery(query).setCount(10).setSummary(true));
                var pages = response == null || response.getLists() == null ? java.util.List.<AiWebSearchResponse.WebPage>of() : response.getLists();
                for (AiWebSearchResponse.WebPage page : pages) {
                    if (page == null || page.getUrl() == null || page.getUrl().isBlank()) continue;
                    if (evidenceMapper.selectCount(Wrappers.lambdaQuery(AiSearchTradeEvidenceDO.class)
                            .eq(AiSearchTradeEvidenceDO::getCompanyId, companyId).eq(AiSearchTradeEvidenceDO::getUrl, page.getUrl())) > 0) continue;
                    AiSearchTradeEvidenceDO evidence = new AiSearchTradeEvidenceDO(); evidence.setCompanyId(companyId);
                    evidence.setUrl(page.getUrl()); evidence.setHsCode(hsCode); evidence.setProduct(product);
                    evidence.setTradeType("PUBLIC_WEB"); evidence.setEvidenceText(page.getSummary() == null ? page.getSnippet() : page.getSummary());
                    evidence.setConfidence(java.math.BigDecimal.valueOf(.35)); evidence.setCollectedAt(java.time.LocalDateTime.now());
                    evidenceMapper.insert(evidence); count++;
                    if (count >= 5) break;
                }
                result.setEvidenceCount(count);
                result.setStatus(count > 0 ? "FOUND" : "NOT_FOUND");
                result.setNote(count > 0 ? "发现公开贸易相关证据，请结合来源核验" : "本次公开检索未发现匹配记录，不代表没有贸易记录");
            } catch (Exception ex) { result.setStatus("UNKNOWN"); result.setNote("贸易检索暂时不可用，不能据此判断没有贸易记录"); }
        }
        return result;
    }
}
