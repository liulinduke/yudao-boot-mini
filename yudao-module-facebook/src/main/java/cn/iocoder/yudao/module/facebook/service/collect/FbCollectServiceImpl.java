package cn.iocoder.yudao.module.facebook.service.collect;

import cn.hutool.core.collection.CollUtil;
import org.springframework.stereotype.Service;
import jakarta.annotation.Resource;
import org.springframework.validation.annotation.Validated;
import org.springframework.transaction.annotation.Transactional;

import java.time.LocalDateTime;
import java.util.*;
import java.util.stream.Collectors;
import cn.iocoder.yudao.module.facebook.controller.admin.collect.vo.*;
import cn.iocoder.yudao.module.facebook.dal.dataobject.account.FbAccountDO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.collect.FbCollectDO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.collectdetail.FbCollectDetailDO;
import cn.iocoder.yudao.module.facebook.dal.mysql.account.FbAccountMapper;
import cn.iocoder.yudao.module.facebook.dal.mysql.collect.FbCollectMapper;
import cn.iocoder.yudao.module.facebook.dal.mysql.collectdetail.FbCollectDetailMapper;
import cn.iocoder.yudao.module.facebook.service.agent.FbAiAgentCollectQueueService;
import cn.iocoder.yudao.framework.mybatis.core.query.LambdaQueryWrapperX;
import cn.iocoder.yudao.framework.common.pojo.PageResult;
import cn.iocoder.yudao.framework.common.pojo.PageParam;
import cn.iocoder.yudao.framework.common.util.object.BeanUtils;

import static cn.iocoder.yudao.framework.common.exception.util.ServiceExceptionUtil.exception;
import static cn.iocoder.yudao.framework.common.util.collection.CollectionUtils.convertList;
import static cn.iocoder.yudao.framework.common.util.collection.CollectionUtils.diffList;
import static cn.iocoder.yudao.module.facebook.enums.ErrorCodeConstants.*;

/**
 * FB采集任务 Service 实现类
 *
 * @author jacky
 */
@Service
@Validated
public class FbCollectServiceImpl implements FbCollectService {

    private static final int DEEP_COLLECT_TASK_TYPE = 12;

    @Resource
    private FbCollectMapper fbCollectMapper;
    
    @Resource
    private FbCollectDetailMapper fbCollectDetailMapper;

    @Resource
    private FbAccountMapper fbAccountMapper;
    @Resource
    private FbAiAgentCollectQueueService accountTaskQueueService;

