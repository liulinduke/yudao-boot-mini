package cn.iocoder.yudao.module.facebook.service.fbcollectpost;

import cn.hutool.core.collection.CollUtil;
import cn.hutool.core.util.StrUtil;
import lombok.extern.slf4j.Slf4j;
import org.springframework.stereotype.Service;
import jakarta.annotation.Resource;
import org.springframework.validation.annotation.Validated;
import org.springframework.transaction.annotation.Transactional;

import java.util.*;
import java.time.LocalDateTime;
import cn.iocoder.yudao.module.facebook.controller.admin.fbcollectpost.vo.*;
import cn.iocoder.yudao.module.facebook.dal.dataobject.fbcollectpost.FbCollectPostDO;
import cn.iocoder.yudao.framework.common.pojo.PageResult;
import cn.iocoder.yudao.framework.common.pojo.PageParam;
import cn.iocoder.yudao.framework.common.util.object.BeanUtils;

import cn.iocoder.yudao.module.facebook.dal.mysql.fbcollectpost.FbCollectPostMapper;
import cn.iocoder.yudao.module.facebook.dal.mysql.collectdetail.FbCollectDetailMapper;
import cn.iocoder.yudao.module.facebook.dal.dataobject.collectdetail.FbCollectDetailDO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.collect.FbCollectDO;
import cn.iocoder.yudao.module.facebook.service.collectdetail.FbCollectCountService;
import cn.iocoder.yudao.module.facebook.service.agent.FbAiAgentCollectQueueService;
import cn.iocoder.yudao.module.facebook.service.agent.FbAiAgentService;
import cn.iocoder.yudao.framework.common.util.spring.SpringUtils;
import com.baomidou.mybatisplus.core.conditions.query.LambdaQueryWrapper;

import static cn.iocoder.yudao.framework.common.exception.util.ServiceExceptionUtil.exception;
import static cn.iocoder.yudao.framework.common.util.collection.CollectionUtils.convertList;
import static cn.iocoder.yudao.framework.common.util.collection.CollectionUtils.diffList;
import static cn.iocoder.yudao.module.facebook.enums.ErrorCodeConstants.*;

/**
 * FB帖子采集结果 Service 实现类
 *
 * @author jacky
 */
@Slf4j
@Service
@Validated
public class FbCollectPostServiceImpl implements FbCollectPostService {

    @Resource
    private FbCollectPostMapper fbCollectPostMapper;

    @Resource
    private FbCollectDetailMapper fbCollectDetailMapper;

    @Resource
    private FbCollectCountService countService;
    @Resource
    private FbAiAgentCollectQueueService accountTaskQueueService;

    @Override
    public Long createFbCollectPost(FbCollectPostSaveReqVO createReqVO) {
        // 插入
        FbCollectPostDO fbCollectPost = BeanUtils.toBean(createReqVO, FbCollectPostDO.class);
        fbCollectPostMapper.insert(fbCollectPost);

        // 返回
        return fbCollectPost.getId();
    }

    @Override
    public void updateFbCollectPost(FbCollectPostSaveReqVO updateReqVO) {
        // 校验存在
        validateFbCollectPostExists(updateReqVO.getId());
        // 更新
        FbCollectPostDO updateObj = BeanUtils.toBean(updateReqVO, FbCollectPostDO.class);
        fbCollectPostMapper.updateById(updateObj);
    }

    @Override
    public void deleteFbCollectPost(Long id) {
        // 校验存在
        validateFbCollectPostExists(id);
        // 删除
        fbCollectPostMapper.deleteById(id);
    }

    @Override
        public void deleteFbCollectPostListByIds(List<Long> ids) {
        // 删除
        fbCollectPostMapper.deleteByIds(ids);
        }


    private void validateFbCollectPostExists(Long id) {
        if (fbCollectPostMapper.selectById(id) == null) {
            throw exception(FB_COLLECT_POST_NOT_EXISTS);
        }
    }

    @Override
    public FbCollectPostDO getFbCollectPost(Long id) {
        return fbCollectPostMapper.selectById(id);
    }

