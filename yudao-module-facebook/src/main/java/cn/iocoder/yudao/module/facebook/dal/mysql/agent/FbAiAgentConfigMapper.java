package cn.iocoder.yudao.module.facebook.dal.mysql.agent;

import cn.iocoder.yudao.framework.mybatis.core.mapper.BaseMapperX;
import cn.iocoder.yudao.framework.common.pojo.PageResult;
import cn.iocoder.yudao.framework.mybatis.core.query.LambdaQueryWrapperX;
import cn.iocoder.yudao.module.facebook.controller.admin.agent.vo.FbAiAgentConfigPageReqVO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.agent.FbAiAgentConfigDO;
import org.apache.ibatis.annotations.Mapper;

@Mapper
public interface FbAiAgentConfigMapper extends BaseMapperX<FbAiAgentConfigDO> {

    default PageResult<FbAiAgentConfigDO> selectPage(FbAiAgentConfigPageReqVO reqVO) {
        return selectPage(reqVO, new LambdaQueryWrapperX<FbAiAgentConfigDO>()
                .likeIfPresent(FbAiAgentConfigDO::getAgentName, reqVO.getAgentName())
                .eqIfPresent(FbAiAgentConfigDO::getAgentType, reqVO.getAgentType())
                .eqIfPresent(FbAiAgentConfigDO::getStatus, reqVO.getStatus())
                .orderByDesc(FbAiAgentConfigDO::getId));
    }

}
