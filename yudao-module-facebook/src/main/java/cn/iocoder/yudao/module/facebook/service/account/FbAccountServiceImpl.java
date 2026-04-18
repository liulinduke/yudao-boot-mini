package cn.iocoder.yudao.module.facebook.service.account;

import cn.hutool.core.collection.CollUtil;
import org.springframework.stereotype.Service;
import javax.annotation.Resource;
import org.springframework.validation.annotation.Validated;
import org.springframework.transaction.annotation.Transactional;

import java.util.*;
import cn.iocoder.yudao.module.facebook.controller.admin.account.vo.*;
import cn.iocoder.yudao.module.facebook.dal.dataobject.account.FbAccountDO;
import cn.iocoder.yudao.framework.common.pojo.PageResult;
import cn.iocoder.yudao.framework.common.pojo.PageParam;
import cn.iocoder.yudao.framework.common.util.object.BeanUtils;

import cn.iocoder.yudao.module.facebook.dal.mysql.account.FbAccountMapper;

import static cn.iocoder.yudao.framework.common.exception.util.ServiceExceptionUtil.exception;
import static cn.iocoder.yudao.framework.common.util.collection.CollectionUtils.convertList;
import static cn.iocoder.yudao.framework.common.util.collection.CollectionUtils.diffList;
import static cn.iocoder.yudao.module.facebook.enums.ErrorCodeConstants.*;

/**
 * FB账号 Service 实现类
 *
 * @author 芋道源码
 */
@Service
@Validated
public class FbAccountServiceImpl implements FbAccountService {

    @Resource
    private FbAccountMapper fbAccountMapper;

    @Override
    public Long createFbAccount(FbAccountSaveReqVO createReqVO) {
        // 插入
        FbAccountDO fbAccount = BeanUtils.toBean(createReqVO, FbAccountDO.class);
        fbAccountMapper.insert(fbAccount);

        // 返回
        return fbAccount.getId();
    }

    @Override
    public void updateFbAccount(FbAccountSaveReqVO updateReqVO) {
        // 校验存在
        validateFbAccountExists(updateReqVO.getId());
        // 更新
        FbAccountDO updateObj = BeanUtils.toBean(updateReqVO, FbAccountDO.class);
        fbAccountMapper.updateById(updateObj);
    }

    @Override
    public void deleteFbAccount(Long id) {
        // 校验存在
        validateFbAccountExists(id);
        // 删除
        fbAccountMapper.deleteById(id);
    }

    @Override
        public void deleteFbAccountListByIds(List<Long> ids) {
        // 删除
        fbAccountMapper.deleteByIds(ids);
        }


    private void validateFbAccountExists(Long id) {
        if (fbAccountMapper.selectById(id) == null) {
            throw exception(FB_ACCOUNT_NOT_EXISTS);
        }
    }

    @Override
    public FbAccountDO getFbAccount(Long id) {
        return fbAccountMapper.selectById(id);
    }

    @Override
    public PageResult<FbAccountDO> getFbAccountPage(FbAccountPageReqVO pageReqVO) {
        return fbAccountMapper.selectPage(pageReqVO);
    }

}