package cn.iocoder.yudao.module.facebook.service.fbcollectgroup;

import cn.hutool.core.collection.CollUtil;
import lombok.extern.slf4j.Slf4j;
import org.springframework.stereotype.Service;
import jakarta.annotation.Resource;
import org.springframework.validation.annotation.Validated;
import org.springframework.transaction.annotation.Transactional;

import java.time.LocalDateTime;
import java.util.*;
import cn.iocoder.yudao.module.facebook.controller.admin.fbcollectgroup.vo.*;
import cn.iocoder.yudao.module.facebook.dal.dataobject.fbcollectgroup.FbCollectGroupDO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.collect.FbCollectDO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.collectdetail.FbCollectDetailDO;
import cn.iocoder.yudao.framework.common.pojo.PageResult;
import cn.iocoder.yudao.framework.common.pojo.PageParam;
import cn.iocoder.yudao.framework.common.util.object.BeanUtils;

import cn.iocoder.yudao.module.facebook.dal.mysql.fbcollectgroup.FbCollectGroupMapper;
import cn.iocoder.yudao.module.facebook.dal.mysql.collect.FbCollectMapper;
import cn.iocoder.yudao.module.facebook.dal.mysql.collectdetail.FbCollectDetailMapper;
import cn.iocoder.yudao.module.facebook.service.collectdetail.FbCollectCountService;

import static cn.iocoder.yudao.framework.common.exception.util.ServiceExceptionUtil.exception;
import static cn.iocoder.yudao.framework.common.util.collection.CollectionUtils.convertList;
import static cn.iocoder.yudao.framework.common.util.collection.CollectionUtils.diffList;
import static cn.iocoder.yudao.module.facebook.enums.ErrorCodeConstants.*;

/**
 * FB群组采集结果 Service 实现类
 *
 * @author jacky
 */
@Slf4j
@Service
@Validated
public class FbCollectGroupServiceImpl implements FbCollectGroupService {

    @Resource
    private FbCollectGroupMapper fbCollectGroupMapper;
    
    @Resource
    private FbCollectDetailMapper fbCollectDetailMapper;
    
    @Resource
    private FbCollectMapper fbCollectMapper;
    
    @Resource
    private FbCollectCountService countService;

    @Override
    public Long createFbCollectGroup(FbCollectGroupSaveReqVO createReqVO) {
        // 插入
        FbCollectGroupDO fbCollectGroup = BeanUtils.toBean(createReqVO, FbCollectGroupDO.class);
        fbCollectGroupMapper.insert(fbCollectGroup);

        // 返回
        return fbCollectGroup.getId();
    }

    @Override
    public void updateFbCollectGroup(FbCollectGroupSaveReqVO updateReqVO) {
        // 校验存在
        validateFbCollectGroupExists(updateReqVO.getId());
        // 更新
        FbCollectGroupDO updateObj = BeanUtils.toBean(updateReqVO, FbCollectGroupDO.class);
        fbCollectGroupMapper.updateById(updateObj);
    }

    @Override
    public void deleteFbCollectGroup(Long id) {
        // 校验存在
        validateFbCollectGroupExists(id);
        // 删除
        fbCollectGroupMapper.deleteById(id);
    }

    @Override
        public void deleteFbCollectGroupListByIds(List<Long> ids) {
        // 删除
        fbCollectGroupMapper.deleteByIds(ids);
        }


    private void validateFbCollectGroupExists(Long id) {
        if (fbCollectGroupMapper.selectById(id) == null) {
            throw exception(FB_COLLECT_GROUP_NOT_EXISTS);
        }
    }

    @Override
    public FbCollectGroupDO getFbCollectGroup(Long id) {
        return fbCollectGroupMapper.selectById(id);
    }

    @Override
    public PageResult<FbCollectGroupDO> getFbCollectGroupPage(FbCollectGroupPageReqVO pageReqVO) {
        return fbCollectGroupMapper.selectPage(pageReqVO);
    }

    @Override
    @Transactional(rollbackFor = Exception.class)
    public Integer batchSaveFbCollectGroup(Long detailId, List<FbCollectGroupSaveReqVO> results) {
        // 1. 查询明细信息
        FbCollectDetailDO detail = fbCollectDetailMapper.selectById(detailId);
        if (detail == null) {
            log.warn("明细 {} 不存在", detailId);
            return 0;
        }
        
        int count = 0;
        if (CollUtil.isNotEmpty(results)) {
            for (FbCollectGroupSaveReqVO result : results) {
                // 设置 taskId
                result.setTaskId(detail.getTaskId());
                
                FbCollectGroupDO fbCollectGroup = BeanUtils.toBean(result, FbCollectGroupDO.class);
                
                // 清空id字段,让数据库自动生成主键
                fbCollectGroup.setId(null);
                fbCollectGroupMapper.insert(fbCollectGroup);
                count++;
            }
        }
        
        // 2. 使用 Redis 原子递增采集数量(即使为0也要记录)
        countService.incrementCollectCount(detailId, count);
        
        // 3. 同时递增主表总采集数量(并发安全)
        countService.incrementTaskTotalCount(detail.getTaskId(), count);
        
        // 4. 异步更新数据库和主表(避免阻塞) - 即使count=0也要更新状态
        updateDetailAndMainTableAsync(detailId);
        
        return count;
    }
    
    /**
     * 异步更新明细表和主表
     */
    private void updateDetailAndMainTableAsync(Long detailId) {
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
        detail.setEndTime(LocalDateTime.now());
        
        fbCollectDetailMapper.updateById(detail);
        
        // 聚合更新主表
        updateMainTaskProgress(detail.getTaskId());
        
        // 清理 Redis 缓存
        countService.removeCountCache(detailId);
        
        log.info("更新明细 {} 完成, 已采集: {}/{}", detailId, redisCount, detail.getExpectedCount());
    }
    
    /**
     * 聚合更新主表进度(使用 Redis 原子计数)
     */
    private void updateMainTaskProgress(Long taskId) {
        // 从 Redis 获取总采集数量(原子操作,并发安全)
        Long totalCollected = countService.getTaskTotalCount(taskId);
        
        // 查询所有明细的期望总数和失败数
        Map<String, Object> stats = fbCollectDetailMapper.selectTaskStats(taskId);
        if (stats == null || stats.isEmpty()) {
            return;
        }
        
        Integer totalExpected = ((Number) stats.get("total_expected")).intValue();
        List<FbCollectDetailDO> details = fbCollectDetailMapper.selectListByTaskId(taskId);
        long unfinishedCount = details.stream()
                .filter(d -> d.getStatus() != null && (d.getStatus() == 0 || d.getStatus() == 1))
                .count();
        Long failedCount = stats.get("failed_count") != null ? ((Number) stats.get("failed_count")).longValue() : 0L;
        
        // 更新主表
        FbCollectDO task = new FbCollectDO();
        task.setId(taskId);
        task.setTotalExpectedCount(totalExpected);
        task.setTotalCollectedCount(totalCollected.intValue());
        
        if (unfinishedCount == 0) {
            task.setStatus(2); // 已完成
            task.setEndTime(LocalDateTime.now());
        } else if (failedCount > 0) {
            task.setStatus(3); // 部分失败
        } else {
            task.setStatus(1); // 采集中
        }
        
        fbCollectMapper.updateById(task);
        
        log.info("更新主表 {} 完成, 总进度: {}/{}", taskId, totalCollected, totalExpected);
    }

}
