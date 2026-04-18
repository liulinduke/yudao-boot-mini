package cn.iocoder.yudao.module.facebook.service.collectuser;

import java.util.*;
import javax.validation.*;
import cn.iocoder.yudao.module.facebook.controller.admin.collectuser.vo.*;
import cn.iocoder.yudao.module.facebook.dal.dataobject.collectuser.FbCollectUserDO;
import cn.iocoder.yudao.framework.common.pojo.PageResult;
import cn.iocoder.yudao.framework.common.pojo.PageParam;

/**
 * FB用户采集结果 Service 接口
 *
 * @author jacky
 */
public interface FbCollectUserService {

    /**
     * 创建FB用户采集结果
     *
     * @param createReqVO 创建信息
     * @return 编号
     */
    Long createFbCollectUser(@Valid FbCollectUserSaveReqVO createReqVO);

    /**
     * 更新FB用户采集结果
     *
     * @param updateReqVO 更新信息
     */
    void updateFbCollectUser(@Valid FbCollectUserSaveReqVO updateReqVO);

    /**
     * 删除FB用户采集结果
     *
     * @param id 编号
     */
    void deleteFbCollectUser(Long id);

    /**
    * 批量删除FB用户采集结果
    *
    * @param ids 编号
    */
    void deleteFbCollectUserListByIds(List<Long> ids);

    /**
     * 获得FB用户采集结果
     *
     * @param id 编号
     * @return FB用户采集结果
     */
    FbCollectUserDO getFbCollectUser(Long id);

    /**
     * 获得FB用户采集结果分页
     *
     * @param pageReqVO 分页查询
     * @return FB用户采集结果分页
     */
    PageResult<FbCollectUserDO> getFbCollectUserPage(FbCollectUserPageReqVO pageReqVO);

    /**
     * 批量保存FB用户采集结果
     *
     * @param taskId 任务ID
     * @param results 采集结果列表
     * @return 保存成功的数量
     */
    Integer batchSaveFbCollectUser(Long taskId, List<FbCollectUserSaveReqVO> results);

}