package cn.iocoder.yudao.module.system.dal.mysql.script;

import cn.iocoder.yudao.framework.common.pojo.PageResult;
import cn.iocoder.yudao.framework.mybatis.core.query.LambdaQueryWrapperX;
import cn.iocoder.yudao.framework.mybatis.core.mapper.BaseMapperX;
import cn.iocoder.yudao.module.system.controller.admin.script.vo.FbScriptGroupPageReqVO;
import cn.iocoder.yudao.module.system.dal.dataobject.script.FbScriptGroupDO;
import org.apache.ibatis.annotations.Mapper;

import java.util.List;

/**
 * 话术分组 Mapper
 *
 * @author 芋道源码
 */
@Mapper
public interface FbScriptGroupMapper extends BaseMapperX<FbScriptGroupDO> {

    default PageResult<FbScriptGroupDO> selectPage(FbScriptGroupPageReqVO reqVO) {
        return selectPage(reqVO, new LambdaQueryWrapperX<FbScriptGroupDO>()
                .likeIfPresent(FbScriptGroupDO::getGroupName, reqVO.getGroupName())
                .eqIfPresent(FbScriptGroupDO::getGroupType, reqVO.getGroupType())
                .orderByAsc(FbScriptGroupDO::getSort));
    }

    default List<FbScriptGroupDO> selectList() {
        return selectList(new LambdaQueryWrapperX<FbScriptGroupDO>()
                .orderByAsc(FbScriptGroupDO::getSort));
    }

}
