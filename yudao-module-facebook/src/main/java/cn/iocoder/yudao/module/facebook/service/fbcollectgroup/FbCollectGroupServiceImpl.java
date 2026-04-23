package cn.iocoder.yudao.module.facebook.service.fbcollectgroup;

import cn.hutool.core.collection.CollUtil;
import org.springframework.stereotype.Service;
import javax.annotation.Resource;
import org.springframework.validation.annotation.Validated;
import org.springframework.transaction.annotation.Transactional;

import java.util.*;
import cn.iocoder.yudao.module.facebook.controller.admin.fbcollectgroup.vo.*;
import cn.iocoder.yudao.module.facebook.dal.dataobject.fbcollectgroup.FbCollectGroupDO;
import cn.iocoder.yudao.framework.common.pojo.PageResult;
import cn.iocoder.yudao.framework.common.pojo.PageParam;
import cn.iocoder.yudao.framework.common.util.object.BeanUtils;

import cn.iocoder.yudao.module.facebook.dal.mysql.fbcollectgroup.FbCollectGroupMapper;

import static cn.iocoder.yudao.framework.common.exception.util.ServiceExceptionUtil.exception;
import static cn.iocoder.yudao.framework.common.util.collection.CollectionUtils.convertList;
import static cn.iocoder.yudao.framework.common.util.collection.CollectionUtils.diffList;
import static cn.iocoder.yudao.module.facebook.enums.ErrorCodeConstants.*;

/**
 * FB群组采集结果 Service 实现类
 *
 * @author jacky
 */
@Service
@Validated
public class FbCollectGroupServiceImpl implements FbCollectGroupService {

    @Resource
    private FbCollectGroupMapper fbCollectGroupMapper;

    @Override
    public Long createFbCollectGroup(FbCollectGroupSaveReqVO createReqVO) {
        // 插入
        FbCollectGroupDO fbCollectGroup = BeanUtils.toBean(createReqVO, FbCollectGroupDO.class);
        fbCollectGroupMapper.insert(fbCollectGroup);

        // 返回
        return fbCollectGroup.getId();
    }

    @Override
    public void updateFbCollectGroup(FbCollectGroupSaveReqVO updateReqVO) {
        // 校验存在
        validateFbCollectGroupExists(updateReqVO.getId());
        // 更新
        FbCollectGroupDO updateObj = BeanUtils.toBean(updateReqVO, FbCollectGroupDO.class);
        fbCollectGroupMapper.updateById(updateObj);
    }

    @Override
    public void deleteFbCollectGroup(Long id) {
        // 校验存在
        validateFbCollectGroupExists(id);
        // 删除
        fbCollectGroupMapper.deleteById(id);
    }

    @Override
        public void deleteFbCollectGroupListByIds(List<Long> ids) {
        // 删除
        fbCollectGroupMapper.deleteByIds(ids);
        }


    private void validateFbCollectGroupExists(Long id) {
        if (fbCollectGroupMapper.selectById(id) == null) {
            throw exception(FB_COLLECT_GROUP_NOT_EXISTS);
        }
    }

    @Override
    public FbCollectGroupDO getFbCollectGroup(Long id) {
        return fbCollectGroupMapper.selectById(id);
    }

    @Override
    public PageResult<FbCollectGroupDO> getFbCollectGroupPage(FbCollectGroupPageReqVO pageReqVO) {
        return fbCollectGroupMapper.selectPage(pageReqVO);
    }

}