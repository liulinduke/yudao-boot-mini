
package cn.iocoder.yudao.module.system.dal.mapper;

import cn.iocoder.yudao.framework.common.pojo.PageResult;
import cn.iocoder.yudao.framework.mybatis.core.mapper.BaseMapperX;
import cn.iocoder.yudao.framework.mybatis.core.query.LambdaQueryWrapperX;
import cn.iocoder.yudao.module.system.controller.admin.proxy.vo.SysProxyPageReqVO;
import cn.iocoder.yudao.module.system.dal.dataobject.SysProxyDO;
import org.apache.ibatis.annotations.Mapper;

import java.util.List;

/**
 * 代理信息 Mapper
 *
 * @author 芋道源码
 */
@Mapper
public interface SysProxyMapper extends BaseMapperX<SysProxyDO> {

    default List<SysProxyDO> selectListByStatus(Integer status) {
        return selectList(new LambdaQueryWrapperX<SysProxyDO>()
                .eqIfPresent(SysProxyDO::getStatus, status)
                .orderByAsc(SysProxyDO::getCreateTime));
    }

    default List<SysProxyDO> selectAllEnabled() {
        return selectListByStatus(1);
    }

    default PageResult<SysProxyDO> selectPage(SysProxyPageReqVO pageReqVO) {
        return selectPage(pageReqVO, new LambdaQueryWrapperX<SysProxyDO>()
                .likeIfPresent(SysProxyDO::getProxyName, pageReqVO.getProxyName())
                .eqIfPresent(SysProxyDO::getProxyType, pageReqVO.getProxyType())
                .likeIfPresent(SysProxyDO::getHost, pageReqVO.getHost())
                .eqIfPresent(SysProxyDO::getStatus, pageReqVO.getStatus())
                .likeIfPresent(SysProxyDO::getCountry, pageReqVO.getCountry())
                .orderByDesc(SysProxyDO::getCreateTime));
    }

}
