package cn.iocoder.yudao.module.facebook.service.dashboard;

import cn.hutool.core.util.StrUtil;
import cn.iocoder.yudao.module.facebook.controller.admin.dashboard.vo.FbDashboardHomeRespVO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.agent.FbAiAgentRunLogDO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.agent.FbAiTouchRecordDO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.collectuser.FbCollectUserDO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.message.FbMessageConversationDO;
import cn.iocoder.yudao.module.facebook.dal.mysql.agent.FbAiAgentRunLogMapper;
import cn.iocoder.yudao.module.facebook.dal.mysql.agent.FbAiTouchRecordMapper;
import cn.iocoder.yudao.module.facebook.dal.mysql.collectuser.FbCollectUserMapper;
import cn.iocoder.yudao.module.facebook.dal.mysql.message.FbMessageConversationMapper;
import com.baomidou.mybatisplus.core.conditions.query.LambdaQueryWrapper;
import jakarta.annotation.Resource;
import org.springframework.stereotype.Service;

import java.time.LocalDateTime;
import java.time.LocalTime;
import java.util.List;

@Service
public class FbDashboardServiceImpl implements FbDashboardService {

    private static final String ROUTE_RESOURCE = "/facebook/resource";
    private static final String ROUTE_AGENT = "/facebook/agent";
    private static final String ROUTE_MESSAGE = "/facebook/message";

    @Resource
    private FbCollectUserMapper collectUserMapper;
    @Resource
    private FbAiTouchRecordMapper touchRecordMapper;
    @Resource
    private FbMessageConversationMapper conversationMapper;
    @Resource
    private FbAiAgentRunLogMapper runLogMapper;

