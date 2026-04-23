package cn.iocoder.yudao.module.facebook.dal.mysql.fbcollectgroup;

import java.util.*;

import cn.iocoder.yudao.framework.common.pojo.PageResult;
import cn.iocoder.yudao.framework.mybatis.core.query.LambdaQueryWrapperX;
import cn.iocoder.yudao.framework.mybatis.core.mapper.BaseMapperX;
import cn.iocoder.yudao.module.facebook.dal.dataobject.fbcollectgroup.FbCollectGroupDO;
import org.apache.ibatis.annotations.Mapper;
import cn.iocoder.yudao.module.facebook.controller.admin.fbcollectgroup.vo.*;

/**
 * FB群组采集结果 Mapper
 *
 * @author jacky
 */
@Mapper
public interface FbCollectGroupMapper extends BaseMapperX<FbCollectGroupDO> {

    default PageResult<FbCollectGroupDO> selectPage(FbCollectGroupPageReqVO reqVO) {
        return selectPage(reqVO, new LambdaQueryWrapperX<FbCollectGroupDO>()
                .eqIfPresent(FbCollectGroupDO::getTaskId, reqVO.getTaskId())
                .likeIfPresent(FbCollectGroupDO::getGroupName, reqVO.getGroupName())
                .eqIfPresent(FbCollectGroupDO::getUrl, reqVO.getUrl())
                .eqIfPresent(FbCollectGroupDO::getType, reqVO.getType())
                .eqIfPresent(FbCollectGroupDO::getMemberQuantity, reqVO.getMemberQuantity())
                .eqIfPresent(FbCollectGroupDO::getActiveQuantity, reqVO.getActiveQuantity())
                .eqIfPresent(FbCollectGroupDO::getSysGroupId, reqVO.getSysGroupId())
                .eqIfPresent(FbCollectGroupDO::getRemark, reqVO.getRemark())
                .eqIfPresent(FbCollectGroupDO::getUserId, reqVO.getUserId())
                .eqIfPresent(FbCollectGroupDO::getDeptId, reqVO.getDeptId())
                .eqIfPresent(FbCollectGroupDO::getGroupId, reqVO.getGroupId())
                .eqIfPresent(FbCollectGroupDO::getJoinGroupTimes, reqVO.getJoinGroupTimes())
                .eqIfPresent(FbCollectGroupDO::getCommentTimes, reqVO.getCommentTimes())
                .eqIfPresent(FbCollectGroupDO::getPostTimes, reqVO.getPostTimes())
                .betweenIfPresent(FbCollectGroupDO::getCreateTime, reqVO.getCreateTime())
                .orderByDesc(FbCollectGroupDO::getId));
    }

}