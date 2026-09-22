package cn.iocoder.yudao.module.facebook.controller.admin.aisearch;
import cn.iocoder.yudao.framework.common.pojo.CommonResult;
import cn.iocoder.yudao.module.facebook.controller.admin.aisearch.vo.AiSearchCompanyRespVO;
import cn.iocoder.yudao.module.facebook.controller.admin.aisearch.vo.AiSearchContactRespVO;
import cn.iocoder.yudao.module.facebook.controller.admin.aisearch.vo.AiSearchCompanyDetailRespVO;
import cn.iocoder.yudao.module.facebook.controller.admin.aisearch.vo.AiSearchStatsRespVO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.aisearch.AiSearchRunDO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.aisearch.AiSearchRoundDO;
import cn.iocoder.yudao.module.facebook.dal.mysql.aisearch.AiSearchRunMapper;
import cn.iocoder.yudao.module.facebook.dal.mysql.aisearch.AiSearchRoundMapper;
import com.baomidou.mybatisplus.core.toolkit.Wrappers;
import cn.iocoder.yudao.module.facebook.service.aisearch.AiSearchResultService;
import io.swagger.v3.oas.annotations.tags.Tag; import jakarta.annotation.Resource; import org.springframework.web.bind.annotation.*;
import java.util.List; import static cn.iocoder.yudao.framework.common.pojo.CommonResult.success;
import cn.iocoder.yudao.module.facebook.controller.admin.aisearch.vo.AiSearchContactPageReqVO;
@Tag(name="管理后台 - AI企业获客结果") @RestController @RequestMapping("/facebook/ai-search/result")
public class AiSearchResultController { @Resource private AiSearchResultService service; @Resource private AiSearchRunMapper runMapper; @Resource private AiSearchRoundMapper roundMapper; @GetMapping("/companies") public CommonResult<List<AiSearchCompanyRespVO>> companies(@RequestParam Long taskId){return success(service.companies(taskId));} @GetMapping("/contacts") public CommonResult<List<AiSearchContactRespVO>> contacts(@RequestParam Long taskId){return success(service.contacts(taskId));} @GetMapping("/contacts/page") public CommonResult<cn.iocoder.yudao.framework.common.pojo.PageResult<AiSearchContactRespVO>> contactsPage(AiSearchContactPageReqVO req){return success(service.contactsPage(req));} @GetMapping("/company-detail") public CommonResult<AiSearchCompanyDetailRespVO> companyDetail(@RequestParam Long companyId){return success(service.companyDetail(companyId));} @GetMapping("/stats") public CommonResult<AiSearchStatsRespVO> stats(@RequestParam Long taskId){return success(service.stats(taskId));} @GetMapping("/runs") public CommonResult<List<AiSearchRunDO>> runs(@RequestParam Long taskId){return success(runMapper.selectList(Wrappers.lambdaQuery(AiSearchRunDO.class).eq(AiSearchRunDO::getTaskId,taskId).orderByDesc(AiSearchRunDO::getId)));} @GetMapping("/rounds") public CommonResult<List<AiSearchRoundDO>> rounds(@RequestParam Long taskId){return success(roundMapper.selectList(Wrappers.lambdaQuery(AiSearchRoundDO.class).eq(AiSearchRoundDO::getTaskId,taskId).orderByDesc(AiSearchRoundDO::getId)));} }
