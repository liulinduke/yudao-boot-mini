package cn.iocoder.yudao.module.facebook.service.collect;

import cn.hutool.core.collection.CollUtil;
import org.springframework.stereotype.Service;
import javax.annotation.Resource;
import org.springframework.validation.annotation.Validated;
import org.springframework.transaction.annotation.Transactional;

import java.util.*;
import java.util.stream.Collectors;
import cn.iocoder.yudao.module.facebook.controller.admin.collect.vo.*;
import cn.iocoder.yudao.module.facebook.dal.dataobject.collect.FbCollectDO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.collectdetail.FbCollectDetailDO;
import cn.iocoder.yudao.module.facebook.dal.mysql.collect.FbCollectMapper;
import cn.iocoder.yudao.module.facebook.dal.mysql.collectdetail.FbCollectDetailMapper;
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

    @Resource
    private FbCollectMapper fbCollectMapper;
    
    @Resource
    private FbCollectDetailMapper fbCollectDetailMapper;

    @Override
    @Transactional(rollbackFor = Exception.class)
    public FbCollectCreateRespVO createFbCollect(FbCollectSaveReqVO createReqVO) {
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
            
        int accountCount = accountIds.size();
        int urlCount = urls.size();
            
        // 3. 计算总数
        int totalExpectedCount = accountCount * urlCount * createReqVO.getExpectedCount();
            
        // 4. 创建主任务
        FbCollectDO task = BeanUtils.toBean(createReqVO, FbCollectDO.class);
        task.setTotalExpectedCount(totalExpectedCount);
        task.setTotalCollectedCount(0);
        task.setAccountCount(accountCount);
        task.setUrlCount(urlCount);
        task.setStatus(0); // 待执行
        fbCollectMapper.insert(task);
            
        // 5. 创建明细记录(每个账号×每个链接)
        // TODO: 这里需要根据 accountIds 查询真实的 fbAccount
        // 暂时简化处理,使用传入的 fbAccount
        String fbAccount = createReqVO.getFbAccount();
        List<FbCollectCreateRespVO.DetailInfo> detailInfos = new ArrayList<>();
            
        for (Long accountId : accountIds) {
            for (String url : urls) {
                FbCollectDetailDO detail = new FbCollectDetailDO();
                detail.setTaskId(task.getId());
                detail.setFbAccount(fbAccount); // TODO: 需要根据 accountId 查询
                detail.setSearchUrl(url.trim());
                detail.setExpectedCount(createReqVO.getExpectedCount());
                detail.setCollectedCount(0);
                detail.setStatus(0); // 待执行
                fbCollectDetailMapper.insert(detail);
                    
                // 收集明细信息
                detailInfos.add(new FbCollectCreateRespVO.DetailInfo(
                    detail.getId(),
                    fbAccount,
                    url.trim()
                ));
            }
        }
            
        // 6. 返回所有明细ID列表
        return new FbCollectCreateRespVO(task.getId(), detailInfos);
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