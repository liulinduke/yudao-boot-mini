package cn.iocoder.yudao.module.facebook.service.aisearch;
import cn.iocoder.yudao.module.facebook.controller.admin.aisearch.vo.*;
import cn.iocoder.yudao.module.facebook.dal.mysql.aisearch.*;
import cn.iocoder.yudao.module.facebook.dal.dataobject.aisearch.*;
import com.baomidou.mybatisplus.core.toolkit.Wrappers; import jakarta.annotation.Resource; import org.springframework.stereotype.Service; import java.util.List;
import cn.iocoder.yudao.module.facebook.controller.admin.aisearch.vo.AiSearchCustomerPageReqVO; import cn.iocoder.yudao.framework.common.pojo.PageResult;
@Service public class AiSearchResultServiceImpl implements AiSearchResultService {
 @Resource private AiSearchCompanyMapper mapper; @Resource private AiSearchContactMapper contactMapper; @Resource private AiSearchCompanyEvidenceMapper evidenceMapper; @Resource private AiSearchTradeEvidenceMapper tradeEvidenceMapper;
 public List<AiSearchCompanyRespVO> companies(Long taskId){return List.of();}
 public List<AiSearchContactRespVO> contacts(Long taskId){var q=Wrappers.lambdaQuery(AiSearchContactDO.class); if(taskId!=null) q.eq(AiSearchContactDO::getTaskId,taskId); return contactMapper.selectList(q.orderByDesc(AiSearchContactDO::getId)).stream().map(this::toResp).toList();}
 private AiSearchContactRespVO toResp(AiSearchContactDO c){AiSearchContactRespVO v=AiSearchContactRespVO.from(c); AiSearchCompanyDO co=mapper.selectById(c.getCompanyId()); if(co!=null){v.setCompanyName(co.getName());v.setCompanyCountry(co.getCountry());v.setCompanyIndustry(co.getIndustry());v.setCompanyWebsite(co.getWebsite());v.setCompanySize(co.getCompanySize());v.setIcpLevel(co.getIcpLevel());} return v;}
 public AiSearchCompanyDetailRespVO companyDetail(Long id){return null;}
 public AiSearchStatsRespVO stats(Long id){return null;}
 public PageResult<AiSearchContactRespVO> contactsPage(AiSearchContactPageReqVO req){ return page(contacts(req.getTaskId()), req.getPageNo(), req.getPageSize()); }
 public PageResult<AiSearchContactRespVO> customers(AiSearchCustomerPageReqVO req){
  List<AiSearchContactDO> rows=contactMapper.selectList(Wrappers.lambdaQuery(AiSearchContactDO.class).orderByDesc(AiSearchContactDO::getId));
  List<AiSearchContactRespVO> all=rows.stream().map(this::toResp).toList();
  int from=Math.max(0,(req.getPageNo()-1)*req.getPageSize()), to=Math.min(all.size(),from+req.getPageSize());
  return new PageResult<>(from>=all.size()?List.of():all.subList(from,to), (long) all.size());
 }
 private PageResult<AiSearchContactRespVO> page(List<AiSearchContactRespVO> rows,int page,int size){int from=Math.max(0,(page-1)*size),to=Math.min(rows.size(),from+size);return new PageResult<>(from>=rows.size()?List.of():rows.subList(from,to),(long)rows.size());}
}
