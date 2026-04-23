package cn.iocoder.yudao.module.facebook.dal.mysql.operation;

import cn.iocoder.yudao.framework.mybatis.core.mapper.BaseMapperX;
import cn.iocoder.yudao.module.facebook.dal.dataobject.operation.FbOperationAddGroupResultDO;
import org.apache.ibatis.annotations.Mapper;

import java.util.List;

/**
 * 链接加组结果表 Mapper
 *
 * @author 芋道源码
 */
@Mapper
public interface FbOperationAddGroupResultMapper extends BaseMapperX<FbOperationAddGroupResultDO> {

    default List<FbOperationAddGroupResultDO> selectListByDetailId(Long detailId) {
        return selectList(FbOperationAddGroupResultDO::getDetailId, detailId);
    }

    default List<FbOperationAddGroupResultDO> selectListByTaskId(Long taskId) {
        return selectList(FbOperationAddGroupResultDO::getTaskId, taskId);
    }

}
