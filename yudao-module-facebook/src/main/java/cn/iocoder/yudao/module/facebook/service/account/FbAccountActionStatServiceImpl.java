package cn.iocoder.yudao.module.facebook.service.account;

import cn.iocoder.yudao.module.facebook.dal.dataobject.account.FbAccountActionStatDO;
import cn.iocoder.yudao.module.facebook.dal.mysql.account.FbAccountActionStatMapper;
import jakarta.annotation.Resource;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import java.time.LocalDateTime;
import java.util.Collection;
import java.util.List;

@Service
public class FbAccountActionStatServiceImpl implements FbAccountActionStatService {

    @Resource
    private FbAccountActionStatMapper statMapper;

    @Override
    public List<FbAccountActionStatDO> getByAccountIds(Collection<Long> accountIds) {
        return statMapper.selectByAccountIds(accountIds);
    }

    @Override
    @Transactional(rollbackFor = Exception.class)
    public synchronized void markStarted(Long accountId, String actionType) {
        FbAccountActionStatDO stat = getOrCreate(accountId, actionType);
        stat.setLastExecuteTime(LocalDateTime.now());
        statMapper.updateById(stat);
    }

    @Override
    @Transactional(rollbackFor = Exception.class)
    public synchronized void recordSuccess(Long accountId, String actionType, long actionCount, long collectCount) {
        FbAccountActionStatDO stat = getOrCreate(accountId, actionType);
        stat.setTotalTaskCount(value(stat.getTotalTaskCount()) + 1);
        stat.setTotalActionCount(value(stat.getTotalActionCount()) + Math.max(0, actionCount));
        stat.setTotalCollectCount(value(stat.getTotalCollectCount()) + Math.max(0, collectCount));
        LocalDateTime now = LocalDateTime.now();
        stat.setLastExecuteTime(now);
        stat.setLastSuccessTime(now);
        statMapper.updateById(stat);
    }

    private FbAccountActionStatDO getOrCreate(Long accountId, String actionType) {
        FbAccountActionStatDO stat = statMapper.selectByAccountAndType(accountId, actionType);
        if (stat != null) {
            return stat;
        }
        stat = FbAccountActionStatDO.builder()
                .accountId(accountId)
                .actionType(actionType)
                .totalTaskCount(0L)
                .totalActionCount(0L)
                .totalCollectCount(0L)
                .lastExecuteTime(LocalDateTime.now())
                .build();
        statMapper.insert(stat);
        return stat;
    }

    private long value(Long value) {
        return value == null ? 0L : value;
    }
}
