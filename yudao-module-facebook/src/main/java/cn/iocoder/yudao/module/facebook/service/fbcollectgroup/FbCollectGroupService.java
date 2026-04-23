package cn.iocoder.yudao.module.facebook.service.fbcollectgroup;

import java.util.*;
import javax.validation.*;
import cn.iocoder.yudao.module.facebook.controller.admin.fbcollectgroup.vo.*;
import cn.iocoder.yudao.module.facebook.dal.dataobject.fbcollectgroup.FbCollectGroupDO;
import cn.iocoder.yudao.framework.common.pojo.PageResult;
import cn.iocoder.yudao.framework.common.pojo.PageParam;

/**
 * FB群组采集结果 Service 接口
 *
 * @author jacky
 */
public interface FbCollectGroupService {

    /**
     * 创建FB群组采集结果
     *
     * @param createReqVO 创建信息
     * @return 编号
     */
    Long createFbCollectGroup(@Valid FbCollectGroupSaveReqVO createReqVO);

    /**
     * 更新FB群组采集结果
     *
     * @param updateReqVO 更新信息
     */
    void updateFbCollectGroup(@Valid FbCollectGroupSaveReqVO updateReqVO);

    /**
     * 删除FB群组采集结果
     *
     * @param id 编号
     */
    void deleteFbCollectGroup(Long id);

    /**
    * 批量删除FB群组采集结果
    *
    * @param ids 编号
    */
    void deleteFbCollectGroupListByIds(List<Long> ids);

    /**
     * 获得FB群组采集结果
     *
     * @param id 编号
     * @return FB群组采集结果
     */
    FbCollectGroupDO getFbCollectGroup(Long id);

    /**
     * 获得FB群组采集结果分页
     *
     * @param pageReqVO 分页查询
     * @return FB群组采集结果分页
     */
    PageResult<FbCollectGroupDO> getFbCollectGroupPage(FbCollectGroupPageReqVO pageReqVO);

}