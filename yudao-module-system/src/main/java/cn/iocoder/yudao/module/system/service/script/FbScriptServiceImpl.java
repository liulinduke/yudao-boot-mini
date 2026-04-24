package cn.iocoder.yudao.module.system.service.script;

import cn.iocoder.yudao.framework.common.pojo.PageResult;
import cn.iocoder.yudao.framework.common.util.object.BeanUtils;
import cn.iocoder.yudao.module.system.controller.admin.script.vo.FbScriptPageReqVO;
import cn.iocoder.yudao.module.system.controller.admin.script.vo.FbScriptSaveReqVO;
import cn.iocoder.yudao.module.system.dal.dataobject.script.FbScriptDO;
import cn.iocoder.yudao.module.system.dal.mysql.script.FbScriptMapper;
import org.springframework.stereotype.Service;
import org.springframework.validation.annotation.Validated;

import javax.annotation.Resource;
import java.util.List;

import static cn.iocoder.yudao.framework.common.exception.util.ServiceExceptionUtil.exception;
import static cn.iocoder.yudao.module.system.enums.ErrorCodeConstants.SCRIPT_NOT_EXISTS;

/**
 * 话术内容 Service 实现
 * @author 芋道源码
 */
@Service
@Validated
public class FbScriptServiceImpl implements FbScriptService {

    @Resource
    private FbScriptMapper scriptMapper;

    @Override
    public Long createScript(FbScriptSaveReqVO createReqVO) {
        FbScriptDO script = BeanUtils.toBean(createReqVO, FbScriptDO.class);
        scriptMapper.insert(script);
        return script.getId();
    }

    @Override
    public void updateScript(FbScriptSaveReqVO updateReqVO) {
        validateScriptExists(updateReqVO.getId());
        FbScriptDO updateObj = BeanUtils.toBean(updateReqVO, FbScriptDO.class);
        scriptMapper.updateById(updateObj);
    }

    @Override
    public void deleteScript(Long id) {
        validateScriptExists(id);
        scriptMapper.deleteById(id);
    }

    @Override
    public void deleteScriptListByIds(List<Long> ids) {
        scriptMapper.deleteByIds(ids);
    }

    @Override
    public FbScriptDO getScript(Long id) {
        return scriptMapper.selectById(id);
    }

    @Override
    public PageResult<FbScriptDO> getScriptPage(FbScriptPageReqVO pageReqVO) {
        return scriptMapper.selectPage(pageReqVO);
    }

    @Override
    public List<FbScriptDO> getScriptListByGroupId(Long groupId) {
        return scriptMapper.selectListByGroupId(groupId);
    }

    private void validateScriptExists(Long id) {
        if (scriptMapper.selectById(id) == null) {
            throw exception(SCRIPT_NOT_EXISTS);
        }
    }

}
