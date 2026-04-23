package cn.iocoder.yudao.module.facebook.dal.mysql.operation;

import cn.iocoder.yudao.framework.mybatis.core.mapper.BaseMapperX;
import cn.iocoder.yudao.framework.mybatis.core.query.LambdaQueryWrapperX;
import cn.iocoder.yudao.module.facebook.dal.dataobject.operation.FbOperationTaskDetailDO;
import org.apache.ibatis.annotations.Mapper;

import java.util.List;

/**
 * 运营任务明细表 Mapper
 *
 * @author 芋道源码
 */
@Mapper
public interface FbOperationTaskDetailMapper extends BaseMapperX<FbOperationTaskDetailDO> {

    default List<FbOperationTaskDetailDO> selectListByTaskId(Long taskId) {
        return selectList(FbOperationTaskDetailDO::getTaskId, taskId);
    }

    default List<FbOperationTaskDetailDO> selectPendingDetailsByAccountId(String accountId) {
        return selectList(new LambdaQueryWrapperX<FbOperationTaskDetailDO>()
                .eq(FbOperationTaskDetailDO::getAccountId, accountId)
                .eq(FbOperationTaskDetailDO::getStatus, 0)); // 0-待执行
    }

}
