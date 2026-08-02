package cn.iocoder.yudao.module.facebook.service.account;

import cn.iocoder.yudao.module.facebook.dal.dataobject.account.FbAccountActionStatDO;

import java.util.Collection;
import java.util.List;

public interface FbAccountActionStatService {

    List<FbAccountActionStatDO> getByAccountIds(Collection<Long> accountIds);

    void markStarted(Long accountId, String actionType);

    void recordSuccess(Long accountId, String actionType, long actionCount, long collectCount);
}
