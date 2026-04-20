package cn.iocoder.yudao.module.facebook.dal.mysql.collectdetail;

import cn.iocoder.yudao.framework.mybatis.core.mapper.BaseMapperX;
import cn.iocoder.yudao.framework.mybatis.core.query.LambdaQueryWrapperX;
import cn.iocoder.yudao.module.facebook.dal.dataobject.collectdetail.FbCollectDetailDO;
import org.apache.ibatis.annotations.Mapper;
import org.apache.ibatis.annotations.Param;
import org.apache.ibatis.annotations.Select;

import java.util.List;
import java.util.Map;

/**
 * FB采集任务明细 Mapper
 *
 * @author jacky
 */
@Mapper
public interface FbCollectDetailMapper extends BaseMapperX<FbCollectDetailDO> {

    /**
     * 查询任务的统计信息
     *
     * @param taskId 任务ID
     * @return 统计信息(total_expected, total_collected, failed_count)
     */
    @Select("SELECT " +
            "SUM(expected_count) as total_expected, " +
            "SUM(collected_count) as total_collected, " +
            "SUM(CASE WHEN status = 3 THEN 1 ELSE 0 END) as failed_count " +
            "FROM facebook_collect_detail " +
            "WHERE task_id = #{taskId} AND deleted = 0")
    Map<String, Object> selectTaskStats(@Param("taskId") Long taskId);

    /**
     * 根据任务ID查询所有明细
     *
     * @param taskId 任务ID
     * @return 明细列表
     */
    default List<FbCollectDetailDO> selectListByTaskId(Long taskId) {
        return selectList(new LambdaQueryWrapperX<FbCollectDetailDO>()
                .eq(FbCollectDetailDO::getTaskId, taskId));
    }

}
