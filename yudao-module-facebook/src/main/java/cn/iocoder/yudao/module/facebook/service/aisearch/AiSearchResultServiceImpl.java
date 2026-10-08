package cn.iocoder.yudao.module.facebook.service.aisearch;
import cn.iocoder.yudao.module.facebook.controller.admin.aisearch.vo.*;
import cn.iocoder.yudao.module.facebook.dal.mysql.aisearch.*;
import cn.iocoder.yudao.module.facebook.dal.dataobject.aisearch.*;
import com.baomidou.mybatisplus.core.toolkit.Wrappers; import jakarta.annotation.Resource; import org.springframework.stereotype.Service; import java.util.List;
import cn.iocoder.yudao.module.facebook.controller.admin.aisearch.vo.AiSearchCustomerPageReqVO; import cn.iocoder.yudao.framework.common.pojo.PageResult;
@Service public class AiSearchResultServiceImpl implements AiSearchResultService {
 @Resource private AiSearchCompanyMapper mapper; @Resource private AiSearchContactMapper contactMapper; @Resource private AiSearchCompanyEvidenceMapper evidenceMapper; @Resource private AiSearchTradeEvidenceMapper tradeEvidenceMapper;
 public List<AiSearchCompanyRespVO> companies(Long taskId){
  var query=Wrappers.lambdaQuery(AiSearchCompanyDO.class);
  if(taskId!=null) query.eq(AiSearchCompanyDO::getTaskId,taskId);
  List<AiSearchCompanyDO> companies=mapper.selectList(query.orderByDesc(AiSearchCompanyDO::getId));
  if(companies.isEmpty()) return List.of();
  List<AiSearchContactDO> contacts=contactMapper.selectList(Wrappers.lambdaQuery(AiSearchContactDO.class)
    .in(AiSearchContactDO::getCompanyId,companies.stream().map(AiSearchCompanyDO::getId).toList()));
  var contactsByCompany=contacts.stream().collect(java.util.stream.Collectors.groupingBy(AiSearchContactDO::getCompanyId));
  return companies.stream().map(company->{
   AiSearchCompanyRespVO response=AiSearchCompanyRespVO.from(company);
   List<AiSearchContactDO> companyContacts=contactsByCompany.getOrDefault(company.getId(),List.of());
   response.setContactCount(companyContacts.size());
   response.setContactWithEmailCount((int)companyContacts.stream().filter(contact->contact.getEmail()!=null&&!contact.getEmail().isBlank()).count());
   response.setContactWithPhoneCount((int)companyContacts.stream().filter(contact->contact.getPhone()!=null&&!contact.getPhone().isBlank()).count());
   return response;
  }).toList();
 }
 public List<AiSearchContactRespVO> contacts(Long taskId){var q=Wrappers.lambdaQuery(AiSearchContactDO.class); if(taskId!=null) q.eq(AiSearchContactDO::getTaskId,taskId); return contactMapper.selectList(q.orderByDesc(AiSearchContactDO::getId)).stream().map(this::toResp).toList();}
 private AiSearchContactRespVO toResp(AiSearchContactDO c){AiSearchContactRespVO v=AiSearchContactRespVO.from(c); AiSearchCompanyDO co=mapper.selectById(c.getCompanyId()); if(co!=null){v.setCompanyName(co.getName());v.setCompanyCountry(co.getCountry());v.setCompanyIndustry(co.getIndustry());v.setCompanyWebsite(co.getWebsite());v.setCompanySize(co.getCompanySize());v.setIcpLevel(co.getIcpLevel());v.setIcpScore(co.getIcpScore());v.setCompanyNote(co.getCompanyNote());} return v;}
 public AiSearchCompanyDetailRespVO companyDetail(Long id){return null;}
 public AiSearchStatsRespVO stats(Long id){return null;}
 public PageResult<AiSearchContactRespVO> contactsPage(AiSearchContactPageReqVO req){
  var companyQuery=Wrappers.lambdaQuery(AiSearchCompanyDO.class);
  if(req.getTaskId()!=null) companyQuery.eq(AiSearchCompanyDO::getTaskId,req.getTaskId());
  List<AiSearchCompanyDO> companies=mapper.selectList(companyQuery.orderByDesc(AiSearchCompanyDO::getId));
  if(companies.isEmpty()) return new PageResult<>(List.of(),0L);
  var contactQuery=Wrappers.lambdaQuery(AiSearchContactDO.class)
    .in(AiSearchContactDO::getCompanyId,companies.stream().map(AiSearchCompanyDO::getId).toList());
  List<AiSearchContactDO> contacts=contactMapper.selectList(contactQuery.orderByDesc(AiSearchContactDO::getId));
  var contactsByCompany=contacts.stream().collect(java.util.stream.Collectors.groupingBy(AiSearchContactDO::getCompanyId));
  List<AiSearchContactRespVO> rows=new java.util.ArrayList<>();
  for(AiSearchCompanyDO company:companies){
   List<AiSearchContactDO> companyContacts=contactsByCompany.getOrDefault(company.getId(),List.of());
   if(companyContacts.isEmpty()) rows.add(toCompanyRow(company));
   else for(AiSearchContactDO contact:companyContacts) rows.add(toResp(contact));
  }
  List<AiSearchContactRespVO> filtered=rows.stream().filter(row->matches(row.getCompanyName(),req.getCompanyName()))
    .filter(row->matches(row.getName(),req.getContactName()))
    .filter(row->matches(row.getCompanyCountry(),req.getCountry()))
    .filter(row->matches(row.getCompanySize(),req.getCompanySize()))
    .filter(row->matches(row.getIcpLevel(),req.getIcpLevel()))
    .filter(row->!Boolean.TRUE.equals(req.getHasEmail())||hasText(row.getEmail()))
    .filter(row->!Boolean.TRUE.equals(req.getHasPhone())||hasText(row.getPhone())).toList();
  return page(filtered,req.getPageNo(),req.getPageSize());
 }
 private AiSearchContactRespVO toCompanyRow(AiSearchCompanyDO company){
  AiSearchContactRespVO row=new AiSearchContactRespVO();
  row.setCompanyId(company.getId());row.setTaskId(company.getTaskId());row.setCompanyName(company.getName());
  row.setCompanyCountry(company.getCountry());row.setCompanyIndustry(company.getIndustry());
  row.setCompanyWebsite(company.getWebsite());row.setCompanySize(company.getCompanySize());row.setIcpLevel(company.getIcpLevel());
  row.setIcpScore(company.getIcpScore());row.setCompanyNote(company.getCompanyNote());
  return row;
 }
 private boolean matches(String value,String filter){return !hasText(filter)||(value!=null&&value.toLowerCase(java.util.Locale.ROOT).contains(filter.trim().toLowerCase(java.util.Locale.ROOT)));}
 private boolean hasText(String value){return value!=null&&!value.isBlank();}
 public PageResult<AiSearchContactRespVO> customers(AiSearchCustomerPageReqVO req){
  List<AiSearchContactDO> rows=contactMapper.selectList(Wrappers.lambdaQuery(AiSearchContactDO.class).orderByDesc(AiSearchContactDO::getId));
  List<AiSearchContactRespVO> all=rows.stream().map(this::toResp).toList();
  int from=Math.max(0,(req.getPageNo()-1)*req.getPageSize()), to=Math.min(all.size(),from+req.getPageSize());
  return new PageResult<>(from>=all.size()?List.of():all.subList(from,to), (long) all.size());
 }
 private PageResult<AiSearchContactRespVO> page(List<AiSearchContactRespVO> rows,int page,int size){int from=Math.max(0,(page-1)*size),to=Math.min(rows.size(),from+size);return new PageResult<>(from>=rows.size()?List.of():rows.subList(from,to),(long)rows.size());}
}
