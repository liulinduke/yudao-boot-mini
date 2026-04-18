package cn.iocoder.yudao.module.facebook.service.account;

import java.util.*;
import javax.validation.*;
import cn.iocoder.yudao.module.facebook.controller.admin.account.vo.*;
import cn.iocoder.yudao.module.facebook.dal.dataobject.account.FbAccountDO;
import cn.iocoder.yudao.framework.common.pojo.PageResult;
import cn.iocoder.yudao.framework.common.pojo.PageParam;

/**
 * FB账号 Service 接口
 *
 * @author 芋道源码
 */
public interface FbAccountService {

    /**
     * 创建FB账号
     *
     * @param createReqVO 创建信息
     * @return 编号
     */
    Long createFbAccount(@Valid FbAccountSaveReqVO createReqVO);

    /**
     * 更新FB账号
     *
     * @param updateReqVO 更新信息
     */
    void updateFbAccount(@Valid FbAccountSaveReqVO updateReqVO);

    /**
     * 删除FB账号
     *
     * @param id 编号
     */
    void deleteFbAccount(Long id);

    /**
    * 批量删除FB账号
    *
    * @param ids 编号
    */
    void deleteFbAccountListByIds(List<Long> ids);

    /**
     * 获得FB账号
     *
     * @param id 编号
     * @return FB账号
     */
    FbAccountDO getFbAccount(Long id);

    /**
     * 获得FB账号分页
     *
     * @param pageReqVO 分页查询
     * @return FB账号分页
     */
    PageResult<FbAccountDO> getFbAccountPage(FbAccountPageReqVO pageReqVO);

}