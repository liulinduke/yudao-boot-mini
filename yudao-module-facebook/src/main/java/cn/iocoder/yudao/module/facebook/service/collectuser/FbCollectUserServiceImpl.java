package cn.iocoder.yudao.module.facebook.service.collectuser;

import cn.hutool.core.collection.CollUtil;
import cn.hutool.core.util.StrUtil;
import cn.iocoder.yudao.framework.mybatis.core.query.LambdaQueryWrapperX;
import lombok.extern.slf4j.Slf4j;
import org.springframework.stereotype.Service;
import jakarta.annotation.Resource;
import org.springframework.validation.annotation.Validated;
import org.springframework.transaction.annotation.Transactional;

import java.time.LocalDateTime;
import java.util.*;
import cn.iocoder.yudao.module.facebook.controller.admin.collectuser.vo.*;
import cn.iocoder.yudao.module.facebook.dal.dataobject.collectuser.FbCollectUserDO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.collect.FbCollectDO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.collectdetail.FbCollectDetailDO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.account.FbAccountDO;
import cn.iocoder.yudao.framework.common.pojo.PageResult;
import cn.iocoder.yudao.framework.common.pojo.PageParam;
import cn.iocoder.yudao.framework.common.util.object.BeanUtils;

import cn.iocoder.yudao.module.facebook.dal.mysql.collectuser.FbCollectUserMapper;
import cn.iocoder.yudao.module.facebook.dal.mysql.collect.FbCollectMapper;
import cn.iocoder.yudao.module.facebook.dal.mysql.collectdetail.FbCollectDetailMapper;
import cn.iocoder.yudao.module.facebook.dal.mysql.account.FbAccountMapper;
import cn.iocoder.yudao.module.facebook.service.agent.FbAiAgentCollectQueueService;
import cn.iocoder.yudao.module.facebook.service.agent.FbAiAgentService;
import cn.iocoder.yudao.module.facebook.service.collectdetail.FbCollectCountService;
import cn.iocoder.yudao.module.facebook.service.account.FbAccountActionStatService;

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

    private static final int DEEP_COLLECT_TASK_TYPE = 12;

    @Resource
    private FbCollectUserMapper fbCollectUserMapper;
    
    @Resource
    private FbCollectDetailMapper fbCollectDetailMapper;
    
    @Resource
    private FbCollectMapper fbCollectMapper;
    @Resource
    private FbAccountMapper fbAccountMapper;
    @Resource
    private FbAccountActionStatService actionStatService;
    
    @Resource
    private FbCollectCountService countService;

    @Resource
    private FbAiAgentService aiAgentService;
    @Resource
    private FbAiAgentCollectQueueService aiAgentCollectQueueService;

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
        // 1. 查询明细信息
        FbCollectDetailDO detail = fbCollectDetailMapper.selectById(detailId);
        if (detail == null) {
            log.warn("明细 {} 不存在", detailId);
            return 0;
        }
        FbCollectDO task = fbCollectMapper.selectById(detail.getTaskId());
        Long resourceGroupId = task == null ? null : task.getResourceGroupId();
        boolean deepCollectTask = task != null && task.getTaskType() != null
                && task.getTaskType() == DEEP_COLLECT_TASK_TYPE;
        boolean aiGroupCommentTask = task != null && (StrUtil.startWith(task.getRemark(), "AI群帖评论截流-评论采集:")
                || StrUtil.startWith(task.getRemark(), "AI竞品监控-评论采集:"));
        boolean aiCompetitorCommentTask = task != null && StrUtil.startWith(task.getRemark(), "AI竞品监控-评论采集:");
        
        int count = 0;
        if (CollUtil.isNotEmpty(results)) {
            for (FbCollectUserSaveReqVO result : results) {
                // 设置 taskId 和 fbAccount
                result.setTaskId(detail.getTaskId());
                result.setFbAccount(detail.getFbAccount());
                
                // 先保存 VO 的 id(Facebook用户ID)
                String fbUserId = result.getId();
                // 清空 VO 的 id,避免 BeanUtil 尝试转换到 DO.id(Long类型)
                result.setId(null);
                
                FbCollectUserDO fbCollectUser = BeanUtils.toBean(result, FbCollectUserDO.class);
                if (fbCollectUser.getResourceGroupId() == null) fbCollectUser.setResourceGroupId(resourceGroupId);
                
                // 字段映射：Facebook API -> DO
                // 设置 Facebook用户ID
                if (fbUserId != null) {
                    fbCollectUser.setFbUserId(fbUserId);
                }
                if (deepCollectTask && Objects.equals(fbCollectUser.getFbUserId(), detail.getFbAccount())) {
                    fbCollectUser.setFbUserId(null);
                }
                
                // name -> userName (如果userName为空)
                if (fbCollectUser.getUserName() == null && result.getName() != null) {
                    fbCollectUser.setUserName(result.getName());
                }
                
                // snippet -> profileStatus (签名/状态)
                if (result.getProfileStatus() == null && result.getSnippet() != null) {
                    fbCollectUser.setProfileStatus(result.getSnippet());
                }

                if (deepCollectTask) {
                    fbCollectUser.setDeepCollected(true);
                    fbCollectUser.setSyncTime(LocalDateTime.now());
                    upsertDeepCollectedUser(fbCollectUser, detail.getSourceUserId());
                } else if (aiGroupCommentTask) {
                    if (fbCollectUser.getSourcePostId() == null) {
                        fbCollectUser.setSourcePostId(detail.getSourceUserId());
                    }
                    if (StrUtil.isBlank(fbCollectUser.getSourcePostUrl())) {
                        fbCollectUser.setSourcePostUrl(detail.getSearchUrl());
                    }
                    fbCollectUser.setFromResource(StrUtil.blankToDefault(fbCollectUser.getFromResource(),
                            aiCompetitorCommentTask ? "ai_competitor_comment" : "ai_group_comment"));
                    fbCollectUser.setLeadType(StrUtil.blankToDefault(fbCollectUser.getLeadType(),
                            aiCompetitorCommentTask ? "competitor_comment_lead" : "comment_lead"));
                    fbCollectUser.setTouchStatus(StrUtil.blankToDefault(fbCollectUser.getTouchStatus(), "not_touched"));
                    fbCollectUser.setSyncTime(LocalDateTime.now());
                    upsertAiGroupCommentUser(fbCollectUser);
                } else {
                    // 清空id字段,让数据库自动生成主键
                    fbCollectUser.setId(null);
                    fbCollectUserMapper.insert(fbCollectUser);
                }
                count++;
            }
        }
        
        // 2. 使用 Redis 原子递增采集数量(即使为0也要记录)
        countService.incrementCollectCount(detailId, count);

        // AI 主页、深度采集及评论采集同样可能分批回传，发现记录按已入库用户实时校正。
        aiAgentService.refreshDiscoveryStatsByCollectTaskId(detail.getTaskId());
        
        // 3. 更新数据库和主表。AI Agent 只在整个采集任务完成后继续下一步，避免每条深度采集都触发一次分析/触达。
        boolean taskFinished = updateDetailAndMainTableAsync(detailId);
        if (taskFinished) {
            aiAgentService.continueAfterCollectTaskFinished(detail.getTaskId());
        }
        
        return count;
    }

    private void upsertAiGroupCommentUser(FbCollectUserDO incoming) {
        FbCollectUserDO existing = null;
        if (incoming.getSourcePostId() != null && StrUtil.isNotBlank(incoming.getFbUserId())) {
            List<FbCollectUserDO> existingList = fbCollectUserMapper.selectList(new LambdaQueryWrapperX<FbCollectUserDO>()
                    .eq(FbCollectUserDO::getSourcePostId, incoming.getSourcePostId())
                    .eq(FbCollectUserDO::getFbUserId, incoming.getFbUserId())
                    .last("LIMIT 1"));
            if (CollUtil.isNotEmpty(existingList)) {
                existing = existingList.get(0);
            }
        }
        if (existing == null) {
            incoming.setId(null);
            fbCollectUserMapper.insert(incoming);
            return;
        }
        incoming.setId(existing.getId());
        if (StrUtil.isBlank(incoming.getCommentContent())) {
            incoming.setCommentContent(existing.getCommentContent());
        }
        if (StrUtil.isBlank(incoming.getUrl())) {
            incoming.setUrl(existing.getUrl());
        }
        if (StrUtil.isBlank(incoming.getUserName())) {
            incoming.setUserName(existing.getUserName());
        }
        fbCollectUserMapper.updateById(incoming);
    }

    private void upsertDeepCollectedUser(FbCollectUserDO incoming, Long sourceUserId) {
        FbCollectUserDO existing = sourceUserId == null ? null : fbCollectUserMapper.selectById(sourceUserId);
        if (existing == null) {
            existing = findExistingUser(incoming);
        }
        if (existing == null) {
            incoming.setId(null);
            fbCollectUserMapper.insert(incoming);
            return;
        }
        incoming.setId(existing.getId());
        incoming.setTaskId(existing.getTaskId());
        if (StrUtil.isBlank(incoming.getFbUserId())) {
            incoming.setFbUserId(existing.getFbUserId());
        }
        if (StrUtil.isBlank(incoming.getUrl())) {
            incoming.setUrl(existing.getUrl());
        }
        if (StrUtil.isBlank(incoming.getUserName())) {
            incoming.setUserName(existing.getUserName());
        }
        fbCollectUserMapper.updateById(incoming);
    }

    private FbCollectUserDO findExistingUser(FbCollectUserDO incoming) {
        if (Objects.equals(incoming.getDataType(), 1) && StrUtil.isNotBlank(incoming.getUrl())) {
            List<FbCollectUserDO> existingList = fbCollectUserMapper.selectList(new LambdaQueryWrapperX<FbCollectUserDO>()
                    .eq(FbCollectUserDO::getUrl, incoming.getUrl()));
            if (CollUtil.isNotEmpty(existingList)) {
                return existingList.get(0);
            }
        }
        if (StrUtil.isNotBlank(incoming.getFbUserId())) {
            List<FbCollectUserDO> existingList = fbCollectUserMapper.selectList(new LambdaQueryWrapperX<FbCollectUserDO>()
                    .eq(FbCollectUserDO::getFbUserId, incoming.getFbUserId()));
            if (CollUtil.isNotEmpty(existingList)) {
                return existingList.get(0);
            }
        }
        if (StrUtil.isNotBlank(incoming.getUrl())) {
            List<FbCollectUserDO> existingList = fbCollectUserMapper.selectList(new LambdaQueryWrapperX<FbCollectUserDO>()
                    .eq(FbCollectUserDO::getUrl, incoming.getUrl()));
            if (CollUtil.isNotEmpty(existingList)) {
                return existingList.get(0);
            }
        }
        return null;
    }
    
    /**
     * 异步更新明细表和主表
     */
    private boolean updateDetailAndMainTableAsync(Long detailId) {
        // TODO: 使用 @Async 注解实现真正的异步
        // 这里暂时同步执行,后续可以优化
        try {
            return updateDetailAndMainTable(detailId);
        } catch (Exception e) {
            log.error("更新明细和主表失败, detailId={}", detailId, e);
            return false;
        }
    }
    
    /**
     * 更新明细表和主表
     */
    private boolean updateDetailAndMainTable(Long detailId) {
        // 从 Redis 获取最新计数
        Long redisCount = countService.getCollectCount(detailId);
        
        // 更新明细表
        FbCollectDetailDO detail = fbCollectDetailMapper.selectById(detailId);
        if (detail == null) {
            return false;
        }
        
        detail.setCollectedCount(redisCount.intValue());
        
        // 采集脚本一旦返回结果（即使数量不足或为 0），本轮明细也视为结束
        detail.setStatus(2); // 已完成
        detail.setEndTime(LocalDateTime.now());
        recordCollectStat(detail, redisCount == null ? 0 : redisCount);
        
        fbCollectDetailMapper.updateById(detail);
        aiAgentCollectQueueService.releaseRunning(detail.getFbAccount());
        
        // 聚合更新主表
        boolean taskFinished = updateMainTaskProgress(detail.getTaskId());
        
        // 清理 Redis 缓存
        countService.removeCountCache(detailId);
        
        log.info("更新明细 {} 完成, 已采集: {}/{}", detailId, redisCount, detail.getExpectedCount());
        return taskFinished;
    }
    
    /**
     * 聚合更新主表进度
     */
    private boolean updateMainTaskProgress(Long taskId) {
        // 查询所有明细的统计信息
        Map<String, Object> stats = fbCollectDetailMapper.selectTaskStats(taskId);
        if (stats == null || stats.isEmpty()) {
            return false;
        }
        
        // 更新主表
        FbCollectDO task = new FbCollectDO();
        task.setId(taskId);
        task.setTotalCollectedCount(((Number) stats.get("total_collected")).intValue());
        
        // 判断主表状态
        Integer totalExpected = ((Number) stats.get("total_expected")).intValue();
        Integer totalCollected = ((Number) stats.get("total_collected")).intValue();
        List<FbCollectDetailDO> details = fbCollectDetailMapper.selectListByTaskId(taskId);
        long unfinishedCount = details.stream()
                .filter(d -> d.getStatus() != null && (d.getStatus() == 0 || d.getStatus() == 1))
                .count();
        Long failedCount = stats.get("failed_count") != null ? ((Number) stats.get("failed_count")).longValue() : 0L;

        if (unfinishedCount == 0) {
            task.setStatus(2); // 已完成
        } else if (failedCount > 0) {
            task.setStatus(3); // 部分失败
        } else {
            task.setStatus(1); // 采集中
        }
        
        fbCollectMapper.updateById(task);
        
        log.info("更新主表 {} 完成, 总进度: {}/{}", taskId, totalCollected, totalExpected);
        return unfinishedCount == 0;
    }

    private void recordCollectStat(FbCollectDetailDO detail, long count) {
        FbAccountDO account = fbAccountMapper.selectOne(new LambdaQueryWrapperX<FbAccountDO>()
                .eq(FbAccountDO::getFbAccount, detail.getFbAccount()).last("LIMIT 1"));
        if (account != null) actionStatService.recordSuccess(account.getId(), "collect", count, count);
    }

}
