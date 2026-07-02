package cn.iocoder.yudao.module.facebook.service.agent;

import cn.hutool.crypto.SecureUtil;
import cn.iocoder.yudao.framework.tenant.core.context.TenantContextHolder;
import jakarta.annotation.Resource;
import org.springframework.data.redis.core.StringRedisTemplate;
import org.springframework.stereotype.Service;

import java.time.LocalDate;
import java.util.ArrayList;
import java.util.Collections;
import java.util.List;
import java.util.Objects;
import java.util.Set;
import java.util.concurrent.TimeUnit;
import java.util.stream.Collectors;

/**
 * Facebook 账号维度串行任务队列。
 *
 * <p>当前先承载采集明细，保留类名兼容旧注入点；Redis key 已升级为通用账号任务队列。</p>
 */
@Service
public class FbAiAgentCollectQueueService {

    private static final String ACCOUNT_SET_KEY_PREFIX = "fb:account-task:accounts:";
    private static final String ACCOUNT_QUEUE_KEY_PREFIX = "fb:account-task:pending:";
    private static final String ACCOUNT_RUNNING_KEY_PREFIX = "fb:account-task:running:";
    private static final String QUEUED_SET_KEY_PREFIX = "fb:account-task:queued:";
    private static final String CREATED_KEY_PREFIX = "fb:ai-agent:collect:created:";
    private static final long QUEUE_EXPIRE_DAYS = 7;
    private static final long RUNNING_EXPIRE_MINUTES = 3;

    @Resource
    private StringRedisTemplate stringRedisTemplate;

    public void push(Long detailId, String fbAccount) {
        push("collect", detailId, fbAccount);
    }

    public void push(String sourceType, Long detailId, String fbAccount) {
        if (detailId == null || fbAccount == null || fbAccount.isBlank()) {
            return;
        }
        String account = fbAccount.trim();
        String key = buildAccountQueueKey(account);
        String setKey = buildQueuedSetKey();
        String value = buildQueueValue(sourceType, detailId);
        Long added = stringRedisTemplate.opsForSet().add(setKey, value);
        stringRedisTemplate.expire(setKey, QUEUE_EXPIRE_DAYS, TimeUnit.DAYS);
        if (!Long.valueOf(1L).equals(added)) {
            return;
        }
        stringRedisTemplate.opsForList().rightPush(key, value);
        stringRedisTemplate.expire(key, QUEUE_EXPIRE_DAYS, TimeUnit.DAYS);
        stringRedisTemplate.opsForSet().add(buildAccountSetKey(), account);
        stringRedisTemplate.expire(buildAccountSetKey(), QUEUE_EXPIRE_DAYS, TimeUnit.DAYS);
    }

    public List<Long> pop(Integer limit, List<String> excludedAccounts) {
        return popItems(limit, excludedAccounts).stream()
                .map(FbAccountTaskQueueItem::getDetailId)
                .collect(Collectors.toList());
    }

    public List<FbAccountTaskQueueItem> popItems(Integer limit, List<String> excludedAccounts) {
        int size = Math.max(1, Math.min(limit == null ? 3 : limit, 10));
        List<FbAccountTaskQueueItem> result = new ArrayList<>(size);
        List<String> accounts = listAccounts(excludedAccounts);
        for (String account : accounts) {
            if (result.size() >= size) {
                break;
            }
            if (Boolean.TRUE.equals(stringRedisTemplate.hasKey(buildAccountRunningKey(account)))) {
                continue;
            }
            String value = stringRedisTemplate.opsForList().leftPop(buildAccountQueueKey(account));
            if (value == null) {
                continue;
            }
            try {
                Long detailId = parseDetailId(value);
                stringRedisTemplate.opsForSet().remove(buildQueuedSetKey(), value);
                markRunning(account, detailId);
                result.add(new FbAccountTaskQueueItem(parseSourceType(value), detailId, account));
            } catch (NumberFormatException ignored) {
                // 丢弃异常缓存值
            }
        }
        return result;
    }

