package cn.iocoder.yudao.module.facebook.dal.mysql.agent;

import cn.iocoder.yudao.framework.common.pojo.PageResult;
import cn.iocoder.yudao.framework.mybatis.core.mapper.BaseMapperX;
import cn.iocoder.yudao.framework.mybatis.core.query.LambdaQueryWrapperX;
import cn.iocoder.yudao.module.facebook.controller.admin.agent.vo.FbAiTouchRecordPageReqVO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.agent.FbAiTouchRecordDO;
import org.apache.ibatis.annotations.Mapper;

@Mapper
public interface FbAiTouchRecordMapper extends BaseMapperX<FbAiTouchRecordDO> {

    default PageResult<FbAiTouchRecordDO> selectPage(FbAiTouchRecordPageReqVO reqVO) {
        return selectPage(reqVO, new LambdaQueryWrapperX<FbAiTouchRecordDO>()
                .eqIfPresent(FbAiTouchRecordDO::getAgentConfigId, reqVO.getAgentConfigId())
                .eqIfPresent(FbAiTouchRecordDO::getLeadType, reqVO.getLeadType())
                .eqIfPresent(FbAiTouchRecordDO::getLeadId, reqVO.getLeadId())
                .eqIfPresent(FbAiTouchRecordDO::getTouchType, reqVO.getTouchType())
                .eqIfPresent(FbAiTouchRecordDO::getStatus, reqVO.getStatus())
                .betweenIfPresent(FbAiTouchRecordDO::getCreateTime, reqVO.getCreateTime())
                .orderByDesc(FbAiTouchRecordDO::getId));
    }

}
