package cn.iocoder.yudao.module.facebook.service.collectdetail;

import cn.iocoder.yudao.module.facebook.dal.dataobject.collectdetail.FbCollectDetailDO;
import cn.iocoder.yudao.module.facebook.dal.mysql.collectdetail.FbCollectDetailMapper;
import com.baomidou.mybatisplus.core.conditions.query.LambdaQueryWrapper;
import org.springframework.stereotype.Service;
import org.springframework.validation.annotation.Validated;

import javax.annotation.Resource;
import java.util.List;

/**
 * FB采集任务明细 Service 实现类
 *
 * @author jacky
 */
@Service
@Validated
public class FbCollectDetailServiceImpl implements FbCollectDetailService {

    @Resource
    private FbCollectDetailMapper fbCollectDetailMapper;

    @Override
    public List<FbCollectDetailDO> getPendingDetailsByAccount(String fbAccount) {
        return fbCollectDetailMapper.selectList(
            new LambdaQueryWrapper<FbCollectDetailDO>()
                .eq(FbCollectDetailDO::getFbAccount, fbAccount)
                .eq(FbCollectDetailDO::getStatus, 0) // 待执行
                .orderByAsc(FbCollectDetailDO::getId)
                .last("LIMIT 1")
        );
    }

    @Override
    public List<FbCollectDetailDO> getDetailListByTaskId(Long taskId) {
        return fbCollectDetailMapper.selectList(
            new LambdaQueryWrapper<FbCollectDetailDO>()
                .eq(FbCollectDetailDO::getTaskId, taskId)
                .orderByAsc(FbCollectDetailDO::getId)
        );
    }
}
