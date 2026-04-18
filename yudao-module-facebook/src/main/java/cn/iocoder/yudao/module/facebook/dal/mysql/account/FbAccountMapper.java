package cn.iocoder.yudao.module.facebook.dal.mysql.account;

import java.util.*;

import cn.iocoder.yudao.framework.common.pojo.PageResult;
import cn.iocoder.yudao.framework.mybatis.core.query.LambdaQueryWrapperX;
import cn.iocoder.yudao.framework.mybatis.core.mapper.BaseMapperX;
import cn.iocoder.yudao.module.facebook.dal.dataobject.account.FbAccountDO;
import org.apache.ibatis.annotations.Mapper;
import cn.iocoder.yudao.module.facebook.controller.admin.account.vo.*;

/**
 * FB账号 Mapper
 *
 * @author 芋道源码
 */
@Mapper
public interface FbAccountMapper extends BaseMapperX<FbAccountDO> {

    default PageResult<FbAccountDO> selectPage(FbAccountPageReqVO reqVO) {
        return selectPage(reqVO, new LambdaQueryWrapperX<FbAccountDO>()
                .eqIfPresent(FbAccountDO::getFbAccount, reqVO.getFbAccount())
                .eqIfPresent(FbAccountDO::getPassword, reqVO.getPassword())
                .eqIfPresent(FbAccountDO::getArea, reqVO.getArea())
                .eqIfPresent(FbAccountDO::getFriends, reqVO.getFriends())
                .eqIfPresent(FbAccountDO::getGroupId, reqVO.getGroupId())
                .eqIfPresent(FbAccountDO::getStatus, reqVO.getStatus())
                .eqIfPresent(FbAccountDO::getRemark, reqVO.getRemark())
                .eqIfPresent(FbAccountDO::getCookie, reqVO.getCookie())
                .eqIfPresent(FbAccountDO::getUserAgent, reqVO.getUserAgent())
                .eqIfPresent(FbAccountDO::getTfa, reqVO.getTfa())
                .eqIfPresent(FbAccountDO::getEmail, reqVO.getEmail())
                .eqIfPresent(FbAccountDO::getEmailPassword, reqVO.getEmailPassword())
                .eqIfPresent(FbAccountDO::getDeviceId, reqVO.getDeviceId())
                .likeIfPresent(FbAccountDO::getDeviceName, reqVO.getDeviceName())
                .eqIfPresent(FbAccountDO::getReason, reqVO.getReason())
                .eqIfPresent(FbAccountDO::getProxy, reqVO.getProxy())
                .eqIfPresent(FbAccountDO::getProxyId, reqVO.getProxyId())
                .betweenIfPresent(FbAccountDO::getCreationDate, reqVO.getCreationDate())
                .betweenIfPresent(FbAccountDO::getCreateTime, reqVO.getCreateTime())
                .orderByDesc(FbAccountDO::getId));
    }

}