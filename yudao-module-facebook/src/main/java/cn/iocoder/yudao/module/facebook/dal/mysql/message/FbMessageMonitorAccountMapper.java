package cn.iocoder.yudao.module.facebook.dal.mysql.message;

import cn.iocoder.yudao.framework.mybatis.core.mapper.BaseMapperX;
import cn.iocoder.yudao.module.facebook.dal.dataobject.message.FbMessageMonitorAccountDO;
import com.baomidou.mybatisplus.core.conditions.query.LambdaQueryWrapper;
import org.apache.ibatis.annotations.Mapper;

import java.util.List;

@Mapper
public interface FbMessageMonitorAccountMapper extends BaseMapperX<FbMessageMonitorAccountDO> {
    default FbMessageMonitorAccountDO selectByAccountId(Long accountId) {
        return selectOne(new LambdaQueryWrapper<FbMessageMonitorAccountDO>()
                .eq(FbMessageMonitorAccountDO::getAccountId, accountId));
    }

    default List<FbMessageMonitorAccountDO> selectEnabledList() {
        return selectList(new LambdaQueryWrapper<FbMessageMonitorAccountDO>()
                .eq(FbMessageMonitorAccountDO::getReceiveEnabled, 1)
                .eq(FbMessageMonitorAccountDO::getStatus, 1)
                .orderByAsc(FbMessageMonitorAccountDO::getNextCheckTime));
    }
}
