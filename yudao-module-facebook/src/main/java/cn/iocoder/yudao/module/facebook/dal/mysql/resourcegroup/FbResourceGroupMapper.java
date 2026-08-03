package cn.iocoder.yudao.module.facebook.dal.mysql.resourcegroup;

import cn.iocoder.yudao.framework.mybatis.core.mapper.BaseMapperX;
import cn.iocoder.yudao.framework.mybatis.core.query.LambdaQueryWrapperX;
import cn.iocoder.yudao.module.facebook.dal.dataobject.resourcegroup.FbResourceGroupDO;
import org.apache.ibatis.annotations.Mapper;
import java.util.List;

@Mapper
public interface FbResourceGroupMapper extends BaseMapperX<FbResourceGroupDO> {
    default List<FbResourceGroupDO> selectByType(String resourceType) {
        return selectList(new LambdaQueryWrapperX<FbResourceGroupDO>()
                .eq(FbResourceGroupDO::getResourceType, resourceType)
                .orderByAsc(FbResourceGroupDO::getIsDefault)
                .orderByAsc(FbResourceGroupDO::getId));
    }
}
