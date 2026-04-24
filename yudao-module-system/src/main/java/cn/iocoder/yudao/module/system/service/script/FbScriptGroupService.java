package cn.iocoder.yudao.module.system.service.script;

import cn.iocoder.yudao.framework.common.pojo.PageResult;
import cn.iocoder.yudao.module.system.controller.admin.script.vo.FbScriptGroupPageReqVO;
import cn.iocoder.yudao.module.system.controller.admin.script.vo.FbScriptGroupSaveReqVO;
import cn.iocoder.yudao.module.system.dal.dataobject.script.FbScriptGroupDO;

import javax.validation.Valid;
import java.util.List;

/**
 * 话术分组 Service 接口
 *
 * @author 芋道源码
 */
public interface FbScriptGroupService {

    /**
     * 创建话术分组
     *
     * @param createReqVO 创建信息
     * @return 编号
     */
    Long createScriptGroup(@Valid FbScriptGroupSaveReqVO createReqVO);

    /**
     * 更新话术分组
     *
     * @param updateReqVO 更新信息
     */
    void updateScriptGroup(@Valid FbScriptGroupSaveReqVO updateReqVO);

    /**
     * 删除话术分组
     *
     * @param id 编号
     */
    void deleteScriptGroup(Long id);

    /**
     * 批量删除话术分组
     *
     * @param ids 编号
     */
    void deleteScriptGroupListByIds(List<Long> ids);

    /**
     * 获得话术分组
     *
     * @param id 编号
     * @return 话术分组
     */
    FbScriptGroupDO getScriptGroup(Long id);

    /**
     * 获得话术分组分页
     *
     * @param pageReqVO 分页查询
     * @return 话术分组分页
     */
    PageResult<FbScriptGroupDO> getScriptGroupPage(FbScriptGroupPageReqVO pageReqVO);

    /**
     * 获得所有话术分组列
     * @return 话术分组列表
     */
    List<FbScriptGroupDO> getScriptGroupList();

}
