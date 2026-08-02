package cn.iocoder.yudao.module.facebook.dal.mysql.account;

import cn.iocoder.yudao.framework.mybatis.core.mapper.BaseMapperX;
import cn.iocoder.yudao.framework.mybatis.core.query.LambdaQueryWrapperX;
import cn.iocoder.yudao.module.facebook.dal.dataobject.account.FbAccountActionStatDO;
import org.apache.ibatis.annotations.Mapper;

import java.util.Collection;
import java.util.List;

@Mapper
public interface FbAccountActionStatMapper extends BaseMapperX<FbAccountActionStatDO> {

    default FbAccountActionStatDO selectByAccountAndType(Long accountId, String actionType) {
        return selectOne(new LambdaQueryWrapperX<FbAccountActionStatDO>()
                .eq(FbAccountActionStatDO::getAccountId, accountId)
                .eq(FbAccountActionStatDO::getActionType, actionType));
    }

    default List<FbAccountActionStatDO> selectByAccountIds(Collection<Long> accountIds) {
        if (accountIds == null || accountIds.isEmpty()) {
            return List.of();
        }
        return selectList(new LambdaQueryWrapperX<FbAccountActionStatDO>()
                .in(FbAccountActionStatDO::getAccountId, accountIds));
    }
}
