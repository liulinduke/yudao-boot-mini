
package cn.iocoder.yudao.module.system.service;

import cn.iocoder.yudao.framework.common.pojo.PageResult;
import cn.iocoder.yudao.module.system.controller.admin.proxy.vo.SysProxyCreateReqVO;
import cn.iocoder.yudao.module.system.controller.admin.proxy.vo.SysProxyPageReqVO;
import cn.iocoder.yudao.module.system.controller.admin.proxy.vo.SysProxyRespVO;
import cn.iocoder.yudao.module.system.controller.admin.proxy.vo.SysProxyUpdateReqVO;
import cn.iocoder.yudao.module.system.dal.dataobject.SysProxyDO;

import java.util.List;

/**
 * 代理信息 Service 接口
 *
 * @author 芋道源码
 */
public interface SysProxyService {

    /**
     * 创建代理信息
     *
     * @param createReqVO 创建请求
     * @return 代理ID
     */
    Long createProxy(SysProxyCreateReqVO createReqVO);

    /**
     * 更新代理信息
     *
     * @param updateReqVO 更新请求
     */
    void updateProxy(SysProxyUpdateReqVO updateReqVO);

    /**
     * 删除代理信息
     *
     * @param id 代理ID
     */
    void deleteProxy(Long id);

    /**
     * 获取代理信息
     *
     * @param id 代理ID
     * @return 代理信息
     */
    SysProxyRespVO getProxy(Long id);

    /**
     * 获取代理信息（内部使用）
     *
     * @param id 代理ID
     * @return 代理信息
     */
    SysProxyDO getProxyDO(Long id);

    /**
     * 分页查询代理信息
     *
     * @param pageReqVO 分页请求
     * @return 代理信息分页结果
     */
    PageResult<SysProxyRespVO> getProxyPage(SysProxyPageReqVO pageReqVO);

    /**
     * 获取所有启用的代理列表
     *
     * @return 代理列表
     */
    List<SysProxyRespVO> getAllEnabledProxyList();

    /**
     * 获取所有代理列表（内部使用）
     *
     * @return 代理列表
     */
    List<SysProxyDO> getAllProxyList();

}
