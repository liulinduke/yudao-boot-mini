package cn.iocoder.yudao.module.facebook.dal.mysql.agent;

import cn.iocoder.yudao.framework.common.pojo.PageResult;
import cn.iocoder.yudao.framework.mybatis.core.mapper.BaseMapperX;
import cn.iocoder.yudao.framework.mybatis.core.query.LambdaQueryWrapperX;
import cn.iocoder.yudao.module.facebook.controller.admin.agent.vo.FbAiAgentDiscoveryLogPageReqVO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.agent.FbAiAgentDiscoveryLogDO;
import org.apache.ibatis.annotations.Mapper;

@Mapper
public interface FbAiAgentDiscoveryLogMapper extends BaseMapperX<FbAiAgentDiscoveryLogDO> {

    default PageResult<FbAiAgentDiscoveryLogDO> selectPage(FbAiAgentDiscoveryLogPageReqVO reqVO) {
        return selectPage(reqVO, new LambdaQueryWrapperX<FbAiAgentDiscoveryLogDO>()
                .eqIfPresent(FbAiAgentDiscoveryLogDO::getAgentConfigId, reqVO.getAgentConfigId())
                .likeIfPresent(FbAiAgentDiscoveryLogDO::getKeyword, reqVO.getKeyword())
                .orderByDesc(FbAiAgentDiscoveryLogDO::getId));
    }

}
