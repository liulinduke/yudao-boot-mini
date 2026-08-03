package cn.iocoder.yudao.module.facebook.service.resourcegroup;

import cn.iocoder.yudao.module.facebook.controller.admin.resourcegroup.vo.*;
import java.util.List;

public interface FbResourceGroupService {
    List<FbResourceGroupRespVO> getList(String resourceType);
    Long create(FbResourceGroupSaveReqVO reqVO);
    void update(FbResourceGroupSaveReqVO reqVO);
    void delete(Long id);
}