    public void remove(Long detailId, String fbAccount) {
        if (detailId == null) {
            return;
        }
        String legacyValue = String.valueOf(detailId);
        if (fbAccount != null && !fbAccount.isBlank()) {
            stringRedisTemplate.opsForList().remove(buildAccountQueueKey(fbAccount.trim()), 0, legacyValue);
            stringRedisTemplate.opsForList().remove(buildAccountQueueKey(fbAccount.trim()), 0, buildQueueValue("collect", detailId));
            stringRedisTemplate.opsForList().remove(buildAccountQueueKey(fbAccount.trim()), 0, buildQueueValue("dm", detailId));
            stringRedisTemplate.opsForList().remove(buildAccountQueueKey(fbAccount.trim()), 0, buildQueueValue("operation", detailId));
            releaseRunning(fbAccount);
        }
        stringRedisTemplate.opsForSet().remove(buildQueuedSetKey(), legacyValue);
        stringRedisTemplate.opsForSet().remove(buildQueuedSetKey(), buildQueueValue("collect", detailId));
        stringRedisTemplate.opsForSet().remove(buildQueuedSetKey(), buildQueueValue("dm", detailId));
        stringRedisTemplate.opsForSet().remove(buildQueuedSetKey(), buildQueueValue("operation", detailId));
    }

    public void releaseRunning(String fbAccount) {
        if (fbAccount == null || fbAccount.isBlank()) {
            return;
        }
        stringRedisTemplate.delete(buildAccountRunningKey(fbAccount.trim()));
    }

    public boolean tryMarkCreated(Long agentId, String scene, String target) {
        if (agentId == null || target == null) {
            return false;
        }
        String key = CREATED_KEY_PREFIX + TenantContextHolder.getRequiredTenantId()
                + ":" + LocalDate.now()
                + ":" + agentId
                + ":" + scene
                + ":" + SecureUtil.md5(target);
        Boolean success = stringRedisTemplate.opsForValue().setIfAbsent(key, "1", QUEUE_EXPIRE_DAYS, TimeUnit.DAYS);
        return Boolean.TRUE.equals(success);
    }

    private void markRunning(String fbAccount, Long detailId) {
        String key = buildAccountRunningKey(fbAccount);
        stringRedisTemplate.opsForValue().set(key, String.valueOf(detailId), RUNNING_EXPIRE_MINUTES, TimeUnit.MINUTES);
    }

    private String buildQueueValue(String sourceType, Long detailId) {
        return (sourceType == null || sourceType.isBlank() ? "collect" : sourceType.trim()) + ":" + detailId;
    }

    private Long parseDetailId(String value) {
        int index = value == null ? -1 : value.lastIndexOf(':');
        return Long.valueOf(index >= 0 ? value.substring(index + 1) : value);
    }

    private String parseSourceType(String value) {
        int index = value == null ? -1 : value.lastIndexOf(':');
        return index > 0 ? value.substring(0, index) : "collect";
    }

    private List<String> listAccounts(List<String> excludedAccounts) {
        List<String> accounts = new ArrayList<>();
        Set<String> members = stringRedisTemplate.opsForSet().members(buildAccountSetKey());
        if (members == null || members.isEmpty()) {
            return accounts;
        }
        List<String> excluded = excludedAccounts == null ? Collections.emptyList() : excludedAccounts.stream()
                .filter(Objects::nonNull)
                .map(String::trim)
                .filter(item -> !item.isEmpty())
                .collect(Collectors.toList());
        for (String account : members) {
            if (account != null && !excluded.contains(account)) {
                accounts.add(account);
            }
        }
        accounts.sort(String::compareTo);
        return accounts;
    }

    private String buildAccountSetKey() {
        return ACCOUNT_SET_KEY_PREFIX + TenantContextHolder.getRequiredTenantId();
    }

    private String buildAccountQueueKey(String fbAccount) {
        return ACCOUNT_QUEUE_KEY_PREFIX + TenantContextHolder.getRequiredTenantId() + ":" + fbAccount;
    }

    private String buildAccountRunningKey(String fbAccount) {
        return ACCOUNT_RUNNING_KEY_PREFIX + TenantContextHolder.getRequiredTenantId() + ":" + fbAccount;
    }

    private String buildQueuedSetKey() {
        return QUEUED_SET_KEY_PREFIX + TenantContextHolder.getRequiredTenantId();
    }

}
