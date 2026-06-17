
package cn.iocoder.yudao.module.system.service.impl;

import cn.iocoder.yudao.framework.common.pojo.PageResult;
import cn.iocoder.yudao.framework.mybatis.core.query.LambdaQueryWrapperX;
import cn.iocoder.yudao.module.system.controller.admin.proxy.vo.SysProxyCreateReqVO;
import cn.iocoder.yudao.module.system.controller.admin.proxy.vo.SysProxyPageReqVO;
import cn.iocoder.yudao.module.system.controller.admin.proxy.vo.SysProxyRespVO;
import cn.iocoder.yudao.module.system.controller.admin.proxy.vo.SysProxyUpdateReqVO;
import cn.iocoder.yudao.framework.common.util.object.BeanUtils;
import cn.iocoder.yudao.module.system.dal.dataobject.SysProxyDO;
import cn.iocoder.yudao.module.system.dal.mapper.SysProxyMapper;
import cn.iocoder.yudao.module.system.service.SysProxyService;
import jakarta.annotation.Resource;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import java.util.List;
import java.util.stream.Collectors;

/**
 * 代理信息 Service 实现类
 *
 * @author 芋道源码
 */
@Service
@Transactional(readOnly = true)
public class SysProxyServiceImpl implements SysProxyService {

    @Resource
    private SysProxyMapper proxyMapper;

    @Override
    @Transactional
    public Long createProxy(SysProxyCreateReqVO createReqVO) {
        SysProxyDO proxy = BeanUtils.toBean(createReqVO, SysProxyDO.class);
        if (proxy.getStatus() == null) {
            proxy.setStatus(1);
        }
        proxyMapper.insert(proxy);
        return proxy.getId();
    }

    @Override
    @Transactional
    public void updateProxy(SysProxyUpdateReqVO updateReqVO) {
        SysProxyDO proxy = BeanUtils.toBean(updateReqVO, SysProxyDO.class);
        proxyMapper.updateById(proxy);
    }

    @Override
    @Transactional
    public void deleteProxy(Long id) {
        proxyMapper.deleteById(id);
    }

    @Override
    public SysProxyRespVO getProxy(Long id) {
        SysProxyDO proxy = proxyMapper.selectById(id);
        return convertToRespVO(proxy);
    }

    @Override
    public SysProxyDO getProxyDO(Long id) {
        return proxyMapper.selectById(id);
    }

    @Override
    public PageResult<SysProxyRespVO> getProxyPage(SysProxyPageReqVO pageReqVO) {
        PageResult<SysProxyDO> pageResult = proxyMapper.selectPage(pageReqVO);
        List<SysProxyRespVO> respVOList = pageResult.getList().stream()
                .map(this::convertToRespVO)
                .collect(Collectors.toList());
        return new PageResult<>(respVOList, pageResult.getTotal());
    }

    @Override
    public List<SysProxyRespVO> getAllEnabledProxyList() {
        List<SysProxyDO> list = proxyMapper.selectAllEnabled();
        return list.stream().map(this::convertToRespVO).collect(Collectors.toList());
    }

    @Override
    public List<SysProxyDO> getAllProxyList() {
        return proxyMapper.selectList();
    }

    private SysProxyRespVO convertToRespVO(SysProxyDO proxy) {
        SysProxyRespVO respVO = BeanUtils.toBean(proxy, SysProxyRespVO.class);
        respVO.setProxyTypeName(getProxyTypeName(proxy.getProxyType()));
        respVO.setStatusName(proxy.getStatus() == 1 ? "启用" : "禁用");
        return respVO;
    }

    private String getProxyTypeName(Integer proxyType) {
        if (proxyType == null) {
            return "";
        }
        switch (proxyType) {
            case 1:
                return "HTTP";
            case 2:
                return "HTTPS";
            case 3:
                return "SOCKS5";
            default:
                return "未知";
        }
    }

}
