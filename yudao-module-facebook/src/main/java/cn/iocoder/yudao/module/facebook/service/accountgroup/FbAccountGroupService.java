package cn.iocoder.yudao.module.facebook.service.accountgroup;

import cn.iocoder.yudao.framework.common.pojo.PageResult;
import cn.iocoder.yudao.module.facebook.controller.admin.accountgroup.vo.FbAccountGroupSaveReqVO;
import cn.iocoder.yudao.module.facebook.controller.admin.accountgroup.vo.FbAccountGroupRespVO;
import cn.iocoder.yudao.module.facebook.controller.admin.accountgroup.vo.FbAccountGroupPageReqVO;

import java.util.List;

/**
 * Facebook 账号分组 Service 接口
 *
 * @author 芋道源码
 */
public interface FbAccountGroupService {

    /**
     * 创建账号分组
     *
     * @param saveReqVO 创建信息
     * @return 编号
     */
    Long createAccountGroup(FbAccountGroupSaveReqVO saveReqVO);

    /**
     * 更新账号分组
     *
     * @param saveReqVO 更新信息
     */
    void updateAccountGroup(FbAccountGroupSaveReqVO saveReqVO);

    /**
     * 删除账号分组
     *
     * @param id 编号
     */
    void deleteAccountGroup(Long id);

    /**
     * 获得账号分组
     *
     * @param id 编号
     * @return 账号分组
     */
    FbAccountGroupRespVO getAccountGroup(Long id);

    /**
     * 获得账号分组分页
     *
     * @param pageReqVO 分页查询
     * @return 账号分组分页
     */
    PageResult<FbAccountGroupRespVO> getAccountGroupPage(FbAccountGroupPageReqVO pageReqVO);

    /**
     * 获得所有启用的账号分组
     *
     * @return 账号分组列表
     */
    List<FbAccountGroupRespVO> getAllEnabledGroups();

}
