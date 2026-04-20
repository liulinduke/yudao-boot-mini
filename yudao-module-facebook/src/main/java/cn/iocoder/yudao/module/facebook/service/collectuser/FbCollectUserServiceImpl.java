package cn.iocoder.yudao.module.facebook.service.collectuser;

import cn.hutool.core.collection.CollUtil;
import lombok.extern.slf4j.Slf4j;
import org.springframework.stereotype.Service;
import javax.annotation.Resource;
import org.springframework.validation.annotation.Validated;
import org.springframework.transaction.annotation.Transactional;

import java.time.LocalDateTime;
import java.util.*;
import cn.iocoder.yudao.module.facebook.controller.admin.collectuser.vo.*;
import cn.iocoder.yudao.module.facebook.dal.dataobject.collectuser.FbCollectUserDO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.collect.FbCollectDO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.collectdetail.FbCollectDetailDO;
import cn.iocoder.yudao.framework.common.pojo.PageResult;
import cn.iocoder.yudao.framework.common.pojo.PageParam;
import cn.iocoder.yudao.framework.common.util.object.BeanUtils;

import cn.iocoder.yudao.module.facebook.dal.mysql.collectuser.FbCollectUserMapper;
import cn.iocoder.yudao.module.facebook.dal.mysql.collect.FbCollectMapper;
import cn.iocoder.yudao.module.facebook.dal.mysql.collectdetail.FbCollectDetailMapper;
import cn.iocoder.yudao.module.facebook.service.collectdetail.FbCollectCountService;

import static cn.iocoder.yudao.framework.common.exception.util.ServiceExceptionUtil.exception;
import static cn.iocoder.yudao.framework.common.util.collection.CollectionUtils.convertList;
import static cn.iocoder.yudao.framework.common.util.collection.CollectionUtils.diffList;
import static cn.iocoder.yudao.module.facebook.enums.ErrorCodeConstants.*;

/**
 * FB用户采集结果 Service 实现类
 *
 * @author jacky
 */
@Slf4j
@Service
@Validated
public class FbCollectUserServiceImpl implements FbCollectUserService {

    @Resource
    private FbCollectUserMapper fbCollectUserMapper;
    
    @Resource
    private FbCollectDetailMapper fbCollectDetailMapper;
    
    @Resource
    private FbCollectMapper fbCollectMapper;
    
    @Resource
    private FbCollectCountService countService;

    @Override
    public Long createFbCollectUser(FbCollectUserSaveReqVO createReqVO) {
        // 插入
        FbCollectUserDO fbCollectUser = BeanUtils.toBean(createReqVO, FbCollectUserDO.class);
        fbCollectUserMapper.insert(fbCollectUser);

        // 返回
        return fbCollectUser.getId();
    }

    @Override
    public void updateFbCollectUser(FbCollectUserSaveReqVO updateReqVO) {
        // 校验存在
        //validateFbCollectUserExists(updateReqVO.getId());
        // 更新
        FbCollectUserDO updateObj = BeanUtils.toBean(updateReqVO, FbCollectUserDO.class);
        fbCollectUserMapper.updateById(updateObj);
    }

    @Override
    public void deleteFbCollectUser(Long id) {
        // 校验存在
        validateFbCollectUserExists(id);
        // 删除
        fbCollectUserMapper.deleteById(id);
    }

    @Override
        public void deleteFbCollectUserListByIds(List<Long> ids) {
        // 删除
        fbCollectUserMapper.deleteByIds(ids);
        }


    private void validateFbCollectUserExists(Long id) {
        if (fbCollectUserMapper.selectById(id) == null) {
            throw exception(FB_COLLECT_USER_NOT_EXISTS);
        }
    }

    @Override
    public FbCollectUserDO getFbCollectUser(Long id) {
        return fbCollectUserMapper.selectById(id);
    }

    @Override
    public PageResult<FbCollectUserDO> getFbCollectUserPage(FbCollectUserPageReqVO pageReqVO) {
        return fbCollectUserMapper.selectPage(pageReqVO);
    }

