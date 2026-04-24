package cn.iocoder.yudao.module.facebook.service.dailylimit;

import cn.iocoder.yudao.module.facebook.enums.OperationTypeEnum;
import javax.annotation.Resource;
import lombok.extern.slf4j.Slf4j;
import org.springframework.data.redis.core.StringRedisTemplate;
import org.springframework.stereotype.Service;

import java.time.LocalDate;
import java.time.format.DateTimeFormatter;
import java.util.HashMap;
import java.util.Map;
import java.util.concurrent.TimeUnit;

/**
 * Facebook 每日操作次数限制服务
 *
 * @author 芋道源码
 */
@Slf4j
@Service
public class FacebookDailyLimitService {

    private static final String REDIS_KEY_PREFIX = "facebook:daily:limit:";
    private static final DateTimeFormatter DATE_FORMATTER = DateTimeFormatter.ofPattern("yyyyMMdd");

    @Resource
    private StringRedisTemplate stringRedisTemplate;

    /**
     * 获取今日剩余次数
     *
     * @param accountId 账号ID
     * @param type      操作类型
     * @return 剩余次数
     */
    public int getRemainingCount(String accountId, OperationTypeEnum type) {
        String key = buildRedisKey(accountId, type);
        String value = stringRedisTemplate.opsForValue().get(key);
        int usedCount = value != null ? Integer.parseInt(value) : 0;
        int limit = getConfiguredLimit(type);
        return Math.max(0, limit - usedCount);
    }

    /**
     * 使用一次次数
     *
     * @param accountId 账号ID
     * @param type      操作类型
     * @return 是否成功（如果已达上限返回false）
     */
    public boolean useOnce(String accountId, OperationTypeEnum type) {
        String key = buildRedisKey(accountId, type);
        int limit = getConfiguredLimit(type);

        // 原子递增
        Long count = stringRedisTemplate.opsForValue().increment(key);
        if (count == null) {
            return false;
        }

        // 首次设置过期时间（24小时）
        if (count == 1) {
            stringRedisTemplate.expire(key, 24, TimeUnit.HOURS);
        }

        // 检查是否超过限制
        if (count > limit) {
            log.warn("账号 {} 的 {} 操作已达每日限制: {}/{}", accountId, type.getName(), count, limit);
            return false;
        }

        log.debug("账号 {} 使用一次 {} 操作，已用: {}/{}", accountId, type.getName(), count, limit);
        return true;
    }

    /**
     * 获取配置的限制次数
     *
     * @param type 操作类型
     * @return 限制次数
     */
    public int getConfiguredLimit(OperationTypeEnum type) {
        // TODO: 从数据库或配置中心读取，暂时使用默认值
        return type.getDefaultLimit();
    }

    /**
     * 构建Redis Key
     *
     * @param accountId 账号ID
     * @param type      操作类型
     * @return Redis Key
     */
    private String buildRedisKey(String accountId, OperationTypeEnum type) {
        String date = LocalDate.now().format(DATE_FORMATTER);
        return REDIS_KEY_PREFIX + accountId + ":" + date + ":" + type.getCode();
    }

    /**
     * 获取所有操作类型的剩余次数
     *
     * @param accountId 账号ID
     * @return 各操作类型的剩余次数
     */
    public Map<String, Integer> getAllRemainingCounts(String accountId) {
        Map<String, Integer> result = new HashMap<>();
        for (OperationTypeEnum type : OperationTypeEnum.values()) {
            result.put(type.getCode(), getRemainingCount(accountId, type));
        }
        return result;
    }

}