    @Override
    @Transactional(rollbackFor = Exception.class)
    public FbCollectCreateRespVO createFbCollect(FbCollectSaveReqVO createReqVO) {
        if (createReqVO.getTaskType() != null && createReqVO.getTaskType() == DEEP_COLLECT_TASK_TYPE) {
            return createDeepCollect(createReqVO);
        }
        // 1. 解析URL列表
        List<String> urls = Arrays.stream(createReqVO.getSearchUrl().split("\\n"))
            .filter(url -> url.trim().length() > 0)
            .collect(Collectors.toList());
            
        if (CollUtil.isEmpty(urls)) {
            throw exception(FB_COLLECT_NOT_EXISTS);
        }
            
        // 2. 获取账号列表(从 accountIds 中获取)
        List<Long> accountIds = createReqVO.getAccountIds();
        if (CollUtil.isEmpty(accountIds)) {
            // 兼容旧逻辑,如果没有 accountIds,使用 fbAccount
            accountIds = Collections.singletonList(0L); // 占位
        }
            
        int urlCount = urls.size();
        // 一个目标只分配给一个账号。目标少于账号时，未被分配的账号不启动；
        // 目标多于账号时按账号顺序轮询，后续目标进入同一账号的串行队列。
        List<Long> assignedAccountIds = selectAssignedAccounts(accountIds, urlCount);
        if (CollUtil.isEmpty(assignedAccountIds)) {
            throw exception(FB_COLLECT_NOT_EXISTS);
        }
        int accountCount = assignedAccountIds.size();
            
        // 3. 计算总数
        int totalExpectedCount = urlCount * createReqVO.getExpectedCount();
            
        // 4. 创建主任务
        FbCollectDO task = BeanUtils.toBean(createReqVO, FbCollectDO.class);
        task.setTotalExpectedCount(totalExpectedCount);
        task.setTotalCollectedCount(0);
        task.setAccountCount(accountCount);
        task.setUrlCount(urlCount);
        task.setStatus(1); // 采集中 (改为1而不是0)
        task.setStartTime(LocalDateTime.now()); // 设置开始时间
        fbCollectMapper.insert(task);
            
        Map<Long, String> accountMap = resolveAccountMap(assignedAccountIds, createReqVO.getFbAccount());

        // 5. 创建明细记录(每个目标只分配一个账号)
        List<FbCollectCreateRespVO.DetailInfo> detailInfos = new ArrayList<>();
            
        for (int i = 0; i < urls.size(); i++) {
            Long accountId = assignedAccountIds.get(i % assignedAccountIds.size());
            String fbAccount = accountMap.getOrDefault(accountId, "account_" + accountId);
            String url = urls.get(i).trim();
            FbCollectDetailDO detail = new FbCollectDetailDO();
            detail.setTaskId(task.getId());
            detail.setFbAccount(fbAccount);
            detail.setSearchUrl(url);
            detail.setExpectedCount(createReqVO.getExpectedCount());
            detail.setCollectedCount(0);
            detail.setStatus(0); // 待执行
            fbCollectDetailMapper.insert(detail);
            accountTaskQueueService.push("collect", detail.getId(), detail.getFbAccount());

            detailInfos.add(new FbCollectCreateRespVO.DetailInfo(
                detail.getId(), fbAccount, url, null
            ));
        }
            
        // 6. 返回所有明细ID列表
        return new FbCollectCreateRespVO(task.getId(), detailInfos);
    }

    private FbCollectCreateRespVO createDeepCollect(FbCollectSaveReqVO createReqVO) {
        List<String> rawUrls = Arrays.stream(createReqVO.getSearchUrl().split("\\r?\\n"))
                .map(String::trim)
                .filter(url -> url.length() > 0)
                .collect(Collectors.toList());
        List<Long> rawSourceUserIds = createReqVO.getSourceUserIds();
        List<String> urls = new ArrayList<>();
        List<Long> sourceUserIds = new ArrayList<>();
        Set<String> seenUrls = new LinkedHashSet<>();
        for (int i = 0; i < rawUrls.size(); i++) {
            String url = rawUrls.get(i);
            if (!seenUrls.add(url)) {
                continue;
            }
            urls.add(url);
            sourceUserIds.add(CollUtil.isNotEmpty(rawSourceUserIds) && i < rawSourceUserIds.size() ? rawSourceUserIds.get(i) : null);
        }
        if (CollUtil.isEmpty(urls)) {
            throw exception(FB_COLLECT_NOT_EXISTS);
        }

        List<Long> accountIds = createReqVO.getAccountIds();
        if (CollUtil.isEmpty(accountIds)) {
            throw exception(FB_COLLECT_NOT_EXISTS);
        }

        List<Long> assignedAccountIds = selectAssignedAccounts(accountIds, urls.size());
        if (CollUtil.isEmpty(assignedAccountIds)) {
            throw exception(FB_COLLECT_NOT_EXISTS);
        }

        List<FbAccountDO> accountList = fbAccountMapper.selectBatchIds(assignedAccountIds);
        Map<Long, String> accountMap = accountList.stream()
                .collect(Collectors.toMap(FbAccountDO::getId, FbAccountDO::getFbAccount, (a, b) -> a));

        FbCollectDO task = BeanUtils.toBean(createReqVO, FbCollectDO.class);
        task.setTaskType(DEEP_COLLECT_TASK_TYPE);
        task.setSearchType(0);
        task.setSearchUrl(String.join("\n", urls));
        task.setExpectedCount(1);
        task.setTotalExpectedCount(urls.size());
        task.setTotalCollectedCount(0);
        task.setAccountCount(assignedAccountIds.size());
        task.setUrlCount(urls.size());
        task.setStatus(1);
        task.setStartTime(LocalDateTime.now());
        fbCollectMapper.insert(task);

        List<FbCollectCreateRespVO.DetailInfo> detailInfos = new ArrayList<>();
        for (int i = 0; i < urls.size(); i++) {
            Long accountId = assignedAccountIds.get(i % assignedAccountIds.size());
            String fbAccount = accountMap.get(accountId);
            if (fbAccount == null || fbAccount.trim().isEmpty()) {
                fbAccount = "account_" + accountId;
            }

            FbCollectDetailDO detail = new FbCollectDetailDO();
            detail.setTaskId(task.getId());
            detail.setFbAccount(fbAccount);
            detail.setSearchUrl(urls.get(i));
            detail.setSourceUserId(sourceUserIds.get(i));
            detail.setExpectedCount(1);
            detail.setCollectedCount(0);
            detail.setStatus(0);
            fbCollectDetailMapper.insert(detail);
            accountTaskQueueService.push("collect", detail.getId(), detail.getFbAccount());

            detailInfos.add(new FbCollectCreateRespVO.DetailInfo(
                    detail.getId(),
                    fbAccount,
                    urls.get(i),
                    detail.getSourceUserId()
            ));
        }

        return new FbCollectCreateRespVO(task.getId(), detailInfos);
    }

