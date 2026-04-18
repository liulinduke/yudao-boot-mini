package cn.iocoder.yudao.module.facebook.service.collectuser;

import cn.hutool.core.collection.CollUtil;
import org.springframework.stereotype.Service;
import javax.annotation.Resource;
import org.springframework.validation.annotation.Validated;
import org.springframework.transaction.annotation.Transactional;

import java.util.*;
import cn.iocoder.yudao.module.facebook.controller.admin.collectuser.vo.*;
import cn.iocoder.yudao.module.facebook.dal.dataobject.collectuser.FbCollectUserDO;
import cn.iocoder.yudao.framework.common.pojo.PageResult;
import cn.iocoder.yudao.framework.common.pojo.PageParam;
import cn.iocoder.yudao.framework.common.util.object.BeanUtils;

import cn.iocoder.yudao.module.facebook.dal.mysql.collectuser.FbCollectUserMapper;

import static cn.iocoder.yudao.framework.common.exception.util.ServiceExceptionUtil.exception;
import static cn.iocoder.yudao.framework.common.util.collection.CollectionUtils.convertList;
import static cn.iocoder.yudao.framework.common.util.collection.CollectionUtils.diffList;
import static cn.iocoder.yudao.module.facebook.enums.ErrorCodeConstants.*;

/**
 * FB用户采集结果 Service 实现类
 *
 * @author jacky
 */
@Service
@Validated
public class FbCollectUserServiceImpl implements FbCollectUserService {

    @Resource
    private FbCollectUserMapper fbCollectUserMapper;

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
        validateFbCollectUserExists(updateReqVO.getId());
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
    public Integer batchSaveFbCollectUser(Long taskId, List<FbCollectUserSaveReqVO> results) {
        if (CollUtil.isEmpty(results)) {
            return 0;
        }
        
        int count = 0;
        for (FbCollectUserSaveReqVO result : results) {
            result.setTaskId(taskId);
            
            FbCollectUserDO fbCollectUser = BeanUtils.toBean(result, FbCollectUserDO.class);
            fbCollectUserMapper.insert(fbCollectUser);
            count++;
        }
        
        return count;
    }

}