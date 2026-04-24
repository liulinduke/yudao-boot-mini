package cn.iocoder.yudao.module.system.dal.mysql.script;

import cn.iocoder.yudao.framework.common.pojo.PageResult;
import cn.iocoder.yudao.framework.mybatis.core.query.LambdaQueryWrapperX;
import cn.iocoder.yudao.framework.mybatis.core.mapper.BaseMapperX;
import cn.iocoder.yudao.module.system.controller.admin.script.vo.FbScriptPageReqVO;
import cn.iocoder.yudao.module.system.dal.dataobject.script.FbScriptDO;
import org.apache.ibatis.annotations.Mapper;

import java.util.List;

/**
 * 话术内容 Mapper
 *
 * @author 芋道源码
 */
@Mapper
public interface FbScriptMapper extends BaseMapperX<FbScriptDO> {

    default PageResult<FbScriptDO> selectPage(FbScriptPageReqVO reqVO) {
        return selectPage(reqVO, new LambdaQueryWrapperX<FbScriptDO>()
                .eqIfPresent(FbScriptDO::getGroupId, reqVO.getGroupId())
                .likeIfPresent(FbScriptDO::getScriptTitle, reqVO.getScriptTitle())
                .eqIfPresent(FbScriptDO::getContentType, reqVO.getContentType())
                .orderByAsc(FbScriptDO::getSort));
    }

    default List<FbScriptDO> selectListByGroupId(Long groupId) {
        return selectList(new LambdaQueryWrapperX<FbScriptDO>()
                .eq(FbScriptDO::getGroupId, groupId)
                .orderByAsc(FbScriptDO::getSort));
    }

}