    @Override
    public FbDashboardHomeRespVO getHome() {
        LocalDateTime now = LocalDateTime.now();
        LocalDateTime todayStart = now.toLocalDate().atStartOfDay();
        LocalDateTime tomorrowStart = todayStart.plusDays(1);
        LocalDateTime yesterdayStart = todayStart.minusDays(1);

        long todayLeadCount = countNewLeads(todayStart, tomorrowStart);
        long yesterdayLeadCount = countNewLeads(yesterdayStart, todayStart);
        long todayHighIntentCount = countHighIntent(todayStart, tomorrowStart);
        long yesterdayHighIntentCount = countHighIntent(yesterdayStart, todayStart);
        long recommendedCount = countRecommendedLeads();
        long yesterdayRecommendedCount = countRecommendedLeads(yesterdayStart, todayStart);
        long todayTouchedCount = countTouched(todayStart, tomorrowStart);
        long yesterdayTouchedCount = countTouched(yesterdayStart, todayStart);

        List<FbDashboardHomeRespVO.RecommendedLead> recommendedLeads = collectUserMapper.selectList(
                new LambdaQueryWrapper<FbCollectUserDO>()
                        .eq(FbCollectUserDO::getIntentLevel, "high")
                        .and(wrapper -> wrapper.isNull(FbCollectUserDO::getTouchStatus)
                                .or().eq(FbCollectUserDO::getTouchStatus, "not_touched"))
                        .orderByDesc(FbCollectUserDO::getProductRelevanceScore)
                        .orderByDesc(FbCollectUserDO::getLastAiAnalyzeTime)
                        .orderByDesc(FbCollectUserDO::getId)
                        .last("LIMIT 6")
        ).stream().map(this::buildRecommendedLead).toList();

        long analyzedCount = countAnalyzed(todayStart, tomorrowStart);
        long generatedTouchCount = countGeneratedTouch(todayStart, tomorrowStart);
        long pendingHighIntentCount = countPendingHighIntent();
        long unreadReplyCount = countUnreadReplies();
        long errorTaskCount = countErrorTasks(todayStart, tomorrowStart);

        FbDashboardHomeRespVO response = new FbDashboardHomeRespVO();
        response.setSummary(FbDashboardHomeRespVO.Summary.builder()
                .headline(String.format("今天 AI 新增 %d 条线索，筛出 %d 位高意向客户，推荐优先联系 %d 人",
                        todayLeadCount, todayHighIntentCount, recommendedCount))
                .subline(String.format("系统已自动完成 %d 条分析与 %d 次触达动作，重点客户已集中到推荐列表。", analyzedCount, todayTouchedCount))
                .leadCount(todayLeadCount)
                .highIntentCount(todayHighIntentCount)
                .recommendedCount(recommendedCount)
                .build());
        response.setMetrics(List.of(
                buildMetric("newLeads", "新增线索", todayLeadCount, yesterdayLeadCount, ROUTE_RESOURCE),
                buildMetric("highIntent", "高意向客户", todayHighIntentCount, yesterdayHighIntentCount, ROUTE_AGENT),
                buildMetric("recommended", "推荐联系", recommendedCount, yesterdayRecommendedCount, ROUTE_AGENT),
                buildMetric("touched", "已触达", todayTouchedCount, yesterdayTouchedCount, ROUTE_AGENT)
        ));
        response.setRecommendedLeads(recommendedLeads);
        response.setAutomationAndTodos(FbDashboardHomeRespVO.AutomationAndTodo.builder()
                .automationItems(List.of(
                        FbDashboardHomeRespVO.AutomationItem.builder()
                                .title("自动采集线索")
                                .value(todayLeadCount)
                                .description("今天首次进入系统的潜客数量")
                                .build(),
                        FbDashboardHomeRespVO.AutomationItem.builder()
                                .title("自动分析客户")
                                .value(analyzedCount)
                                .description("今天完成 AI 判断与摘要生成的客户数量")
                                .build(),
                        FbDashboardHomeRespVO.AutomationItem.builder()
                                .title("生成互动建议")
                                .value(generatedTouchCount)
                                .description("今天生成的私信或互动建议数量")
                                .build(),
                        FbDashboardHomeRespVO.AutomationItem.builder()
                                .title("自动完成触达")
                                .value(todayTouchedCount)
                                .description("今天成功执行的触达动作数量")
                                .build()
                ))
                .todoItems(List.of(
                        FbDashboardHomeRespVO.TodoItem.builder()
                                .title("推荐客户待联系")
                                .count(recommendedCount)
                                .level("high")
                                .routePath(ROUTE_AGENT)
                                .build(),
                        FbDashboardHomeRespVO.TodoItem.builder()
                                .title("高意向客户未触达")
                                .count(pendingHighIntentCount)
                                .level("high")
                                .routePath(ROUTE_RESOURCE)
                                .build(),
                        FbDashboardHomeRespVO.TodoItem.builder()
                                .title("未读消息待回复")
                                .count(unreadReplyCount)
                                .level("medium")
                                .routePath(ROUTE_MESSAGE)
                                .build(),
                        FbDashboardHomeRespVO.TodoItem.builder()
                                .title("任务执行异常")
                                .count(errorTaskCount)
                                .level("warning")
                                .routePath(ROUTE_AGENT)
                                .build()
                ))
                .build());
        return response;
    }

    private FbDashboardHomeRespVO.MetricCard buildMetric(String key, String title, long todayValue, long yesterdayValue, String routePath) {
        long delta = todayValue - yesterdayValue;
        String deltaLabel = delta == 0 ? "较昨日持平" : delta > 0 ? "较昨日 +" + delta : "较昨日 " + delta;
        return FbDashboardHomeRespVO.MetricCard.builder()
                .key(key)
                .title(title)
                .value(todayValue)
                .delta(delta)
                .deltaLabel(deltaLabel)
                .routePath(routePath)
                .build();
    }

