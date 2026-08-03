package cn.iocoder.yudao.module.facebook.service.resourcegroup;

import cn.iocoder.yudao.framework.common.util.object.BeanUtils;
import cn.iocoder.yudao.module.facebook.controller.admin.resourcegroup.vo.*;
import cn.iocoder.yudao.module.facebook.dal.dataobject.resourcegroup.FbResourceGroupDO;
import cn.iocoder.yudao.module.facebook.dal.mysql.resourcegroup.FbResourceGroupMapper;
import cn.iocoder.yudao.module.facebook.dal.mysql.collectuser.FbCollectUserMapper;
import cn.iocoder.yudao.module.facebook.dal.mysql.fbcollectgroup.FbCollectGroupMapper;
import cn.iocoder.yudao.module.facebook.dal.mysql.fbcollectpost.FbCollectPostMapper;
import jakarta.annotation.Resource;
import org.springframework.stereotype.Service;
import org.springframework.validation.annotation.Validated;
import java.util.List;

@Service
@Validated
public class FbResourceGroupServiceImpl implements FbResourceGroupService {
    @Resource
    private FbResourceGroupMapper mapper;
    @Resource private FbCollectUserMapper userMapper;
    @Resource private FbCollectGroupMapper groupMapper;
    @Resource private FbCollectPostMapper fbCollectPostMapper;

    @Override
    public List<FbResourceGroupRespVO> getList(String resourceType) {
        List<FbResourceGroupDO> list = mapper.selectByType(resourceType);
        if (list.stream().noneMatch(item -> Boolean.TRUE.equals(item.getIsDefault()))) {
            FbResourceGroupDO defaultGroup = FbResourceGroupDO.builder()
                    .name("未分组").resourceType(resourceType).isDefault(true).build();
            mapper.insert(defaultGroup);
            list = mapper.selectByType(resourceType);
        }
        return BeanUtils.toBean(list, FbResourceGroupRespVO.class);
    }

    @Override
    public Long create(FbResourceGroupSaveReqVO reqVO) {
        validateType(reqVO.getResourceType());
        FbResourceGroupDO data = BeanUtils.toBean(reqVO, FbResourceGroupDO.class);
        data.setIsDefault(false);
        mapper.insert(data);
        return data.getId();
    }

    @Override
    public void update(FbResourceGroupSaveReqVO reqVO) {
        validateType(reqVO.getResourceType());
        FbResourceGroupDO existing = mapper.selectById(reqVO.getId());
        if (existing == null || !existing.getResourceType().equals(reqVO.getResourceType()) || Boolean.TRUE.equals(existing.getIsDefault())) {
            throw new IllegalArgumentException("未分组不能修改");
        }
        existing.setName(reqVO.getName());
        mapper.updateById(existing);
    }

    @Override
    public void delete(Long id) {
        FbResourceGroupDO existing = mapper.selectById(id);
        if (existing == null || Boolean.TRUE.equals(existing.getIsDefault())) {
            throw new IllegalArgumentException("未分组不能删除");
        }
        if ("LEAD".equals(existing.getResourceType())) userMapper.clearResourceGroup(id);
        if ("GROUP".equals(existing.getResourceType())) groupMapper.clearResourceGroup(id);
        if ("POST".equals(existing.getResourceType())) fbCollectPostMapper.clearResourceGroup(id);
        mapper.deleteById(id);
    }

    private void validateType(String resourceType) {
        if (!"LEAD".equals(resourceType) && !"GROUP".equals(resourceType) && !"POST".equals(resourceType)) {
            throw new IllegalArgumentException("不支持的资源类型");
        }
    }
}
