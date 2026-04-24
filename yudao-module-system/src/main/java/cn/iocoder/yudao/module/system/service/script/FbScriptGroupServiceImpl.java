package cn.iocoder.yudao.module.system.service.script;

import cn.iocoder.yudao.framework.common.pojo.PageResult;
import cn.iocoder.yudao.framework.common.util.object.BeanUtils;
import cn.iocoder.yudao.module.system.controller.admin.script.vo.FbScriptGroupPageReqVO;
import cn.iocoder.yudao.module.system.controller.admin.script.vo.FbScriptGroupSaveReqVO;
import cn.iocoder.yudao.module.system.dal.dataobject.script.FbScriptGroupDO;
import cn.iocoder.yudao.module.system.dal.mysql.script.FbScriptGroupMapper;
import org.springframework.stereotype.Service;
import org.springframework.validation.annotation.Validated;

import javax.annotation.Resource;
import java.util.List;

import static cn.iocoder.yudao.framework.common.exception.util.ServiceExceptionUtil.exception;
import static cn.iocoder.yudao.module.system.enums.ErrorCodeConstants.SCRIPT_GROUP_NOT_EXISTS;

/**
 * 话术分组 Service 实现
 * @author 芋道源码
 */
@Service
@Validated
public class FbScriptGroupServiceImpl implements FbScriptGroupService {

    @Resource
    private FbScriptGroupMapper scriptGroupMapper;

    @Override
    public Long createScriptGroup(FbScriptGroupSaveReqVO createReqVO) {
        FbScriptGroupDO scriptGroup = BeanUtils.toBean(createReqVO, FbScriptGroupDO.class);
        scriptGroupMapper.insert(scriptGroup);
        return scriptGroup.getId();
    }

    @Override
    public void updateScriptGroup(FbScriptGroupSaveReqVO updateReqVO) {
        validateScriptGroupExists(updateReqVO.getId());
        FbScriptGroupDO updateObj = BeanUtils.toBean(updateReqVO, FbScriptGroupDO.class);
        scriptGroupMapper.updateById(updateObj);
    }

    @Override
    public void deleteScriptGroup(Long id) {
        validateScriptGroupExists(id);
        scriptGroupMapper.deleteById(id);
    }

    @Override
    public void deleteScriptGroupListByIds(List<Long> ids) {
        scriptGroupMapper.deleteByIds(ids);
    }

    @Override
    public FbScriptGroupDO getScriptGroup(Long id) {
        return scriptGroupMapper.selectById(id);
    }

    @Override
    public PageResult<FbScriptGroupDO> getScriptGroupPage(FbScriptGroupPageReqVO pageReqVO) {
        return scriptGroupMapper.selectPage(pageReqVO);
    }

    @Override
    public List<FbScriptGroupDO> getScriptGroupList() {
        return scriptGroupMapper.selectList();
    }

    private void validateScriptGroupExists(Long id) {
        if (scriptGroupMapper.selectById(id) == null) {
            throw exception(SCRIPT_GROUP_NOT_EXISTS);
        }
    }

}
