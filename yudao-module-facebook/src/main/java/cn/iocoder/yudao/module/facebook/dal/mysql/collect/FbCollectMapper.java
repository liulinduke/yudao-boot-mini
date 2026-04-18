package cn.iocoder.yudao.module.facebook.dal.mysql.collect;

import java.util.*;

import cn.iocoder.yudao.framework.common.pojo.PageResult;
import cn.iocoder.yudao.framework.mybatis.core.query.LambdaQueryWrapperX;
import cn.iocoder.yudao.framework.mybatis.core.mapper.BaseMapperX;
import cn.iocoder.yudao.module.facebook.dal.dataobject.collect.FbCollectDO;
import org.apache.ibatis.annotations.Mapper;
import cn.iocoder.yudao.module.facebook.controller.admin.collect.vo.*;

/**
 * FB采集任务 Mapper
 *
 * @author jacky
 */
@Mapper
public interface FbCollectMapper extends BaseMapperX<FbCollectDO> {

    default PageResult<FbCollectDO> selectPage(FbCollectPageReqVO reqVO) {
        return selectPage(reqVO, new LambdaQueryWrapperX<FbCollectDO>()
                .eqIfPresent(FbCollectDO::getFbAccount, reqVO.getFbAccount())
                .eqIfPresent(FbCollectDO::getTaskType, reqVO.getTaskType())
                .eqIfPresent(FbCollectDO::getSearchUrl, reqVO.getSearchUrl())
                .eqIfPresent(FbCollectDO::getSearchType, reqVO.getSearchType())
                .eqIfPresent(FbCollectDO::getExpectedCount, reqVO.getExpectedCount())
                .eqIfPresent(FbCollectDO::getIntervalSeconds, reqVO.getIntervalSeconds())
                .eqIfPresent(FbCollectDO::getStatus, reqVO.getStatus())
                .eqIfPresent(FbCollectDO::getCollectedCount, reqVO.getCollectedCount())
                .eqIfPresent(FbCollectDO::getErrorMessage, reqVO.getErrorMessage())
                .betweenIfPresent(FbCollectDO::getStartTime, reqVO.getStartTime())
                .betweenIfPresent(FbCollectDO::getEndTime, reqVO.getEndTime())
                .eqIfPresent(FbCollectDO::getRemark, reqVO.getRemark())
                .betweenIfPresent(FbCollectDO::getCreateTime, reqVO.getCreateTime())
                .orderByDesc(FbCollectDO::getId));
    }

}