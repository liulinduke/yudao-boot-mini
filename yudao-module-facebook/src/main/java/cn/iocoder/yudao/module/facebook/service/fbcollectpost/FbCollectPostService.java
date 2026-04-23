package cn.iocoder.yudao.module.facebook.service.fbcollectpost;

import java.util.*;
import javax.validation.*;
import cn.iocoder.yudao.module.facebook.controller.admin.fbcollectpost.vo.*;
import cn.iocoder.yudao.module.facebook.dal.dataobject.fbcollectpost.FbCollectPostDO;
import cn.iocoder.yudao.framework.common.pojo.PageResult;
import cn.iocoder.yudao.framework.common.pojo.PageParam;

/**
 * FB帖子采集结果 Service 接口
 *
 * @author jacky
 */
public interface FbCollectPostService {

    /**
     * 创建FB帖子采集结果
     *
     * @param createReqVO 创建信息
     * @return 编号
     */
    Long createFbCollectPost(@Valid FbCollectPostSaveReqVO createReqVO);

    /**
     * 更新FB帖子采集结果
     *
     * @param updateReqVO 更新信息
     */
    void updateFbCollectPost(@Valid FbCollectPostSaveReqVO updateReqVO);

    /**
     * 删除FB帖子采集结果
     *
     * @param id 编号
     */
    void deleteFbCollectPost(Long id);

    /**
    * 批量删除FB帖子采集结果
    *
    * @param ids 编号
    */
    void deleteFbCollectPostListByIds(List<Long> ids);

    /**
     * 获得FB帖子采集结果
     *
     * @param id 编号
     * @return FB帖子采集结果
     */
    FbCollectPostDO getFbCollectPost(Long id);

    /**
     * 获得FB帖子采集结果分页
     *
     * @param pageReqVO 分页查询
     * @return FB帖子采集结果分页
     */
    PageResult<FbCollectPostDO> getFbCollectPostPage(FbCollectPostPageReqVO pageReqVO);

}