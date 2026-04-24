package cn.iocoder.yudao.module.facebook.dal.mysql.accountgroup;

import cn.iocoder.yudao.framework.common.pojo.PageResult;
import cn.iocoder.yudao.framework.mybatis.core.mapper.BaseMapperX;
import cn.iocoder.yudao.framework.mybatis.core.query.LambdaQueryWrapperX;
import cn.iocoder.yudao.module.facebook.controller.admin.accountgroup.vo.FbAccountGroupPageReqVO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.accountgroup.FbAccountGroupDO;
import org.apache.ibatis.annotations.Mapper;

/**
 * Facebook 账号分组 Mapper
 *
 * @author 芋道源码
 */
@Mapper
public interface FbAccountGroupMapper extends BaseMapperX<FbAccountGroupDO> {

    default PageResult<FbAccountGroupDO> selectPage(FbAccountGroupPageReqVO reqVO) {
        return selectPage(reqVO, new LambdaQueryWrapperX<FbAccountGroupDO>()
                .likeIfPresent(FbAccountGroupDO::getGroupName, reqVO.getGroupName())
                .eqIfPresent(FbAccountGroupDO::getStatus, reqVO.getStatus())
                .orderByAsc(FbAccountGroupDO::getSort));
    }

}
