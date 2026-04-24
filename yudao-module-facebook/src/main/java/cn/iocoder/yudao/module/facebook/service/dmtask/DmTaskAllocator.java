package cn.iocoder.yudao.module.facebook.service.dmtask;

import cn.iocoder.yudao.module.facebook.service.dailylimit.FacebookDailyLimitService;
import cn.iocoder.yudao.module.facebook.enums.OperationTypeEnum;
import lombok.extern.slf4j.Slf4j;
import org.springframework.stereotype.Component;

import javax.annotation.Resource;
import java.util.*;
import java.util.stream.Collectors;

/**
 * 群发私信任务分配器
 *
 * @author 芋道源码
 */
@Slf4j
@Component
public class DmTaskAllocator {

    @Resource
    private FacebookDailyLimitService dailyLimitService;

    /**
     * 分配任务给账号
     *
     * @param accountIds     可用账号ID列表
     * @param targetUserIds  目标用户ID列表
     * @return 分配结果：账号ID -> 目标用户ID列表
     */
    public Map<String, List<String>> allocate(List<String> accountIds, List<String> targetUserIds) {
        if (accountIds == null || accountIds.isEmpty() || targetUserIds == null || targetUserIds.isEmpty()) {
            return Collections.emptyMap();
        }

        // 1. 检查每个账号的剩余次数
        Map<String, Integer> remainingMap = new HashMap<>();
        for (String accountId : accountIds) {
            int remaining = dailyLimitService.getRemainingCount(accountId, OperationTypeEnum.DM);
            remainingMap.put(accountId, remaining);
            log.debug("账号 {} 剩余私信次数: {}", accountId, remaining);
        }

        // 2. 过滤掉次数为0的账号
        List<String> availableAccounts = remainingMap.entrySet().stream()
                .filter(entry -> entry.getValue() > 0)
                .map(Map.Entry::getKey)
                .collect(Collectors.toList());

        if (availableAccounts.isEmpty()) {
            log.warn("没有可用的账号（所有账号都达到每日限制）");
            return Collections.emptyMap();
        }

        // 3. 平均分配任务
        int totalUsers = targetUserIds.size();
        int accountCount = availableAccounts.size();
        int baseCount = totalUsers / accountCount; // 每个账号基础分配数量
        int remainder = totalUsers % accountCount;  // 余数

        // 4. 打乱目标用户顺序（随机化）
        List<String> shuffledUsers = new ArrayList<>(targetUserIds);
        Collections.shuffle(shuffledUsers);

        // 5. 分配
        Map<String, List<String>> allocation = new LinkedHashMap<>();
        int currentIndex = 0;

        for (int i = 0; i < accountCount; i++) {
            String accountId = availableAccounts.get(i);
            // 前remainder个账号多分配1个
            int assignCount = baseCount + (i < remainder ? 1 : 0);

            // 确保不超过账号的剩余次数
            int maxCanAssign = Math.min(assignCount, remainingMap.get(accountId));

            if (maxCanAssign > 0 && currentIndex < shuffledUsers.size()) {
                int endIndex = Math.min(currentIndex + maxCanAssign, shuffledUsers.size());
                List<String> assignedUsers = shuffledUsers.subList(currentIndex, endIndex);
                allocation.put(accountId, new ArrayList<>(assignedUsers));
                currentIndex = endIndex;
                log.info("账号 {} 分配了 {} 个任务", accountId, assignedUsers.size());
            }
        }

        log.info("任务分配完成: 总用户数={}, 可用账号数={}, 实际分配账号数={}",
                totalUsers, accountCount, allocation.size());

        return allocation;
    }

}
