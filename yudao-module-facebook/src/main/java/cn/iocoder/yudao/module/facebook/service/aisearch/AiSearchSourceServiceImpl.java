package cn.iocoder.yudao.module.facebook.service.aisearch;
import cn.iocoder.yudao.framework.common.util.object.BeanUtils;
import cn.iocoder.yudao.module.facebook.controller.admin.aisearch.vo.*;
import cn.iocoder.yudao.module.facebook.dal.dataobject.aisearch.AiSearchSourceDO;
import cn.iocoder.yudao.module.facebook.dal.mysql.aisearch.AiSearchSourceMapper;
import com.baomidou.mybatisplus.core.toolkit.Wrappers;
import jakarta.annotation.Resource; import org.springframework.stereotype.Service;
import java.util.List;
import java.net.URI;
@Service public class AiSearchSourceServiceImpl implements AiSearchSourceService {
    @Resource private AiSearchSourceMapper mapper;
    public Long create(AiSearchSourceSaveReqVO req) { if (req == null || req.getName() == null || req.getName().isBlank()) throw new IllegalArgumentException("渠道名称不能为空"); AiSearchSourceDO d=BeanUtils.toBean(req,AiSearchSourceDO.class); d.setDomain(normalizeDomain(d.getDomain())); if (d.getDomain() != null && mapper.selectCount(Wrappers.lambdaQuery(AiSearchSourceDO.class).eq(AiSearchSourceDO::getDomain,d.getDomain())) > 0) throw new IllegalArgumentException("渠道域名已存在"); d.setStatus("CANDIDATE"); mapper.insert(d); return d.getId(); }
    public int createBatch(List<AiSearchSourceSaveReqVO> requests) { int count=0; if (requests == null) return 0; java.util.Set<String> seen = new java.util.HashSet<>(); for (AiSearchSourceSaveReqVO req : requests) { if (req == null || req.getName() == null || req.getName().isBlank()) continue; String domain = normalizeDomain(req.getDomain()); if (domain != null && (!seen.add(domain) || mapper.selectCount(Wrappers.lambdaQuery(AiSearchSourceDO.class).eq(AiSearchSourceDO::getDomain, domain)) > 0)) continue; AiSearchSourceDO d=BeanUtils.toBean(req,AiSearchSourceDO.class); d.setDomain(domain); d.setStatus("CANDIDATE"); mapper.insert(d); count++; } return count; }
    public void update(AiSearchSourceSaveReqVO req) { if (req == null || req.getId() == null) throw new IllegalArgumentException("渠道编号不能为空"); AiSearchSourceDO d=BeanUtils.toBean(req,AiSearchSourceDO.class); d.setDomain(normalizeDomain(d.getDomain())); if (d.getDomain() != null && mapper.selectCount(Wrappers.lambdaQuery(AiSearchSourceDO.class).eq(AiSearchSourceDO::getDomain,d.getDomain()).ne(AiSearchSourceDO::getId,d.getId())) > 0) throw new IllegalArgumentException("渠道域名已存在"); mapper.updateById(d); }
    public void updateStatus(Long id,String status) { if (!List.of("ACTIVE","CANDIDATE","LOW_YIELD","PAUSED","RETIRED").contains(status)) throw new IllegalArgumentException("渠道状态不正确"); AiSearchSourceDO d=new AiSearchSourceDO(); d.setId(id); d.setStatus(status); mapper.updateById(d); }
    public List<AiSearchSourceRespVO> list(String status) { return mapper.selectList(Wrappers.lambdaQuery(AiSearchSourceDO.class).eq(status!=null&&!status.isBlank(),AiSearchSourceDO::getStatus,status).orderByDesc(AiSearchSourceDO::getPriority).orderByDesc(AiSearchSourceDO::getYieldScore)).stream().map(AiSearchSourceRespVO::from).toList(); }
    private String normalizeDomain(String value) { if (value == null || value.isBlank()) return null; String text = value.trim(); try { URI uri = URI.create(text.contains("://") ? text : "https://" + text); if (uri.getHost() != null) return uri.getHost().replaceFirst("^www\\.", "").toLowerCase(java.util.Locale.ROOT); } catch (Exception ignored) { } return text.replaceFirst("^www\\.", "").split("/", 2)[0].toLowerCase(java.util.Locale.ROOT); }
}
