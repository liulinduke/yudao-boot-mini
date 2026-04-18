package cn.iocoder.yudao.module.facebook.service.collect;

import java.util.*;
import javax.validation.*;
import cn.iocoder.yudao.module.facebook.controller.admin.collect.vo.*;
import cn.iocoder.yudao.module.facebook.dal.dataobject.collect.FbCollectDO;
import cn.iocoder.yudao.framework.common.pojo.PageResult;
import cn.iocoder.yudao.framework.common.pojo.PageParam;

/**
 * FB采集任务 Service 接口
 *
 * @author jacky
 */
public interface FbCollectService {

    /**
     * 创建FB采集任务
     *
     * @param createReqVO 创建信息
     * @return 编号
     */
    Long createFbCollect(@Valid FbCollectSaveReqVO createReqVO);

    /**
     * 更新FB采集任务
     *
     * @param updateReqVO 更新信息
     */
    void updateFbCollect(@Valid FbCollectSaveReqVO updateReqVO);

    /**
     * 删除FB采集任务
     *
     * @param id 编号
     */
    void deleteFbCollect(Long id);

    /**
    * 批量删除FB采集任务
    *
    * @param ids 编号
    */
    void deleteFbCollectListByIds(List<Long> ids);

    /**
     * 获得FB采集任务
     *
     * @param id 编号
     * @return FB采集任务
     */
    FbCollectDO getFbCollect(Long id);

    /**
     * 获得FB采集任务分页
     *
     * @param pageReqVO 分页查询
     * @return FB采集任务分页
     */
    PageResult<FbCollectDO> getFbCollectPage(FbCollectPageReqVO pageReqVO);

}