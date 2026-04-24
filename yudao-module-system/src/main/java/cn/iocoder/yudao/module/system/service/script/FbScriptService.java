package cn.iocoder.yudao.module.system.service.script;

import cn.iocoder.yudao.framework.common.pojo.PageResult;
import cn.iocoder.yudao.module.system.controller.admin.script.vo.FbScriptPageReqVO;
import cn.iocoder.yudao.module.system.controller.admin.script.vo.FbScriptSaveReqVO;
import cn.iocoder.yudao.module.system.dal.dataobject.script.FbScriptDO;

import javax.validation.Valid;
import java.util.List;

/**
 * 话术内容 Service 接口
 *
 * @author 芋道源码
 */
public interface FbScriptService {

    /**
     * 创建话术内容
     *
     * @param createReqVO 创建信息
     * @return 编号
     */
    Long createScript(@Valid FbScriptSaveReqVO createReqVO);

    /**
     * 更新话术内容
     *
     * @param updateReqVO 更新信息
     */
    void updateScript(@Valid FbScriptSaveReqVO updateReqVO);

    /**
     * 删除话术内容
     *
     * @param id 编号
     */
    void deleteScript(Long id);

    /**
     * 批量删除话术内容
     *
     * @param ids 编号
     */
    void deleteScriptListByIds(List<Long> ids);

    /**
     * 获得话术内容
     *
     * @param id 编号
     * @return 话术内容
     */
    FbScriptDO getScript(Long id);

    /**
     * 获得话术内容分页
     *
     * @param pageReqVO 分页查询
     * @return 话术内容分页
     */
    PageResult<FbScriptDO> getScriptPage(FbScriptPageReqVO pageReqVO);

    /**
     * 根据分组ID获得话术列表
     *
     * @param groupId 分组ID
     * @return 话术列表
     */
    List<FbScriptDO> getScriptListByGroupId(Long groupId);

}
