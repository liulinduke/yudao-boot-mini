package cn.iocoder.yudao.module.facebook.dal.mysql.operation;

import cn.iocoder.yudao.framework.mybatis.core.mapper.BaseMapperX;
import cn.iocoder.yudao.module.facebook.dal.dataobject.operation.FbOperationAddGroupResultDO;
import org.apache.ibatis.annotations.Mapper;
import org.apache.ibatis.annotations.Param;
import org.apache.ibatis.annotations.Select;

import java.util.List;

/**
 * 链接加组结果表 Mapper
 *
 * @author 芋道源码
 */
@Mapper
public interface FbOperationAddGroupResultMapper extends BaseMapperX<FbOperationAddGroupResultDO> {

    @Select("""
            <script>
            SELECT r.account_id AS accountId, COUNT(DISTINCT r.group_id) AS groupCount
            FROM fb_operation_add_group_result r
            INNER JOIN fb_operation_task t ON t.id = r.task_id
            WHERE t.task_type = 9
              AND r.join_status = 1
            <if test="joinedBeforeDays != null and joinedBeforeDays &gt; 0">
              AND r.join_time &lt;= DATE_SUB(NOW(), INTERVAL #{joinedBeforeDays} DAY)
            </if>
            <if test="groupName != null and groupName != ''">
              AND r.group_name LIKE CONCAT('%', #{groupName}, '%')
            </if>
            <if test="accountIds != null and accountIds.size() &gt; 0">
              AND r.account_id IN
              <foreach collection="accountIds" item="accountId" open="(" separator="," close=")">
                #{accountId}
              </foreach>
            </if>
            <if test="groupIds != null and groupIds.size() &gt; 0">
              AND r.group_id IN
              <foreach collection="groupIds" item="groupId" open="(" separator="," close=")">
                #{groupId}
              </foreach>
            </if>
            GROUP BY r.account_id
            HAVING COUNT(DISTINCT r.group_id) &gt;= #{minGroupCount}
            </script>
            """)
    List<FbOperationGroupAccountCountDTO> selectSelectorAccountCounts(
            @Param("accountIds") List<String> accountIds,
            @Param("groupIds") List<String> groupIds,
            @Param("groupName") String groupName,
            @Param("joinedBeforeDays") Integer joinedBeforeDays,
            @Param("minGroupCount") Integer minGroupCount);

    default List<FbOperationAddGroupResultDO> selectListByDetailId(Long detailId) {
        return selectList(FbOperationAddGroupResultDO::getDetailId, detailId);
    }

    default List<FbOperationAddGroupResultDO> selectListByTaskId(Long taskId) {
        return selectList(FbOperationAddGroupResultDO::getTaskId, taskId);
    }

}
