package cn.iocoder.yudao.module.facebook.dal.mysql.warmup;

import cn.iocoder.yudao.framework.mybatis.core.mapper.BaseMapperX;
import cn.iocoder.yudao.module.facebook.dal.dataobject.warmup.FbWarmupTaskDetailDO;
import org.apache.ibatis.annotations.Mapper;

import java.util.List;

@Mapper
public interface FbWarmupTaskDetailMapper extends BaseMapperX<FbWarmupTaskDetailDO> {
    default List<FbWarmupTaskDetailDO> selectListByTaskId(Long taskId) {
        return selectList(FbWarmupTaskDetailDO::getTaskId, taskId);
    }
}
