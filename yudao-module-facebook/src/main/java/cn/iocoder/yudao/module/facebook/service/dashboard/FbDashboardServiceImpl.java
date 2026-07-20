package cn.iocoder.yudao.module.facebook.service.dashboard;

import cn.iocoder.yudao.module.facebook.controller.admin.dashboard.vo.FbDashboardHomeRespVO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.agent.FbAiTouchRecordDO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.collectuser.FbCollectUserDO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.collect.FbCollectDO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.dmtask.FbDmTaskDO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.operation.FbOperationTaskDO;
import cn.iocoder.yudao.module.facebook.dal.mysql.agent.FbAiTouchRecordMapper;
import cn.iocoder.yudao.module.facebook.dal.mysql.collectuser.FbCollectUserMapper;
import cn.iocoder.yudao.module.facebook.dal.mysql.collect.FbCollectMapper;
import cn.iocoder.yudao.module.facebook.dal.mysql.dmtask.FbDmTaskMapper;
import cn.iocoder.yudao.module.facebook.dal.mysql.operation.FbOperationTaskMapper;
import com.baomidou.mybatisplus.core.conditions.query.LambdaQueryWrapper;
import jakarta.annotation.Resource;
import org.springframework.stereotype.Service;

import java.time.LocalDate;
import java.time.LocalDateTime;
import java.util.ArrayList;
import java.util.Comparator;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.function.ToLongFunction;
import java.util.stream.Collectors;

@Service
public class FbDashboardServiceImpl implements FbDashboardService {

    @Resource
    private FbCollectUserMapper collectUserMapper;
    @Resource
    private FbCollectMapper collectMapper;
    @Resource
    private FbAiTouchRecordMapper touchRecordMapper;
    @Resource
    private FbOperationTaskMapper operationTaskMapper;
    @Resource
    private FbDmTaskMapper dmTaskMapper;

    @Override
    public FbDashboardHomeRespVO getHome() {
        LocalDate today = LocalDate.now();
        LocalDateTime todayStart = today.atStartOfDay();
        LocalDateTime tomorrowStart = today.plusDays(1).atStartOfDay();

        List<FbCollectUserDO> todayLeads = collectUserMapper.selectList(new LambdaQueryWrapper<FbCollectUserDO>()
                .ge(FbCollectUserDO::getCreateTime, todayStart)
                .lt(FbCollectUserDO::getCreateTime, tomorrowStart));
        List<Long> aiCollectTaskIds = getAiCollectTaskIds();
        List<FbCollectUserDO> todayAiLeads = aiCollectTaskIds.isEmpty()
                ? List.of()
                : collectUserMapper.selectList(new LambdaQueryWrapper<FbCollectUserDO>()
                        .in(FbCollectUserDO::getTaskId, aiCollectTaskIds)
                        .ge(FbCollectUserDO::getCreateTime, todayStart)
                        .lt(FbCollectUserDO::getCreateTime, tomorrowStart));
        long analyzedCount = countAnalyzed(todayStart, tomorrowStart);
        long generatedTouchCount = countGeneratedTouch(todayStart, tomorrowStart);
        long touchedCount = countTouched(todayStart, tomorrowStart);

        FbDashboardHomeRespVO response = new FbDashboardHomeRespVO();
        response.setAiResult(FbDashboardHomeRespVO.AiResult.builder()
                .autoCollectedLeadCount((long) todayAiLeads.size())
                .autoAnalyzedCustomerCount(analyzedCount)
                .generatedInteractionSuggestionCount(generatedTouchCount)
                .autoTouchedCount(touchedCount)
                .build());
        response.setSocialCollection(buildSocialSummary(todayLeads.stream()
                .map(FbCollectUserDO::getFromResource)
                .collect(Collectors.toList()), value -> 1L));
        response.setSocialOperation(buildOperationSummary(todayStart, tomorrowStart));
        return response;
    }

    private FbDashboardHomeRespVO.SocialSummary buildSocialSummary(List<String> types,
                                                                     ToLongFunction<String> valueFunction) {
        Map<String, Long> grouped = types.stream()
                .map(this::normalizeCollectionType)
                .collect(Collectors.groupingBy(type -> type, LinkedHashMap::new,
                        Collectors.summingLong(valueFunction)));
        return FbDashboardHomeRespVO.SocialSummary.builder()
                .total(grouped.values().stream().mapToLong(Long::longValue).sum())
                .items(toItems(grouped))
                .build();
    }

