package cn.iocoder.yudao.module.facebook.dal.mysql.warmup;

import cn.iocoder.yudao.framework.common.pojo.PageResult;
import cn.iocoder.yudao.framework.mybatis.core.mapper.BaseMapperX;
import cn.iocoder.yudao.framework.mybatis.core.query.LambdaQueryWrapperX;
import cn.iocoder.yudao.module.facebook.controller.admin.warmup.vo.FbWarmupTaskPageReqVO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.warmup.FbWarmupTaskDO;
import org.apache.ibatis.annotations.Mapper;

@Mapper
public interface FbWarmupTaskMapper extends BaseMapperX<FbWarmupTaskDO> {
    default PageResult<FbWarmupTaskDO> selectPage(FbWarmupTaskPageReqVO reqVO) {
        LambdaQueryWrapperX<FbWarmupTaskDO> wrapper = new LambdaQueryWrapperX<>();
        wrapper.eqIfPresent(FbWarmupTaskDO::getStatus, reqVO.getStatus());
        wrapper.ne(FbWarmupTaskDO::getStatus, 5);
        wrapper.orderByDesc(FbWarmupTaskDO::getScheduleTime);
        return selectPage(reqVO, wrapper);
    }
}