    @Override
    @Transactional(rollbackFor = Exception.class)
    public Integer batchSaveFbCollectUser(Long detailId, List<FbCollectUserSaveReqVO> results) {
        if (CollUtil.isEmpty(results)) {
            return 0;
        }
        
        // 1. 查询明细信息
        FbCollectDetailDO detail = fbCollectDetailMapper.selectById(detailId);
        if (detail == null) {
            log.warn("明细 {} 不存在", detailId);
            return 0;
        }
        
        int count = 0;
        for (FbCollectUserSaveReqVO result : results) {
            // 设置 taskId 和 fbAccount
            result.setTaskId(detail.getTaskId());
            result.setFbAccount(detail.getFbAccount());
            
            // 先保存 VO 的 id(Facebook用户ID)
            String fbUserId = result.getId();
            // 清空 VO 的 id,避免 BeanUtil 尝试转换到 DO.id(Long类型)
            result.setId(null);
            
            FbCollectUserDO fbCollectUser = BeanUtils.toBean(result, FbCollectUserDO.class);
            
            // 字段映射：Facebook API -> DO
            // 设置 Facebook用户ID
            if (fbUserId != null) {
                fbCollectUser.setFbUserId(fbUserId);
            }
            
            // name -> userName (如果userName为空)
            if (fbCollectUser.getUserName() == null && result.getName() != null) {
                fbCollectUser.setUserName(result.getName());
            }
            
            // snippet -> profileStatus (签名/状态)
            if (result.getProfileStatus() == null && result.getSnippet() != null) {
                fbCollectUser.setProfileStatus(result.getSnippet());
            }
            
            // 清空id字段,让数据库自动生成主键
            fbCollectUser.setId(null);
            fbCollectUserMapper.insert(fbCollectUser);
            count++;
        }
        
        // 2. 使用 Redis 原子递增采集数量
        countService.incrementCollectCount(detailId, count);
        
        // 3. 异步更新数据库和主表(避免阻塞)
        updateDetailAndMainTableAsync(detailId);
        
        return count;
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
        
        // 判断状态
        if (redisCount >= detail.getExpectedCount()) {
            detail.setStatus(2); // 已完成
            detail.setEndTime(LocalDateTime.now());
        } else {
            detail.setStatus(1); // 采集中
        }
        
        fbCollectDetailMapper.updateById(detail);
        
        // 聚合更新主表
        updateMainTaskProgress(detail.getTaskId());
        
        // 清理 Redis 缓存
        countService.removeCountCache(detailId);
        
        log.info("更新明细 {} 完成, 已采集: {}/{}", detailId, redisCount, detail.getExpectedCount());
    }
    
    /**
     * 聚合更新主表进度
     */
    private void updateMainTaskProgress(Long taskId) {
        // 查询所有明细的统计信息
        Map<String, Object> stats = fbCollectDetailMapper.selectTaskStats(taskId);
        if (stats == null || stats.isEmpty()) {
            return;
        }
        
        // 更新主表
        FbCollectDO task = new FbCollectDO();
        task.setId(taskId);
        task.setTotalCollectedCount(((Number) stats.get("total_collected")).intValue());
        
        // 判断主表状态
        Integer totalExpected = ((Number) stats.get("total_expected")).intValue();
        Integer totalCollected = ((Number) stats.get("total_collected")).intValue();
        Long failedCount = stats.get("failed_count") != null ? ((Number) stats.get("failed_count")).longValue() : 0L;
        
        if (totalCollected >= totalExpected) {
            task.setStatus(2); // 已完成
        } else if (failedCount > 0) {
            task.setStatus(3); // 部分失败
        } else {
            task.setStatus(1); // 采集中
        }
        
        fbCollectMapper.updateById(task);
        
        log.info("更新主表 {} 完成, 总进度: {}/{}", taskId, totalCollected, totalExpected);
    }

}