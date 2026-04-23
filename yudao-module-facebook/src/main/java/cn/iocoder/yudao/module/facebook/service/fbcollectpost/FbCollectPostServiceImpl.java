package cn.iocoder.yudao.module.facebook.service.fbcollectpost;

import cn.hutool.core.collection.CollUtil;
import org.springframework.stereotype.Service;
import javax.annotation.Resource;
import org.springframework.validation.annotation.Validated;
import org.springframework.transaction.annotation.Transactional;

import java.util.*;
import cn.iocoder.yudao.module.facebook.controller.admin.fbcollectpost.vo.*;
import cn.iocoder.yudao.module.facebook.dal.dataobject.fbcollectpost.FbCollectPostDO;
import cn.iocoder.yudao.framework.common.pojo.PageResult;
import cn.iocoder.yudao.framework.common.pojo.PageParam;
import cn.iocoder.yudao.framework.common.util.object.BeanUtils;

import cn.iocoder.yudao.module.facebook.dal.mysql.fbcollectpost.FbCollectPostMapper;

import static cn.iocoder.yudao.framework.common.exception.util.ServiceExceptionUtil.exception;
import static cn.iocoder.yudao.framework.common.util.collection.CollectionUtils.convertList;
import static cn.iocoder.yudao.framework.common.util.collection.CollectionUtils.diffList;
import static cn.iocoder.yudao.module.facebook.enums.ErrorCodeConstants.*;

/**
 * FB帖子采集结果 Service 实现类
 *
 * @author jacky
 */
@Service
@Validated
public class FbCollectPostServiceImpl implements FbCollectPostService {

    @Resource
    private FbCollectPostMapper fbCollectPostMapper;

    @Override
    public Long createFbCollectPost(FbCollectPostSaveReqVO createReqVO) {
        // 插入
        FbCollectPostDO fbCollectPost = BeanUtils.toBean(createReqVO, FbCollectPostDO.class);
        fbCollectPostMapper.insert(fbCollectPost);

        // 返回
        return fbCollectPost.getId();
    }

    @Override
    public void updateFbCollectPost(FbCollectPostSaveReqVO updateReqVO) {
        // 校验存在
        validateFbCollectPostExists(updateReqVO.getId());
        // 更新
        FbCollectPostDO updateObj = BeanUtils.toBean(updateReqVO, FbCollectPostDO.class);
        fbCollectPostMapper.updateById(updateObj);
    }

    @Override
    public void deleteFbCollectPost(Long id) {
        // 校验存在
        validateFbCollectPostExists(id);
        // 删除
        fbCollectPostMapper.deleteById(id);
    }

    @Override
        public void deleteFbCollectPostListByIds(List<Long> ids) {
        // 删除
        fbCollectPostMapper.deleteByIds(ids);
        }


    private void validateFbCollectPostExists(Long id) {
        if (fbCollectPostMapper.selectById(id) == null) {
            throw exception(FB_COLLECT_POST_NOT_EXISTS);
        }
    }

    @Override
    public FbCollectPostDO getFbCollectPost(Long id) {
        return fbCollectPostMapper.selectById(id);
    }

    @Override
    public PageResult<FbCollectPostDO> getFbCollectPostPage(FbCollectPostPageReqVO pageReqVO) {
        return fbCollectPostMapper.selectPage(pageReqVO);
    }

}