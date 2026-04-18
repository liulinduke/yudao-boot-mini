package cn.iocoder.yudao.module.facebook.dal.mysql.collectuser;

import java.util.*;

import cn.iocoder.yudao.framework.common.pojo.PageResult;
import cn.iocoder.yudao.framework.mybatis.core.query.LambdaQueryWrapperX;
import cn.iocoder.yudao.framework.mybatis.core.mapper.BaseMapperX;
import cn.iocoder.yudao.module.facebook.dal.dataobject.collectuser.FbCollectUserDO;
import org.apache.ibatis.annotations.Mapper;
import cn.iocoder.yudao.module.facebook.controller.admin.collectuser.vo.*;

/**
 * FB用户采集结果 Mapper
 *
 * @author jacky
 */
@Mapper
public interface FbCollectUserMapper extends BaseMapperX<FbCollectUserDO> {

    default PageResult<FbCollectUserDO> selectPage(FbCollectUserPageReqVO reqVO) {
        return selectPage(reqVO, new LambdaQueryWrapperX<FbCollectUserDO>()
                .eqIfPresent(FbCollectUserDO::getTaskId, reqVO.getTaskId())
                .eqIfPresent(FbCollectUserDO::getUserId, reqVO.getUserId())
                .eqIfPresent(FbCollectUserDO::getDeptId, reqVO.getDeptId())
                .eqIfPresent(FbCollectUserDO::getFbAccount, reqVO.getFbAccount())
                .eqIfPresent(FbCollectUserDO::getFbUserId, reqVO.getFbUserId())
                .likeIfPresent(FbCollectUserDO::getUserName, reqVO.getUserName())
                .eqIfPresent(FbCollectUserDO::getUrl, reqVO.getUrl())
                .eqIfPresent(FbCollectUserDO::getDataType, reqVO.getDataType())
                .eqIfPresent(FbCollectUserDO::getFollowers, reqVO.getFollowers())
                .eqIfPresent(FbCollectUserDO::getCity, reqVO.getCity())
                .eqIfPresent(FbCollectUserDO::getLocation, reqVO.getLocation())
                .eqIfPresent(FbCollectUserDO::getHometown, reqVO.getHometown())
                .eqIfPresent(FbCollectUserDO::getPhonenumber, reqVO.getPhonenumber())
                .eqIfPresent(FbCollectUserDO::getPhonenumber2, reqVO.getPhonenumber2())
                .eqIfPresent(FbCollectUserDO::getEmail, reqVO.getEmail())
                .eqIfPresent(FbCollectUserDO::getEmail2, reqVO.getEmail2())
                .eqIfPresent(FbCollectUserDO::getWechat, reqVO.getWechat())
                .eqIfPresent(FbCollectUserDO::getWhatsapp, reqVO.getWhatsapp())
                .eqIfPresent(FbCollectUserDO::getLine, reqVO.getLine())
                .eqIfPresent(FbCollectUserDO::getWebsite, reqVO.getWebsite())
                .eqIfPresent(FbCollectUserDO::getProfileStatus, reqVO.getProfileStatus())
                .eqIfPresent(FbCollectUserDO::getLanguage, reqVO.getLanguage())
                .eqIfPresent(FbCollectUserDO::getGender, reqVO.getGender())
                .eqIfPresent(FbCollectUserDO::getRelationship, reqVO.getRelationship())
                .eqIfPresent(FbCollectUserDO::getWorkExperience, reqVO.getWorkExperience())
                .eqIfPresent(FbCollectUserDO::getEducation, reqVO.getEducation())
                .betweenIfPresent(FbCollectUserDO::getLastPostTime, reqVO.getLastPostTime())
                .eqIfPresent(FbCollectUserDO::getLastPostSummary, reqVO.getLastPostSummary())
                .eqIfPresent(FbCollectUserDO::getGroupId, reqVO.getGroupId())
                .eqIfPresent(FbCollectUserDO::getFromResource, reqVO.getFromResource())
                .eqIfPresent(FbCollectUserDO::getConfig, reqVO.getConfig())
                .betweenIfPresent(FbCollectUserDO::getSyncTime, reqVO.getSyncTime())
                .betweenIfPresent(FbCollectUserDO::getCreateTime, reqVO.getCreateTime())
                .orderByDesc(FbCollectUserDO::getId));
    }

}