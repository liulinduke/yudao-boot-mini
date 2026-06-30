package cn.iocoder.yudao.module.facebook.service.collectdetail;

import cn.iocoder.yudao.module.facebook.dal.dataobject.collectdetail.FbCollectDetailDO;
import cn.iocoder.yudao.module.facebook.controller.admin.collectdetail.vo.FbCollectPendingDetailRespVO;

import java.util.List;

/**
 * FB采集任务明细 Service 接口
 *
 * @author jacky
 */
public interface FbCollectDetailService {

    /**
     * 查询账号的待执行明细(按ID升序,返回第一个)
     *
     * @param fbAccount FB账号
     * @return 待执行明细列表(最多1个)
     */
    List<FbCollectDetailDO> getPendingDetailsByAccount(String fbAccount);

    /**
     * 根据任务ID查询明细列表
     *
     * @param taskId 任务ID
     * @return 明细列表
     */
    List<FbCollectDetailDO> getDetailListByTaskId(Long taskId);

    /**
     * WPF 拉取待执行采集明细，并将返回的明细锁定为采集中。
     *
     * @param limit 拉取数量
     * @return 可执行明细列表
     */
    List<FbCollectPendingDetailRespVO> claimPendingDetails(Integer limit);
}
