package cn.iocoder.yudao.module.facebook.service.collect;

import cn.hutool.core.collection.CollUtil;
import org.springframework.stereotype.Service;
import javax.annotation.Resource;
import org.springframework.validation.annotation.Validated;
import org.springframework.transaction.annotation.Transactional;

import java.util.*;
import cn.iocoder.yudao.module.facebook.controller.admin.collect.vo.*;
import cn.iocoder.yudao.module.facebook.dal.dataobject.collect.FbCollectDO;
import cn.iocoder.yudao.framework.common.pojo.PageResult;
import cn.iocoder.yudao.framework.common.pojo.PageParam;
import cn.iocoder.yudao.framework.common.util.object.BeanUtils;

import cn.iocoder.yudao.module.facebook.dal.mysql.collect.FbCollectMapper;

import static cn.iocoder.yudao.framework.common.exception.util.ServiceExceptionUtil.exception;
import static cn.iocoder.yudao.framework.common.util.collection.CollectionUtils.convertList;
import static cn.iocoder.yudao.framework.common.util.collection.CollectionUtils.diffList;
import static cn.iocoder.yudao.module.facebook.enums.ErrorCodeConstants.*;

/**
 * FB采集任务 Service 实现类
 *
 * @author jacky
 */
@Service
@Validated
public class FbCollectServiceImpl implements FbCollectService {

    @Resource
    private FbCollectMapper fbCollectMapper;

    @Override
    public Long createFbCollect(FbCollectSaveReqVO createReqVO) {
        // 插入
        FbCollectDO fbCollect = BeanUtils.toBean(createReqVO, FbCollectDO.class);
        fbCollectMapper.insert(fbCollect);

        // 返回
        return fbCollect.getId();
    }

    @Override
    public void updateFbCollect(FbCollectSaveReqVO updateReqVO) {
        // 校验存在
        validateFbCollectExists(updateReqVO.getId());
        // 更新
        FbCollectDO updateObj = BeanUtils.toBean(updateReqVO, FbCollectDO.class);
        fbCollectMapper.updateById(updateObj);
    }

    @Override
    public void deleteFbCollect(Long id) {
        // 校验存在
        validateFbCollectExists(id);
        // 删除
        fbCollectMapper.deleteById(id);
    }

    @Override
        public void deleteFbCollectListByIds(List<Long> ids) {
        // 删除
        fbCollectMapper.deleteByIds(ids);
        }


    private void validateFbCollectExists(Long id) {
        if (fbCollectMapper.selectById(id) == null) {
            throw exception(FB_COLLECT_NOT_EXISTS);
        }
    }

    @Override
    public FbCollectDO getFbCollect(Long id) {
        return fbCollectMapper.selectById(id);
    }

    @Override
    public PageResult<FbCollectDO> getFbCollectPage(FbCollectPageReqVO pageReqVO) {
        return fbCollectMapper.selectPage(pageReqVO);
    }

}