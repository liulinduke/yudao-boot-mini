package cn.iocoder.yudao.module.facebook.dal.mysql.dmtask;

import cn.iocoder.yudao.framework.common.pojo.PageResult;
import cn.iocoder.yudao.framework.mybatis.core.mapper.BaseMapperX;
import cn.iocoder.yudao.framework.mybatis.core.query.LambdaQueryWrapperX;
import cn.iocoder.yudao.module.facebook.controller.admin.dmtask.vo.FbDmTaskPageReqVO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.dmtask.FbDmTaskDO;
import org.apache.ibatis.annotations.Mapper;

import java.util.List;

/**
 * Facebook 群发私信任务 Mapper
 *
 * @author 芋道源码
 */
@Mapper
public interface FbDmTaskMapper extends BaseMapperX<FbDmTaskDO> {

    default PageResult<FbDmTaskDO> selectPage(FbDmTaskPageReqVO reqVO) {
        return selectPage(reqVO, new LambdaQueryWrapperX<FbDmTaskDO>()
                .likeIfPresent(FbDmTaskDO::getTaskName, reqVO.getTaskName())
                .eqIfPresent(FbDmTaskDO::getStatus, reqVO.getStatus())
                .orderByDesc(FbDmTaskDO::getCreateTime));
    }

    /**
     * 查询待执行的任务列表
     *
     * @return 待执行任务列表
     */
    default List<FbDmTaskDO> selectPendingList() {
        return selectList(FbDmTaskDO::getStatus, 0);
    }

}
