package cn.iocoder.yudao.module.facebook.dal.mysql.operation;

import cn.iocoder.yudao.framework.mybatis.core.mapper.BaseMapperX;
import cn.iocoder.yudao.module.facebook.dal.dataobject.operation.FbRepostResultDO;
import org.apache.ibatis.annotations.Mapper;

import java.util.List;

/**
 * Facebook转帖结果表 Mapper
 *
 * @author 芋道源码
 */
@Mapper
public interface FbRepostResultMapper extends BaseMapperX<FbRepostResultDO> {

    default List<FbRepostResultDO> selectListByTaskId(Long taskId) {
        return selectList(FbRepostResultDO::getTaskId, taskId);
    }

    default List<FbRepostResultDO> selectListByDetailId(Long detailId) {
        return selectList(FbRepostResultDO::getDetailId, detailId);
    }

}
