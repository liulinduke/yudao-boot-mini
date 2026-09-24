package cn.iocoder.yudao.module.facebook.dal.mysql.agent;

import cn.iocoder.yudao.framework.mybatis.core.mapper.BaseMapperX;
import cn.iocoder.yudao.framework.common.pojo.PageResult;
import cn.iocoder.yudao.module.facebook.controller.admin.agent.vo.FbAiAgentConfigPageReqVO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.agent.FbAiAgentConfigDO;
import cn.hutool.core.util.StrUtil;
import com.baomidou.mybatisplus.core.conditions.query.QueryWrapper;
import org.apache.ibatis.annotations.Mapper;

@Mapper
public interface FbAiAgentConfigMapper extends BaseMapperX<FbAiAgentConfigDO> {

    default PageResult<FbAiAgentConfigDO> selectPage(FbAiAgentConfigPageReqVO reqVO) {
        return selectPage(reqVO, new QueryWrapper<FbAiAgentConfigDO>()
                .like(StrUtil.isNotBlank(reqVO.getAgentName()), "agent_name", reqVO.getAgentName())
                .eq(StrUtil.isNotBlank(reqVO.getAgentType()), "agent_type", reqVO.getAgentType())
                .eq(reqVO.getStatus() != null, "status", reqVO.getStatus())
                .orderByAsc("CASE WHEN status = 1 THEN 0 ELSE 1 END")
                .orderByDesc("id"));
    }

}
