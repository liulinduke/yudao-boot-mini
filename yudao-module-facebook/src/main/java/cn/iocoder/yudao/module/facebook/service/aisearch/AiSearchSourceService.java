package cn.iocoder.yudao.module.facebook.service.aisearch;
import cn.iocoder.yudao.module.facebook.controller.admin.aisearch.vo.*;
import java.util.List;
public interface AiSearchSourceService { Long create(AiSearchSourceSaveReqVO req); int createBatch(List<AiSearchSourceSaveReqVO> requests); void update(AiSearchSourceSaveReqVO req); void updateStatus(Long id, String status); List<AiSearchSourceRespVO> list(String status); }
