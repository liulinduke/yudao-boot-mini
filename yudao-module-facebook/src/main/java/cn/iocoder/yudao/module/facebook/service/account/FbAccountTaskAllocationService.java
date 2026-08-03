package cn.iocoder.yudao.module.facebook.service.account;

import cn.hutool.core.collection.CollUtil;
import cn.iocoder.yudao.module.facebook.controller.admin.account.vo.FbAccountSelectorOptionReqVO;
import cn.iocoder.yudao.module.facebook.controller.admin.account.vo.FbAccountSelectorOptionRespVO;
import jakarta.annotation.Resource;
import org.springframework.stereotype.Service;

import java.util.ArrayList;
import java.util.Comparator;
import java.util.List;
import java.util.Set;

/** 统一处理采集、运营和 Agent 的账号候选与公平排序。 */
@Service
public class FbAccountTaskAllocationService {

    @Resource
    private FbAccountService accountService;

    public List<Long> selectAccounts(String mode, List<Long> requestedIds, int targetCount,
                                     String scene, List<String> actionTypes) {
        return selectAccounts(mode, requestedIds, targetCount, scene, actionTypes, Set.of());
    }

    public List<Long> selectAccounts(String mode, List<Long> requestedIds, int targetCount,
                                     String scene, List<String> actionTypes, Set<Long> excludedIds) {
        if (targetCount <= 0) {
            return List.of();
        }
        FbAccountSelectorOptionReqVO req = new FbAccountSelectorOptionReqVO();
        req.setScene(scene);
        req.setActionTypes(actionTypes == null ? new ArrayList<>() : actionTypes);
        req.setTargetCount(targetCount);
        req.setAccountIds(requestedIds == null ? new ArrayList<>() : requestedIds);
        List<FbAccountSelectorOptionRespVO> options = accountService.getSelectorOptions(req);

        boolean manual = "MANUAL".equalsIgnoreCase(mode);
        if (manual) {
            List<Long> selected = requestedIds == null ? List.of() : requestedIds;
            options = options.stream()
                    .filter(option -> selected.contains(option.getId()))
                    .toList();
        }

        return options.stream()
                .filter(FbAccountSelectorOptionRespVO::getEligible)
                .filter(option -> excludedIds == null || !excludedIds.contains(option.getId()))
                .sorted(Comparator
                        .comparingLong((FbAccountSelectorOptionRespVO item) -> value(item.getTotal().get("taskCount")))
                        .thenComparingLong(item -> totalActionCount(item))
                        .thenComparing(item -> item.getLastExecuteTime(), Comparator.nullsFirst(Comparator.naturalOrder()))
                        .thenComparing(FbAccountSelectorOptionRespVO::getId))
                .limit(Math.min(targetCount, options.size()))
                .map(FbAccountSelectorOptionRespVO::getId)
                .toList();
    }

    private long totalActionCount(FbAccountSelectorOptionRespVO item) {
        return item.getTotal().entrySet().stream()
                .filter(entry -> !"taskCount".equals(entry.getKey()))
                .mapToLong(entry -> value(entry.getValue()))
                .sum();
    }

    private long value(Number value) {
        return value == null ? 0L : value.longValue();
    }
}