    private FbDashboardHomeRespVO.SocialSummary buildOperationSummary(LocalDateTime start,
                                                                        LocalDateTime end) {
        Map<String, Long> grouped = new LinkedHashMap<>();
        List<FbOperationTaskDO> tasks = operationTaskMapper.selectList(new LambdaQueryWrapper<FbOperationTaskDO>()
                .in(FbOperationTaskDO::getStatus, 1, 2)
                .and(wrapper -> wrapper
                        .between(FbOperationTaskDO::getCreateTime, start, end.minusNanos(1))
                        .or().between(FbOperationTaskDO::getStartTime, start, end.minusNanos(1))
                        .or().between(FbOperationTaskDO::getEndTime, start, end.minusNanos(1))
                        .or().between(FbOperationTaskDO::getUpdateTime, start, end.minusNanos(1))));
        tasks.forEach(task -> addCount(grouped, operationTypeLabel(task.getTaskType()), task.getActualCount()));

        List<FbDmTaskDO> dmTasks = dmTaskMapper.selectList(new LambdaQueryWrapper<FbDmTaskDO>()
                .in(FbDmTaskDO::getStatus, 1, 2)
                .and(wrapper -> wrapper
                        .between(FbDmTaskDO::getCreateTime, start, end.minusNanos(1))
                        .or().between(FbDmTaskDO::getStartTime, start, end.minusNanos(1))
                        .or().between(FbDmTaskDO::getEndTime, start, end.minusNanos(1))
                        .or().between(FbDmTaskDO::getUpdateTime, start, end.minusNanos(1))));
        dmTasks.forEach(task -> addCount(grouped, "私信", task.getCompletedCount()));

        return FbDashboardHomeRespVO.SocialSummary.builder()
                .total(grouped.values().stream().mapToLong(Long::longValue).sum())
                .items(toItems(grouped))
                .build();
    }

    private void addCount(Map<String, Long> grouped, String type, Number count) {
        long value = count == null ? 0 : Math.max(0, count.longValue());
        if (value > 0) {
            grouped.merge(type, value, Long::sum);
        }
    }

    private List<FbDashboardHomeRespVO.SocialItem> toItems(Map<String, Long> grouped) {
        return grouped.entrySet().stream()
                .sorted(Map.Entry.<String, Long>comparingByValue(Comparator.reverseOrder())
                        .thenComparing(Map.Entry::getKey))
                .map(entry -> FbDashboardHomeRespVO.SocialItem.builder()
                        .type(entry.getKey())
                        .count(entry.getValue())
                        .build())
                .collect(Collectors.toCollection(ArrayList::new));
    }

    private String normalizeCollectionType(String source) {
        if (source == null || source.isBlank()) {
            return "其他采集";
        }
        String value = source.trim().toLowerCase();
        if (value.contains("comment") || value.contains("评论")) {
            return "评论采集";
        }
        if (value.contains("group") || value.contains("群")) {
            return "群组采集";
        }
        if (value.contains("post") || value.contains("帖子")) {
            return "帖子采集";
        }
        if (value.contains("video") || value.contains("视频")) {
            return "视频采集";
        }
        if (value.contains("page") || value.contains("主页")) {
            return "主页采集";
        }
        return source.trim();
    }

    private String operationTypeLabel(Integer taskType) {
        if (taskType == null) {
            return "其他运营";
        }
        return switch (taskType) {
            case 9 -> "加群";
            case 10 -> "转帖";
            case 12, 13 -> "发帖";
            case 14, 11 -> "私信";
            case 15 -> "评论";
            case 16 -> "好友/关注";
            default -> "其他运营";
        };
    }

    private long countTouched(LocalDateTime start, LocalDateTime end) {
        return touchRecordMapper.selectCount(new LambdaQueryWrapper<FbAiTouchRecordDO>()
                .eq(FbAiTouchRecordDO::getStatus, 2)
                .ge(FbAiTouchRecordDO::getSentTime, start)
                .lt(FbAiTouchRecordDO::getSentTime, end));
    }

    private long countAnalyzed(LocalDateTime start, LocalDateTime end) {
        return collectUserMapper.selectCount(new LambdaQueryWrapper<FbCollectUserDO>()
                .ge(FbCollectUserDO::getLastAiAnalyzeTime, start)
                .lt(FbCollectUserDO::getLastAiAnalyzeTime, end));
    }

    private long countGeneratedTouch(LocalDateTime start, LocalDateTime end) {
        return touchRecordMapper.selectCount(new LambdaQueryWrapper<FbAiTouchRecordDO>()
                .ge(FbAiTouchRecordDO::getCreateTime, start)
                .lt(FbAiTouchRecordDO::getCreateTime, end));
    }

    /**
     * AI Agent 创建的采集任务统一以 AI 开头，普通社媒采集任务不参与 AI 成果统计。
     */
    private List<Long> getAiCollectTaskIds() {
        return collectMapper.selectList(new LambdaQueryWrapper<FbCollectDO>()
                        .select(FbCollectDO::getId)
                        .like(FbCollectDO::getRemark, "AI"))
                .stream()
                .map(FbCollectDO::getId)
                .filter(id -> id != null)
                .toList();
    }
}