    @Override
    public PageResult<FbCollectPostDO> getFbCollectPostPage(FbCollectPostPageReqVO pageReqVO) {
        return fbCollectPostMapper.selectPage(pageReqVO);
    }

    @Override
    @Transactional(rollbackFor = Exception.class)
    public Integer batchSaveFbCollectPost(Long detailId, List<FbCollectPostSaveReqVO> results) {
        // 1. 查询明细信息
        FbCollectDetailDO detail = fbCollectDetailMapper.selectById(detailId);
        if (detail == null) {
            log.warn("明细 {} 不存在", detailId);
            return 0;
        }
        FbCollectDO task = SpringUtils.getBean(cn.iocoder.yudao.module.facebook.dal.mysql.collect.FbCollectMapper.class)
                .selectById(detail.getTaskId());
        boolean aiGroupPostCollect = task != null && task.getRemark() != null
                && (task.getRemark().startsWith("AI群帖获客:") || task.getRemark().startsWith("AI帖子获客:"));
        
        int count = 0;
        int duplicateCount = 0;
        if (CollUtil.isNotEmpty(results)) {
            for (FbCollectPostSaveReqVO result : results) {
                // 设置 taskId 和 fbAccount
                result.setTaskId(detail.getTaskId());
                result.setFbAccount(detail.getFbAccount());
                // 解析失败或旧数据可能会落成 Unix epoch，不能作为真实发帖时间保存。
                if (result.getPostCreateTime() != null
                        && result.getPostCreateTime().isBefore(LocalDateTime.of(1971, 1, 1, 0, 0))) {
                    result.setPostCreateTime(null);
                }
                if (aiGroupPostCollect && existsAiGroupPost(result)) {
                    duplicateCount++;
                    log.info("AI帖子去重跳过: itemId={}, url={}", result.getItemId(), result.getUrl());
                    continue;
                }
                
                FbCollectPostDO fbCollectPost = BeanUtils.toBean(result, FbCollectPostDO.class);
                
                // 清空id字段,让数据库自动生成主键
                fbCollectPost.setId(null);
                fbCollectPostMapper.insert(fbCollectPost);
                count++;
            }
        }
        log.info("批量保存帖子结果: detailId={}, 接收={}, 新增={}, 重复跳过={}",
                detailId, results == null ? 0 : results.size(), count, duplicateCount);
        FbCollectDetailDO summaryUpdate = new FbCollectDetailDO();
        summaryUpdate.setId(detailId);
        summaryUpdate.setErrorMessage(String.format("本轮采集：接收 %d 条，新增保存 %d 条，重复跳过 %d 条",
                results == null ? 0 : results.size(), count, duplicateCount));
        fbCollectDetailMapper.updateById(summaryUpdate);
        
        // 2. 使用 Redis 原子递增采集数量(即使为0也要记录)
        countService.incrementCollectCount(detailId, count);
        
        // 3. 同时递增主表总采集数量(并发安全)
        countService.incrementTaskTotalCount(detail.getTaskId(), count);
        
        // 4. 异步更新数据库和主表(避免阻塞) - 即使count=0也要更新状态
        updateDetailAndMainTableAsync(detailId);
        
        return count;
    }

    private boolean existsAiGroupPost(FbCollectPostSaveReqVO result) {
        LambdaQueryWrapper<FbCollectPostDO> wrapper = new LambdaQueryWrapper<>();
        wrapper.and(query -> {
            boolean hasAny = false;
            if (StrUtil.isNotBlank(result.getItemId())) {
                query.eq(FbCollectPostDO::getItemId, result.getItemId());
                hasAny = true;
            }
            if (StrUtil.isNotBlank(result.getUrl())) {
                if (hasAny) {
                    query.or();
                }
                query.eq(FbCollectPostDO::getUrl, result.getUrl());
                hasAny = true;
            }
            if (!hasAny) {
                query.eq(FbCollectPostDO::getId, -1L);
            }
        });
        Long count = fbCollectPostMapper.selectCount(wrapper);
        return count != null && count > 0;
    }
    
