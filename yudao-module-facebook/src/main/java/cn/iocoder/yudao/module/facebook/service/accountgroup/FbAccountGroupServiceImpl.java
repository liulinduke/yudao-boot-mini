package cn.iocoder.yudao.module.facebook.service.accountgroup;

import cn.iocoder.yudao.framework.common.pojo.PageResult;
import cn.iocoder.yudao.framework.common.util.object.BeanUtils;
import cn.iocoder.yudao.module.facebook.controller.admin.accountgroup.vo.FbAccountGroupSaveReqVO;
import cn.iocoder.yudao.module.facebook.controller.admin.accountgroup.vo.FbAccountGroupRespVO;
import cn.iocoder.yudao.module.facebook.controller.admin.accountgroup.vo.FbAccountGroupPageReqVO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.accountgroup.FbAccountGroupDO;
import cn.iocoder.yudao.module.facebook.dal.mysql.accountgroup.FbAccountGroupMapper;
import com.baomidou.mybatisplus.core.conditions.query.LambdaQueryWrapper;
import javax.annotation.Resource;
import org.springframework.stereotype.Service;
import org.springframework.validation.annotation.Validated;

import java.util.List;

import static cn.iocoder.yudao.framework.common.exception.util.ServiceExceptionUtil.exception;
import static cn.iocoder.yudao.module.facebook.enums.ErrorCodeConstants.ACCOUNT_GROUP_NOT_EXISTS;

/**
 * Facebook 账号分组 Service 实现类
 *
 * @author 芋道源码
 */
@Service
@Validated
public class FbAccountGroupServiceImpl implements FbAccountGroupService {

    @Resource
    private FbAccountGroupMapper accountGroupMapper;

    @Override
    public Long createAccountGroup(FbAccountGroupSaveReqVO saveReqVO) {
        FbAccountGroupDO accountGroup = BeanUtils.toBean(saveReqVO, FbAccountGroupDO.class);
        accountGroupMapper.insert(accountGroup);
        return accountGroup.getId();
    }

    @Override
    public void updateAccountGroup(FbAccountGroupSaveReqVO saveReqVO) {
        validateAccountGroupExists(saveReqVO.getId());
        FbAccountGroupDO updateObj = BeanUtils.toBean(saveReqVO, FbAccountGroupDO.class);
        accountGroupMapper.updateById(updateObj);
    }

    @Override
    public void deleteAccountGroup(Long id) {
        validateAccountGroupExists(id);
        accountGroupMapper.deleteById(id);
    }

    @Override
    public FbAccountGroupRespVO getAccountGroup(Long id) {
        FbAccountGroupDO accountGroup = accountGroupMapper.selectById(id);
        return BeanUtils.toBean(accountGroup, FbAccountGroupRespVO.class);
    }

    @Override
    public PageResult<FbAccountGroupRespVO> getAccountGroupPage(FbAccountGroupPageReqVO pageReqVO) {
        PageResult<FbAccountGroupDO> pageResult = accountGroupMapper.selectPage(pageReqVO);
        return BeanUtils.toBean(pageResult, FbAccountGroupRespVO.class);
    }

    @Override
    public List<FbAccountGroupRespVO> getAllEnabledGroups() {
        List<FbAccountGroupDO> list = accountGroupMapper.selectList(
                new LambdaQueryWrapper<FbAccountGroupDO>()
                        .eq(FbAccountGroupDO::getStatus, 1)
                        .orderByAsc(FbAccountGroupDO::getSort));
        return BeanUtils.toBean(list, FbAccountGroupRespVO.class);
    }

    private void validateAccountGroupExists(Long id) {
        if (accountGroupMapper.selectById(id) == null) {
            throw exception(ACCOUNT_GROUP_NOT_EXISTS);
        }
    }

}
