package cn.iocoder.yudao.module.facebook.dal.mysql.agent;

import cn.iocoder.yudao.framework.common.pojo.PageResult;
import cn.iocoder.yudao.framework.mybatis.core.mapper.BaseMapperX;
import cn.iocoder.yudao.framework.mybatis.core.query.LambdaQueryWrapperX;
import cn.iocoder.yudao.module.facebook.controller.admin.agent.vo.FbAiAgentRunLogPageReqVO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.agent.FbAiAgentRunLogDO;
import org.apache.ibatis.annotations.Mapper;

@Mapper
public interface FbAiAgentRunLogMapper extends BaseMapperX<FbAiAgentRunLogDO> {

    default PageResult<FbAiAgentRunLogDO> selectPage(FbAiAgentRunLogPageReqVO reqVO) {
        return selectPage(reqVO, new LambdaQueryWrapperX<FbAiAgentRunLogDO>()
                .eqIfPresent(FbAiAgentRunLogDO::getAgentConfigId, reqVO.getAgentConfigId())
                .orderByDesc(FbAiAgentRunLogDO::getId));
    }

}