    /**
     * 异步更新明细表和主表
     */
    private void updateDetailAndMainTableAsync(Long detailId) {
        // TODO: 使用 @Async 注解实现真正的异步
        // 这里暂时同步执行,后续可以优化
        try {
            updateDetailAndMainTable(detailId);
        } catch (Exception e) {
            log.error("更新明细和主表失败, detailId={}", detailId, e);
        }
    }
    
    /**
     * 更新明细表和主表
     */
    private void updateDetailAndMainTable(Long detailId) {
        // 从 Redis 获取最新计数
        Long redisCount = countService.getCollectCount(detailId);
        
        // 更新明细表
        FbCollectDetailDO detail = fbCollectDetailMapper.selectById(detailId);
        if (detail == null) {
            return;
        }
        
        detail.setCollectedCount(redisCount.intValue());
        
        // 采集脚本一旦返回结果（即使数量不足或为 0），本轮明细也视为结束
        detail.setStatus(2); // 已完成
        detail.setEndTime(java.time.LocalDateTime.now()); // 设置结束时间
        
        fbCollectDetailMapper.updateById(detail);
        accountTaskQueueService.releaseRunning(detail.getFbAccount());
        
        // 聚合更新主表
        boolean taskFinished = updateMainTaskProgress(detail.getTaskId());
        
        // 清理 Redis 缓存
        countService.removeCountCache(detailId);
        
        // 如果明细已完成,也清理主表缓存(可选,防止内存泄漏)
        if (detail.getStatus() == 2) {
            countService.removeTaskTotalCountCache(detail.getTaskId());
        }
        if (taskFinished) {
            SpringUtils.getBean(FbAiAgentService.class).continueAfterCollectTaskFinished(detail.getTaskId());
        }
        
        log.info("更新明细 {} 完成, 已采集: {}/{}", detailId, redisCount, detail.getExpectedCount());
    }
    
    /**
     * 聚合更新主表进度(使用 Redis 原子计数)
     */
    private boolean updateMainTaskProgress(Long taskId) {
        // 从 Redis 获取总采集数量(原子操作,并发安全)
        Long totalCollected = countService.getTaskTotalCount(taskId);
        
        // 查询所有明细的期望总数和失败数
        Map<String, Object> stats = fbCollectDetailMapper.selectTaskStats(taskId);
        if (stats == null || stats.isEmpty()) {
            return false;
        }
        
        Integer totalExpected = ((Number) stats.get("total_expected")).intValue();
        List<FbCollectDetailDO> details = fbCollectDetailMapper.selectListByTaskId(taskId);
        long unfinishedCount = details.stream()
                .filter(d -> d.getStatus() != null && (d.getStatus() == 0 || d.getStatus() == 1))
                .count();
        Long failedCount = stats.get("failed_count") != null ? ((Number) stats.get("failed_count")).longValue() : 0L;
        
        // 更新主表
        cn.iocoder.yudao.module.facebook.dal.dataobject.collect.FbCollectDO task = new cn.iocoder.yudao.module.facebook.dal.dataobject.collect.FbCollectDO();
        task.setId(taskId);
        task.setTotalExpectedCount(totalExpected); // 设置总期望数
        task.setTotalCollectedCount(totalCollected.intValue()); // 设置总采集数
        
        if (unfinishedCount == 0) {
            task.setStatus(2); // 已完成
            task.setEndTime(java.time.LocalDateTime.now()); // 设置结束时间
        } else if (failedCount > 0) {
            task.setStatus(3); // 部分失败
        } else {
            task.setStatus(1); // 采集中
        }
        
        cn.iocoder.yudao.module.facebook.dal.mysql.collect.FbCollectMapper fbCollectMapper = 
            SpringUtils.getBean(cn.iocoder.yudao.module.facebook.dal.mysql.collect.FbCollectMapper.class);
        fbCollectMapper.updateById(task);
        
        log.info("更新主表 {} 完成, 总进度: {}/{}", taskId, totalCollected, totalExpected);
        return unfinishedCount == 0;
    }

}
