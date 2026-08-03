package cn.iocoder.yudao.module.facebook.dal.mysql.fbcollectpost;

import java.util.*;

import cn.iocoder.yudao.framework.common.pojo.PageResult;
import cn.iocoder.yudao.framework.mybatis.core.query.LambdaQueryWrapperX;
import cn.iocoder.yudao.framework.mybatis.core.mapper.BaseMapperX;
import cn.iocoder.yudao.module.facebook.dal.dataobject.fbcollectpost.FbCollectPostDO;
import org.apache.ibatis.annotations.Mapper;
import org.apache.ibatis.annotations.Update;
import org.apache.ibatis.annotations.Param;
import cn.iocoder.yudao.module.facebook.controller.admin.fbcollectpost.vo.*;

/**
 * FB帖子采集结果 Mapper
 *
 * @author jacky
 */
@Mapper
public interface FbCollectPostMapper extends BaseMapperX<FbCollectPostDO> {

    @Update("UPDATE fb_collect_post SET resource_group_id = NULL WHERE resource_group_id = #{id}")
    int clearResourceGroup(@Param("id") Long id);

    default PageResult<FbCollectPostDO> selectPage(FbCollectPostPageReqVO reqVO) {
        return selectPage(reqVO, new LambdaQueryWrapperX<FbCollectPostDO>()
                .eqIfPresent(FbCollectPostDO::getTaskId, reqVO.getTaskId())
                .eqIfPresent(FbCollectPostDO::getItemId, reqVO.getItemId())
                .eqIfPresent(FbCollectPostDO::getPostUser, reqVO.getPostUser())
                .eqIfPresent(FbCollectPostDO::getUrl, reqVO.getUrl())
                .eqIfPresent(FbCollectPostDO::getFromResource, reqVO.getFromResource())
                .likeIfPresent(FbCollectPostDO::getGroupName, reqVO.getGroupName())
                .eqIfPresent(FbCollectPostDO::getSysGroupId, reqVO.getSysGroupId())
                .eqIfPresent(FbCollectPostDO::getUserId, reqVO.getUserId())
                .eqIfPresent(FbCollectPostDO::getDeptId, reqVO.getDeptId())
                .eqIfPresent(FbCollectPostDO::getGroupId, reqVO.getGroupId())
                .eqIfPresent(FbCollectPostDO::getResourceGroupId, reqVO.getResourceGroupId())
                .eqIfPresent(FbCollectPostDO::getReshareCount, reqVO.getReshareCount())
                .eqIfPresent(FbCollectPostDO::getCommentCount, reqVO.getCommentCount())
                .eqIfPresent(FbCollectPostDO::getReactionCount, reqVO.getReactionCount())
                .eqIfPresent(FbCollectPostDO::getUsedCount, reqVO.getUsedCount())
                .eqIfPresent(FbCollectPostDO::getPostContent, reqVO.getPostContent())
                .eqIfPresent(FbCollectPostDO::getFbAccount, reqVO.getFbAccount())
                .betweenIfPresent(FbCollectPostDO::getPostCreateTime, reqVO.getPostCreateTime())
                .likeIfPresent(FbCollectPostDO::getAiTags, reqVO.getAiTags())
                .eqIfPresent(FbCollectPostDO::getIntentLevel, reqVO.getIntentLevel())
                .eqIfPresent(FbCollectPostDO::getTouchStatus, reqVO.getTouchStatus())
                .eqIfPresent(FbCollectPostDO::getCountry, reqVO.getCountry())
                .orderByDesc(FbCollectPostDO::getId));
    }

}
