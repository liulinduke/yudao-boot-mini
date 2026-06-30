package cn.iocoder.yudao.module.facebook.service.collectdetail;

import cn.hutool.core.collection.CollUtil;
import cn.iocoder.yudao.module.facebook.controller.admin.collectdetail.vo.FbCollectPendingDetailRespVO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.account.FbAccountDO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.collect.FbCollectDO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.collectdetail.FbCollectDetailDO;
import cn.iocoder.yudao.module.facebook.dal.mysql.account.FbAccountMapper;
import cn.iocoder.yudao.module.facebook.dal.mysql.collect.FbCollectMapper;
import cn.iocoder.yudao.module.facebook.dal.mysql.collectdetail.FbCollectDetailMapper;
import cn.iocoder.yudao.module.facebook.service.agent.FbAiAgentCollectQueueService;
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
    @Resource
    private FbAiAgentCollectQueueService aiAgentCollectQueueService;

    @Override
    public List<FbCollectDetailDO> getPendingDetailsByAccount(String fbAccount, Long taskId) {
        LambdaQueryWrapper<FbCollectDetailDO> wrapper = new LambdaQueryWrapper<FbCollectDetailDO>()
                .eq(FbCollectDetailDO::getFbAccount, fbAccount)
                .eq(FbCollectDetailDO::getStatus, 0);
        if (taskId != null) {
            wrapper.eq(FbCollectDetailDO::getTaskId, taskId);
        }
        wrapper.orderByAsc(FbCollectDetailDO::getId).last("LIMIT 1");
        return fbCollectDetailMapper.selectList(wrapper);
    }

    @Override
    public FbCollectDetailDO getDetail(Long id) {
        return fbCollectDetailMapper.selectById(id);
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
        List<Long> detailIds = aiAgentCollectQueueService.pop(limit);
        if (CollUtil.isEmpty(detailIds)) {
            return List.of();
        }
        List<FbCollectDetailDO> details = fbCollectDetailMapper.selectBatchIds(detailIds).stream()
                .filter(detail -> detail != null && Objects.equals(detail.getStatus(), 0))
                .collect(Collectors.toList());
        if (CollUtil.isEmpty(details)) {
            return List.of();
        }

        List<Long> taskIds = details.stream()
                .map(FbCollectDetailDO::getTaskId)
                .filter(Objects::nonNull)
                .distinct()
                .collect(Collectors.toList());
        Map<Long, FbCollectDO> taskMap = fbCollectMapper.selectBatchIds(taskIds).stream()
                .collect(Collectors.toMap(FbCollectDO::getId, Function.identity(), (a, b) -> a));

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
            item.setSourceUserId(detail.getSourceUserId());
            item.setExpectedCount(detail.getExpectedCount());
            item.setTaskType(task == null ? 1 : task.getTaskType());
            result.add(item);
        }
        return result;
    }
}
