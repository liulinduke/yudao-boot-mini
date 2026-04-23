package cn.iocoder.yudao.module.facebook.dal.mysql.operation;

import cn.iocoder.yudao.framework.mybatis.core.mapper.BaseMapperX;
import cn.iocoder.yudao.framework.mybatis.core.query.LambdaQueryWrapperX;
import cn.iocoder.yudao.module.facebook.dal.dataobject.operation.FbOperationTaskDO;
import org.apache.ibatis.annotations.Mapper;

/**
 * 运营任务主表 Mapper
 *
 * @author 芋道源码
 */
@Mapper
public interface FbOperationTaskMapper extends BaseMapperX<FbOperationTaskDO> {

    default FbOperationTaskDO selectByTaskName(String taskName) {
        return selectOne(FbOperationTaskDO::getTaskName, taskName);
    }

}