    private FbDashboardHomeRespVO.RecommendedLead buildRecommendedLead(FbCollectUserDO item) {
        String reason = StrUtil.blankToDefault(item.getIntentReason(),
                StrUtil.blankToDefault(item.getAiSummary(), "近期互动活跃，且与产品高度相关"));
        String source = StrUtil.blankToDefault(item.getFromResource(),
                StrUtil.blankToDefault(item.getCountry(), "系统线索池"));
        String action = hasDirectContact(item) ? "建议私信" : "建议先评论互动";
        return FbDashboardHomeRespVO.RecommendedLead.builder()
                .id(item.getId())
                .customerName(StrUtil.blankToDefault(item.getUserName(), "未命名客户"))
                .source(source)
                .intentLevel(convertIntentLevel(item.getIntentLevel()))
                .aiReason(reason)
                .recommendedAction(action)
                .targetUrl(item.getUrl())
                .build();
    }

    private boolean hasDirectContact(FbCollectUserDO item) {
        return StrUtil.isNotBlank(item.getWhatsapp())
                || StrUtil.isNotBlank(item.getEmail())
                || StrUtil.isNotBlank(item.getPhonenumber())
                || StrUtil.isNotBlank(item.getPhonenumber2());
    }

    private String convertIntentLevel(String level) {
        if ("high".equals(level)) {
            return "高意向";
        }
        if ("medium".equals(level)) {
            return "中意向";
        }
        if ("low".equals(level)) {
            return "低意向";
        }
        return "待判断";
    }

    private long countNewLeads(LocalDateTime start, LocalDateTime end) {
        return collectUserMapper.selectCount(new LambdaQueryWrapper<FbCollectUserDO>()
                .ge(FbCollectUserDO::getCreateTime, start)
                .lt(FbCollectUserDO::getCreateTime, end));
    }

    private long countHighIntent(LocalDateTime start, LocalDateTime end) {
        return collectUserMapper.selectCount(new LambdaQueryWrapper<FbCollectUserDO>()
                .eq(FbCollectUserDO::getIntentLevel, "high")
                .ge(FbCollectUserDO::getCreateTime, start)
                .lt(FbCollectUserDO::getCreateTime, end));
    }

    private long countRecommendedLeads() {
        return collectUserMapper.selectCount(new LambdaQueryWrapper<FbCollectUserDO>()
                .eq(FbCollectUserDO::getIntentLevel, "high")
                .and(wrapper -> wrapper.isNull(FbCollectUserDO::getTouchStatus)
                        .or().eq(FbCollectUserDO::getTouchStatus, "not_touched")));
    }

    private long countRecommendedLeads(LocalDateTime start, LocalDateTime end) {
        return collectUserMapper.selectCount(new LambdaQueryWrapper<FbCollectUserDO>()
                .eq(FbCollectUserDO::getIntentLevel, "high")
                .and(wrapper -> wrapper.isNull(FbCollectUserDO::getTouchStatus)
                        .or().eq(FbCollectUserDO::getTouchStatus, "not_touched"))
                .ge(FbCollectUserDO::getCreateTime, start)
                .lt(FbCollectUserDO::getCreateTime, end));
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

    private long countPendingHighIntent() {
        return collectUserMapper.selectCount(new LambdaQueryWrapper<FbCollectUserDO>()
                .eq(FbCollectUserDO::getIntentLevel, "high")
                .and(wrapper -> wrapper.isNull(FbCollectUserDO::getTouchStatus)
                        .or().eq(FbCollectUserDO::getTouchStatus, "not_touched")));
    }

    private long countUnreadReplies() {
        return conversationMapper.selectList(new LambdaQueryWrapper<FbMessageConversationDO>()
                        .gt(FbMessageConversationDO::getUnreadCount, 0))
                .stream()
                .map(FbMessageConversationDO::getUnreadCount)
                .filter(count -> count != null && count > 0)
                .mapToLong(Integer::longValue)
                .sum();
    }

    private long countErrorTasks(LocalDateTime start, LocalDateTime end) {
        return runLogMapper.selectCount(new LambdaQueryWrapper<FbAiAgentRunLogDO>()
                .eq(FbAiAgentRunLogDO::getLogLevel, "error")
                .ge(FbAiAgentRunLogDO::getCreateTime, start)
                .lt(FbAiAgentRunLogDO::getCreateTime, end));
    }
}
