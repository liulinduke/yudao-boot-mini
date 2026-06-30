package cn.iocoder.yudao.module.facebook.service.agent;

import cn.hutool.crypto.SecureUtil;
import cn.iocoder.yudao.framework.tenant.core.context.TenantContextHolder;
import jakarta.annotation.Resource;
import org.springframework.data.redis.core.StringRedisTemplate;
import org.springframework.stereotype.Service;

import java.time.LocalDate;
import java.util.ArrayList;
import java.util.Collection;
import java.util.List;
import java.util.Objects;
import java.util.concurrent.TimeUnit;

/**
 * AI 获客 Agent 待启动采集明细队列。
 */
@Service
public class FbAiAgentCollectQueueService {

    private static final String QUEUE_KEY_PREFIX = "fb:ai-agent:collect:pending:";
    private static final String CREATED_KEY_PREFIX = "fb:ai-agent:collect:created:";
    private static final long QUEUE_EXPIRE_DAYS = 7;

    @Resource
    private StringRedisTemplate stringRedisTemplate;

    public void push(Long detailId) {
        if (detailId == null) {
            return;
        }
        String key = buildQueueKey();
        stringRedisTemplate.opsForList().rightPush(key, String.valueOf(detailId));
        stringRedisTemplate.expire(key, QUEUE_EXPIRE_DAYS, TimeUnit.DAYS);
    }

    public void pushAll(Collection<Long> detailIds) {
        if (detailIds == null || detailIds.isEmpty()) {
            return;
        }
        String key = buildQueueKey();
        List<String> values = detailIds.stream()
                .filter(Objects::nonNull)
                .map(String::valueOf)
                .toList();
        if (values.isEmpty()) {
            return;
        }
        stringRedisTemplate.opsForList().rightPushAll(key, values);
        stringRedisTemplate.expire(key, QUEUE_EXPIRE_DAYS, TimeUnit.DAYS);
    }

    public List<Long> pop(Integer limit) {
        int size = Math.max(1, Math.min(limit == null ? 3 : limit, 10));
        String key = buildQueueKey();
        List<Long> result = new ArrayList<>(size);
        for (int i = 0; i < size; i++) {
            String value = stringRedisTemplate.opsForList().leftPop(key);
            if (value == null) {
                break;
            }
            try {
                result.add(Long.valueOf(value));
            } catch (NumberFormatException ignored) {
                // 丢弃异常缓存值
            }
        }
        return result;
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

    private String buildQueueKey() {
        return QUEUE_KEY_PREFIX + TenantContextHolder.getRequiredTenantId();
    }

}
