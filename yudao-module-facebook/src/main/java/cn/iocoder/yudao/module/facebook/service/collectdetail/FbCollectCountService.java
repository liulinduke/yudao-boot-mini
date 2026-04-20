package cn.iocoder.yudao.module.facebook.service.collectdetail;

import lombok.extern.slf4j.Slf4j;
import org.springframework.data.redis.core.StringRedisTemplate;
import org.springframework.stereotype.Service;

import javax.annotation.Resource;
import java.util.concurrent.TimeUnit;

/**
 * FB采集任务 Redis 计数服务
 * 用于原子性更新采集数量,避免并发问题
 *
 * @author jacky
 */
@Slf4j
@Service
public class FbCollectCountService {

    @Resource
    private StringRedisTemplate stringRedisTemplate;

    /**
     * Redis Key 前缀: 任务采集计数
     * 格式: fb:collect:count:{detailId}
     */
    private static final String COLLECT_COUNT_KEY_PREFIX = "fb:collect:count:";

    /**
     * Redis Key 过期时间: 7天
     */
    private static final long KEY_EXPIRE_DAYS = 7;

    /**
     * 原子递增采集数量
     *
     * @param detailId 明细ID
     * @param increment 增量(通常为1)
     * @return 递增后的值
     */
    public Long incrementCollectCount(Long detailId, long increment) {
        String key = buildCountKey(detailId);
        Long count = stringRedisTemplate.opsForValue().increment(key, increment);
        
        // 设置过期时间,防止内存泄漏
        if (count != null && count == increment) {
            // 第一次设置时,设置过期时间
            stringRedisTemplate.expire(key, KEY_EXPIRE_DAYS, TimeUnit.DAYS);
        }
        
        log.debug("明细 {} 采集数量递增: +{} = {}", detailId, increment, count);
        return count;
    }

    /**
     * 获取当前采集数量
     *
     * @param detailId 明细ID
     * @return 采集数量,不存在返回0
     */
    public Long getCollectCount(Long detailId) {
        String key = buildCountKey(detailId);
        String value = stringRedisTemplate.opsForValue().get(key);
        return value != null ? Long.parseLong(value) : 0L;
    }

    /**
     * 同步 Redis 计数到数据库
     *
     * @param detailId 明细ID
     * @return 是否同步成功
     */
    public boolean syncToDatabase(Long detailId) {
        try {
            Long redisCount = getCollectCount(detailId);
            if (redisCount == null || redisCount == 0) {
                return true; // 没有数据需要同步
            }

            // TODO: 调用 Mapper 更新数据库
            // fbCollectDetailMapper.updateCollectedCount(detailId, redisCount.intValue());
            
            log.info("同步明细 {} 的采集数量到数据库: {}", detailId, redisCount);
            return true;
        } catch (Exception e) {
            log.error("同步明细 {} 的采集数量失败", detailId, e);
            return false;
        }
    }

    /**
     * 删除计数缓存
     *
     * @param detailId 明细ID
     */
    public void removeCountCache(Long detailId) {
        String key = buildCountKey(detailId);
        stringRedisTemplate.delete(key);
        log.debug("删除明细 {} 的计数缓存", detailId);
    }

    /**
     * 构建 Redis Key
     *
     * @param detailId 明细ID
     * @return Redis Key
     */
    private String buildCountKey(Long detailId) {
        return COLLECT_COUNT_KEY_PREFIX + detailId;
    }

}
