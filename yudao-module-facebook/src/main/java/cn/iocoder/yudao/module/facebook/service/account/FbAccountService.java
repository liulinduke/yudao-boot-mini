package cn.iocoder.yudao.module.facebook.service.account;

import java.util.*;
import jakarta.validation.*;
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

    /** 获取任务账号选择器所需的状态、额度和长期统计。 */
    List<FbAccountSelectorOptionRespVO> getSelectorOptions(FbAccountSelectorOptionReqVO reqVO);

    /**
     * 更新FB账号语言设置
     *
     * @param id 编号
     * @param language 语言：1-英文，2-中文
     */
    void updateFbAccountLanguage(Long id, String languageCode);

    /**
     * 批量更新FB账号代理
     *
     * @param ids 账号ID列表
     * @param proxyId 代理ID
     */
    void updateFbAccountProxy(List<Long> ids, Long proxyId);

    /**
     * 批量更新FB账号分组
     *
     * @param ids 账号ID列表
     * @param groupId 分组ID
     */
    void updateFbAccountGroup(List<Long> ids, Long groupId);

    /** 批量更新 FB 账号启用状态。 */
    void updateFbAccountStatus(List<Long> ids, Boolean status);

    /**
     * 导入FB账号
     *
     * @param importReqVO 导入信息
     */
    void importFbAccount(FbAccountImportReqVO importReqVO);

    /**
     * 导入FB账号Cookie
     *
     * @param importReqVO 导入信息
     */
    void importFbAccountCookie(FbAccountCookieImportReqVO importReqVO);

    void updateFbAccountLoginResult(FbAccountLoginResultUpdateReqVO reqVO);

    /** 保存资料上传任务的待执行资料。 */
    void saveProfileUpload(@Valid FbAccountProfileUploadReqVO reqVO);

    /** 保存资料上传任务执行结果。 */
    void reportProfileUpload(@Valid FbAccountProfileReportReqVO reqVO);

}
