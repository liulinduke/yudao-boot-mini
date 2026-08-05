package cn.iocoder.yudao.module.facebook.service.account;

import cn.hutool.core.collection.CollUtil;
import cn.iocoder.yudao.framework.tenant.core.context.TenantContextHolder;
import cn.iocoder.yudao.module.facebook.controller.admin.account.vo.FbAccountSelectorOptionReqVO;
import cn.iocoder.yudao.module.facebook.controller.admin.account.vo.FbAccountSelectorOptionRespVO;
import jakarta.annotation.Resource;
import org.springframework.stereotype.Service;

import java.util.ArrayList;
import java.util.Comparator;
import java.util.HashMap;
import java.util.List;
import java.util.Map;
import java.util.Set;

/** 统一处理采集、运营和 Agent 的账号候选与公平排序。 */
@Service
public class FbAccountTaskAllocationService {

    @Resource
    private FbAccountService accountService;

    @Resource
    private FbAccountActionStatService actionStatService;

    /** 当前进程内的统一账号轮询游标，按租户隔离。应用重启后由长期统计恢复初始顺序。 */
    private final Map<Long, Long> lastAllocatedAccountByTenant = new HashMap<>();

    public List<Long> selectAccounts(String mode, List<Long> requestedIds, int targetCount,
                                     String scene, List<String> actionTypes) {
        return selectAccounts(mode, requestedIds, targetCount, scene, actionTypes, Set.of(), true);
    }

    public List<Long> selectAccounts(String mode, List<Long> requestedIds, int targetCount,
                                     String scene, List<String> actionTypes, Set<Long> excludedIds) {
        return selectAccounts(mode, requestedIds, targetCount, scene, actionTypes, excludedIds, true);
    }

    /**
     * 选择并预占账号。预占会更新最近执行时间，使下一次任务从队列中的下一个账号开始。
     * 账号候选弹框等只读场景应传 reserve=false，避免打开弹框改变轮询位置。
     */
    public synchronized List<Long> selectAccounts(String mode, List<Long> requestedIds, int targetCount,
                                                  String scene, List<String> actionTypes, Set<Long> excludedIds,
                                                  boolean reserve) {
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

        List<FbAccountSelectorOptionRespVO> candidates = options.stream()
                .filter(FbAccountSelectorOptionRespVO::getEligible)
                .filter(option -> excludedIds == null || !excludedIds.contains(option.getId()))
                .toList();

        if (candidates.isEmpty()) {
            return List.of();
        }

        Long tenantId = TenantContextHolder.getRequiredTenantId();
        Long lastAccountId = lastAllocatedAccountByTenant.get(tenantId);
        candidates = new ArrayList<>(candidates);
        if (lastAccountId == null) {
            // 首次进入轮询时沿用历史最近执行时间恢复队列位置。
            candidates.sort(Comparator
                    .comparing((FbAccountSelectorOptionRespVO item) -> item.getLastExecuteTime(),
                            Comparator.nullsFirst(Comparator.naturalOrder()))
                    .thenComparing(FbAccountSelectorOptionRespVO::getId));
        } else {
            // 账号ID只用于稳定定义队列顺序，实际分配从上次账号之后开始并循环。
            candidates.sort(Comparator.comparing(FbAccountSelectorOptionRespVO::getId));
            int startIndex = 0;
            for (int i = 0; i < candidates.size(); i++) {
                if (candidates.get(i).getId() > lastAccountId) {
                    startIndex = i;
                    break;
                }
                startIndex = (i + 1) % candidates.size();
            }
            List<FbAccountSelectorOptionRespVO> rotated = new ArrayList<>(candidates.size());
            rotated.addAll(candidates.subList(startIndex, candidates.size()));
            rotated.addAll(candidates.subList(0, startIndex));
            candidates = rotated;
        }

        List<Long> selectedIds = candidates.stream()
                .limit(Math.min(targetCount, candidates.size()))
                .map(FbAccountSelectorOptionRespVO::getId)
                .toList();

        if (reserve && !selectedIds.isEmpty()) {
            lastAllocatedAccountByTenant.put(tenantId, selectedIds.get(selectedIds.size() - 1));
            String actionType = firstActionType(actionTypes, scene);
            for (Long accountId : selectedIds) {
                actionStatService.markStarted(accountId, actionType);
            }
        }
        return selectedIds;
    }

    private String firstActionType(List<String> actionTypes, String scene) {
        if (actionTypes != null) {
            for (String actionType : actionTypes) {
                if (actionType != null && !actionType.isBlank()) {
                    return actionType.trim();
                }
            }
        }
        return scene == null || scene.isBlank() ? "operation" : scene.trim();
    }
}
