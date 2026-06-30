package cn.iocoder.yudao.module.facebook.service.collectdetail;

import cn.hutool.core.collection.CollUtil;
import cn.hutool.core.util.StrUtil;
import cn.iocoder.yudao.module.facebook.controller.admin.collectdetail.vo.FbCollectPendingDetailRespVO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.account.FbAccountDO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.collect.FbCollectDO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.collectdetail.FbCollectDetailDO;
import cn.iocoder.yudao.module.facebook.dal.mysql.account.FbAccountMapper;
import cn.iocoder.yudao.module.facebook.dal.mysql.collect.FbCollectMapper;
import cn.iocoder.yudao.module.facebook.dal.mysql.collectdetail.FbCollectDetailMapper;
import com.baomidou.mybatisplus.core.conditions.query.LambdaQueryWrapper;
import org.springframework.stereotype.Service;
import org.springframework.validation.annotation.Validated;

import jakarta.annotation.Resource;
import java.time.LocalDateTime;
import java.util.ArrayList;
import java.util.List;
import java.util.Map;
import java.util.Objects;
import java.util.function.Function;
import java.util.stream.Collectors;

/**
 * FB采集任务明细 Service 实现类
 *
 * @author jacky
 */
@Service
@Validated
public class FbCollectDetailServiceImpl implements FbCollectDetailService {

    @Resource
    private FbCollectDetailMapper fbCollectDetailMapper;
    @Resource
    private FbCollectMapper fbCollectMapper;
    @Resource
    private FbAccountMapper fbAccountMapper;

    @Override
    public List<FbCollectDetailDO> getPendingDetailsByAccount(String fbAccount) {
        return fbCollectDetailMapper.selectList(
            new LambdaQueryWrapper<FbCollectDetailDO>()
                .eq(FbCollectDetailDO::getFbAccount, fbAccount)
                .eq(FbCollectDetailDO::getStatus, 0) // 待执行
                .orderByAsc(FbCollectDetailDO::getId)
                .last("LIMIT 1")
        );
    }

    @Override
    public List<FbCollectDetailDO> getDetailListByTaskId(Long taskId) {
        return fbCollectDetailMapper.selectList(
            new LambdaQueryWrapper<FbCollectDetailDO>()
                .eq(FbCollectDetailDO::getTaskId, taskId)
                .orderByAsc(FbCollectDetailDO::getId)
        );
    }

    @Override
    public List<FbCollectPendingDetailRespVO> claimPendingDetails(Integer limit) {
        int size = Math.max(1, Math.min(limit == null ? 3 : limit, 10));
        List<FbCollectDetailDO> candidates = fbCollectDetailMapper.selectList(
                new LambdaQueryWrapper<FbCollectDetailDO>()
                        .eq(FbCollectDetailDO::getStatus, 0)
                        .orderByAsc(FbCollectDetailDO::getId)
                        .last("LIMIT " + (size * 5))
        );
        if (CollUtil.isEmpty(candidates)) {
            return List.of();
        }

        List<Long> taskIds = candidates.stream()
                .map(FbCollectDetailDO::getTaskId)
                .filter(Objects::nonNull)
                .distinct()
                .collect(Collectors.toList());
        Map<Long, FbCollectDO> taskMap = fbCollectMapper.selectBatchIds(taskIds).stream()
                .collect(Collectors.toMap(FbCollectDO::getId, Function.identity(), (a, b) -> a));

        List<FbCollectDetailDO> details = candidates.stream()
                .filter(detail -> {
                    FbCollectDO task = taskMap.get(detail.getTaskId());
                    return task != null && StrUtil.startWith(task.getRemark(), "AI_AGENT_");
                })
                .limit(size)
                .collect(Collectors.toList());
        if (CollUtil.isEmpty(details)) {
            return List.of();
        }

        List<String> fbAccounts = details.stream()
                .map(FbCollectDetailDO::getFbAccount)
                .filter(Objects::nonNull)
                .distinct()
                .collect(Collectors.toList());
        Map<String, FbAccountDO> accountMap = fbAccountMapper.selectList(new LambdaQueryWrapper<FbAccountDO>()
                        .in(FbAccountDO::getFbAccount, fbAccounts))
                .stream()
                .collect(Collectors.toMap(FbAccountDO::getFbAccount, Function.identity(), (a, b) -> a));

        List<FbCollectPendingDetailRespVO> result = new ArrayList<>();
        LocalDateTime now = LocalDateTime.now();
        for (FbCollectDetailDO detail : details) {
            FbCollectDetailDO updateObj = new FbCollectDetailDO();
            updateObj.setId(detail.getId());
            updateObj.setStatus(1);
            updateObj.setStartTime(now);
            fbCollectDetailMapper.updateById(updateObj);

            FbCollectDO task = taskMap.get(detail.getTaskId());
            FbAccountDO account = accountMap.get(detail.getFbAccount());
            FbCollectPendingDetailRespVO item = new FbCollectPendingDetailRespVO();
            item.setTaskId(detail.getTaskId());
            item.setDetailId(detail.getId());
            item.setFbAccount(detail.getFbAccount());
            item.setCookie(account == null ? null : account.getCookie());
            item.setSearchUrl(detail.getSearchUrl());
            item.setExpectedCount(detail.getExpectedCount());
            item.setTaskType(task == null ? 1 : task.getTaskType());
            result.add(item);
        }
        return result;
    }
}
