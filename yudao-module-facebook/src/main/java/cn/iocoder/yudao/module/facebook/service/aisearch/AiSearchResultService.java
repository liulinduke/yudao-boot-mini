package cn.iocoder.yudao.module.facebook.service.aisearch;
import cn.iocoder.yudao.module.facebook.controller.admin.aisearch.vo.AiSearchCompanyRespVO;
import cn.iocoder.yudao.module.facebook.controller.admin.aisearch.vo.AiSearchContactRespVO;
import cn.iocoder.yudao.module.facebook.controller.admin.aisearch.vo.AiSearchCompanyDetailRespVO;
import cn.iocoder.yudao.module.facebook.controller.admin.aisearch.vo.AiSearchStatsRespVO;
import java.util.List;
import cn.iocoder.yudao.module.facebook.controller.admin.aisearch.vo.AiSearchCustomerPageReqVO;
import cn.iocoder.yudao.module.facebook.controller.admin.aisearch.vo.AiSearchContactPageReqVO;
import cn.iocoder.yudao.framework.common.pojo.PageResult;
public interface AiSearchResultService { List<AiSearchCompanyRespVO> companies(Long taskId); List<AiSearchContactRespVO> contacts(Long taskId); PageResult<AiSearchContactRespVO> contactsPage(AiSearchContactPageReqVO req); AiSearchCompanyDetailRespVO companyDetail(Long companyId); AiSearchStatsRespVO stats(Long taskId); PageResult<AiSearchContactRespVO> customers(AiSearchCustomerPageReqVO req); }
