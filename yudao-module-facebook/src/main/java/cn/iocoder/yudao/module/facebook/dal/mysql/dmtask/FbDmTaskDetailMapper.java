package cn.iocoder.yudao.module.facebook.dal.mysql.dmtask;

import cn.iocoder.yudao.framework.mybatis.core.mapper.BaseMapperX;
import cn.iocoder.yudao.module.facebook.dal.dataobject.dmtask.FbDmTaskDetailDO;
import org.apache.ibatis.annotations.Mapper;

import java.util.List;

/**
 * Facebook 群发私信任务明细 Mapper
 *
 * @author 芋道源码
 */
@Mapper
public interface FbDmTaskDetailMapper extends BaseMapperX<FbDmTaskDetailDO> {

    default List<FbDmTaskDetailDO> selectDueScheduled(Integer limit) {
        return selectList(new com.baomidou.mybatisplus.core.conditions.query.LambdaQueryWrapper<FbDmTaskDetailDO>()
                .eq(FbDmTaskDetailDO::getStatus, 0)
                .isNotNull(FbDmTaskDetailDO::getScheduledTime)
                .le(FbDmTaskDetailDO::getScheduledTime, java.time.LocalDateTime.now())
                .orderByAsc(FbDmTaskDetailDO::getScheduledTime)
                .last("LIMIT " + Math.max(1, Math.min(limit == null ? 200 : limit, 500))));
    }

    /**
     * 根据任务ID查询明细列表
     *
     * @param taskId 任务ID
     * @return 明细列表
     */
    default List<FbDmTaskDetailDO> selectListByTaskId(Long taskId) {
        return selectList(FbDmTaskDetailDO::getTaskId, taskId);
    }

    /**
     * 根据账号ID查询待执行的明细
     *
     * @param accountId 账号ID
     * @return 待执行明细列表
     */
    default List<FbDmTaskDetailDO> selectPendingByAccountId(String accountId) {
        return selectList(new com.baomidou.mybatisplus.core.conditions.query.LambdaQueryWrapper<FbDmTaskDetailDO>()
                .eq(FbDmTaskDetailDO::getAccountId, accountId)
                .eq(FbDmTaskDetailDO::getStatus, 0));
    }

}