    private List<Long> selectAssignedAccounts(List<Long> accountIds, int targetCount) {
        if (CollUtil.isEmpty(accountIds) || targetCount <= 0) {
            return Collections.emptyList();
        }
        return accountIds.stream()
                .filter(Objects::nonNull)
                .distinct()
                .limit(Math.min(accountIds.size(), targetCount))
                .collect(Collectors.toList());
    }

    private Map<Long, String> resolveAccountMap(List<Long> accountIds, String fallbackFbAccount) {
        Map<Long, String> accountMap = new LinkedHashMap<>();
        List<Long> realAccountIds = accountIds.stream()
                .filter(Objects::nonNull)
                .filter(id -> id > 0)
                .collect(Collectors.toList());
        if (CollUtil.isNotEmpty(realAccountIds)) {
            List<FbAccountDO> accounts = fbAccountMapper.selectBatchIds(realAccountIds);
            for (FbAccountDO account : accounts) {
                if (account != null && account.getId() != null && account.getFbAccount() != null) {
                    accountMap.put(account.getId(), account.getFbAccount());
                }
            }
        }
        for (Long accountId : accountIds) {
            if (!accountMap.containsKey(accountId)) {
                accountMap.put(accountId, fallbackFbAccount == null || fallbackFbAccount.trim().isEmpty()
                        ? "account_" + accountId : fallbackFbAccount);
            }
        }
        return accountMap;
    }

    @Override
    public void updateFbCollect(FbCollectSaveReqVO updateReqVO) {
        // 校验存在
        validateFbCollectExists(updateReqVO.getId());
        // 更新
        FbCollectDO updateObj = BeanUtils.toBean(updateReqVO, FbCollectDO.class);
        fbCollectMapper.updateById(updateObj);
    }

    @Override
    public void deleteFbCollect(Long id) {
        // 校验存在
        validateFbCollectExists(id);
        // 删除
        fbCollectMapper.deleteById(id);
    }

    @Override
        public void deleteFbCollectListByIds(List<Long> ids) {
        // 删除
        fbCollectMapper.deleteByIds(ids);
        }


    private void validateFbCollectExists(Long id) {
        if (fbCollectMapper.selectById(id) == null) {
            throw exception(FB_COLLECT_NOT_EXISTS);
        }
    }

    @Override
    public FbCollectDO getFbCollect(Long id) {
        return fbCollectMapper.selectById(id);
    }

    @Override
    public PageResult<FbCollectDO> getFbCollectPage(FbCollectPageReqVO pageReqVO) {
        return fbCollectMapper.selectPage(pageReqVO);
    }

}
