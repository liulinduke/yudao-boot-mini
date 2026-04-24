package cn.iocoder.yudao.module.facebook.service.operation;

import cn.iocoder.yudao.framework.common.pojo.PageResult;
import cn.iocoder.yudao.framework.common.util.object.BeanUtils;
import cn.iocoder.yudao.module.facebook.controller.admin.operation.vo.FbOperationAddGroupResultPageReqVO;
import cn.iocoder.yudao.module.facebook.controller.admin.operation.vo.FbOperationAddGroupResultRespVO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.operation.FbOperationAddGroupResultDO;
import cn.iocoder.yudao.module.facebook.dal.mysql.operation.FbOperationAddGroupResultMapper;
import cn.iocoder.yudao.framework.mybatis.core.query.LambdaQueryWrapperX;
import org.springframework.stereotype.Service;
import org.springframework.validation.annotation.Validated;

import javax.annotation.Resource;
import java.util.List;

/**
 * 链接加组结果 Service 实现类
 *
 * @author 芋道源码
 */
@Service
@Validated
public class FbOperationAddGroupResultServiceImpl implements FbOperationAddGroupResultService {

    @Resource
    private FbOperationAddGroupResultMapper addGroupResultMapper;

    @Override
    public PageResult<FbOperationAddGroupResultRespVO> getAddGroupResultPage(FbOperationAddGroupResultPageReqVO pageReqVO) {
        PageResult<FbOperationAddGroupResultDO> pageResult = addGroupResultMapper.selectPage(pageReqVO,
                new LambdaQueryWrapperX<FbOperationAddGroupResultDO>()
                        .eqIfPresent(FbOperationAddGroupResultDO::getTaskId, pageReqVO.getTaskId())
                        .eqIfPresent(FbOperationAddGroupResultDO::getDetailId, pageReqVO.getDetailId())
                        .eqIfPresent(FbOperationAddGroupResultDO::getAccountId, pageReqVO.getAccountId())
                        .eqIfPresent(FbOperationAddGroupResultDO::getJoinStatus, pageReqVO.getJoinStatus())
                        .likeIfPresent(FbOperationAddGroupResultDO::getGroupId, pageReqVO.getGroupId())
                        .likeIfPresent(FbOperationAddGroupResultDO::getGroupName, pageReqVO.getGroupName())
                        .orderByDesc(FbOperationAddGroupResultDO::getId));
        return BeanUtils.toBean(pageResult, FbOperationAddGroupResultRespVO.class);
    }

    @Override
    public List<FbOperationAddGroupResultRespVO> getAddGroupResultByTaskId(Long taskId) {
        List<FbOperationAddGroupResultDO> list = addGroupResultMapper.selectListByTaskId(taskId);
        return BeanUtils.toBean(list, FbOperationAddGroupResultRespVO.class);
    }

}
