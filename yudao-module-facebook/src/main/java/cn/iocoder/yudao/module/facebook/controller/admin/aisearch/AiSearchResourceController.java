package cn.iocoder.yudao.module.facebook.controller.admin.aisearch;

import cn.iocoder.yudao.framework.common.pojo.CommonResult;
import cn.iocoder.yudao.module.facebook.controller.admin.aisearch.vo.AiSearchCustomerPageReqVO;
import cn.iocoder.yudao.module.facebook.service.aisearch.AiSearchResultService;
import io.swagger.v3.oas.annotations.tags.Tag;
import jakarta.annotation.Resource;
import org.springframework.web.bind.annotation.*;
import cn.iocoder.yudao.framework.common.pojo.PageResult;
import jakarta.servlet.http.HttpServletResponse;
import java.nio.charset.StandardCharsets;
import static cn.iocoder.yudao.framework.common.pojo.CommonResult.success;

@Tag(name = "管理后台 - AI企业获客客户资源库")
@RestController
@RequestMapping("/facebook/ai-search/resource")
public class AiSearchResourceController {
    @Resource private AiSearchResultService service;

    @GetMapping("/customers")
    public CommonResult<PageResult<cn.iocoder.yudao.module.facebook.controller.admin.aisearch.vo.AiSearchContactRespVO>> customers(AiSearchCustomerPageReqVO req) {
        return success(service.customers(req));
    }

    @GetMapping("/customers/export")
    public void exportCustomers(AiSearchCustomerPageReqVO req, HttpServletResponse response) throws Exception {
        PageResult<cn.iocoder.yudao.module.facebook.controller.admin.aisearch.vo.AiSearchContactRespVO> page = service.customers(req);
        response.setContentType("text/csv;charset=UTF-8");
        response.setHeader("Content-Disposition", "attachment; filename=ai-customers.csv");
        response.getOutputStream().write("企业,国家/地区,联系人,职位,邮箱,电话\n".getBytes(StandardCharsets.UTF_8));
        for (var c : page.getList()) {
            String line = String.join(",", java.util.stream.Stream.of(c.getCompanyName(), c.getCompanyCountry(), c.getName(), c.getJobTitle(), c.getEmail(), c.getPhone()).map(v -> v == null ? "" : v.replace(",", " ")).toList()) + "\n";
            response.getOutputStream().write(line.getBytes(StandardCharsets.UTF_8));
        }
    }
}
