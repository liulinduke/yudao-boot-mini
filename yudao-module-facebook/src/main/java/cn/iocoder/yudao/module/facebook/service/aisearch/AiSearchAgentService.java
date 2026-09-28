package cn.iocoder.yudao.module.facebook.service.aisearch;

import cn.hutool.json.JSONUtil;
import cn.iocoder.yudao.module.ai.dal.dataobject.model.AiModelDO;
import cn.iocoder.yudao.module.ai.enums.model.AiModelTypeEnum;
import cn.iocoder.yudao.module.ai.framework.ai.config.YudaoAiProperties;
import cn.iocoder.yudao.module.ai.framework.ai.core.webserch.AiWebSearchResponse;
import cn.iocoder.yudao.module.ai.service.model.AiModelService;
import cn.iocoder.yudao.module.facebook.dal.dataobject.aisearch.AiSearchQueryDO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.aisearch.AiSearchFrontierDO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.aisearch.AiSearchMemoryDO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.aisearch.AiSearchSourceDO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.aisearch.AiSearchTaskDO;
import cn.iocoder.yudao.module.facebook.dal.mysql.aisearch.AiSearchQueryMapper;
import cn.iocoder.yudao.module.facebook.dal.mysql.aisearch.AiSearchFrontierMapper;
import cn.iocoder.yudao.module.facebook.dal.mysql.aisearch.AiSearchMemoryMapper;
import cn.iocoder.yudao.module.facebook.dal.mysql.aisearch.AiSearchSourceMapper;
import com.baomidou.mybatisplus.core.toolkit.Wrappers;
import jakarta.annotation.Resource;
import lombok.Data;
import org.springframework.ai.chat.client.ChatClient;
import org.springframework.ai.tool.annotation.Tool;
import org.springframework.ai.tool.annotation.ToolParam;
import org.springframework.stereotype.Service;

import java.net.URI;
import java.util.ArrayList;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.concurrent.atomic.AtomicInteger;

@Service
public class AiSearchAgentService {

    @Resource
    private AiModelService aiModelService;
    @Resource
    private AiSearchQueryMapper queryMapper;
    @Resource
    private AiSearchFrontierMapper frontierMapper;
    @Resource
    private AiSearchMemoryMapper memoryMapper;
    @Resource
    private AiSearchSourceMapper sourceMapper;
    @Resource
    private YudaoAiProperties aiProperties;

    public AgentTurn search(AiSearchTaskDO task, int round, int remainingToolCalls,
                            int qualifiedAddedThisRun, int remainingQualifiedTarget) {
        assertSearchReady();
        AiModelDO model = aiModelService.getRequiredDefaultModel(AiModelTypeEnum.CHAT.getType());
        SearchTool searchTool = new SearchTool(Math.max(1, remainingToolCalls), aiModelService);
        String context = buildContext(task, round, qualifiedAddedThisRun, remainingQualifiedTarget);
        String decision = ChatClient.create(aiModelService.getChatModel(model.getId()))
                .prompt()
                .system("你是全网企业获客 Search Agent。根据任务目标、用户确认的搜索快照、已搜索行为、搜索效果和渠道效果，决定下一步搜索方向。重点记忆搜过的 query、关键词、产品词、角色、场景、语言和来源，不需要企业名单；企业去重由后端数据库负责。可以调整关键词、客户角色、采购词、语言、应用/场景、渠道来源；来源仅为优先提示，不是搜索白名单。选择某个目录/渠道时，在 query 中使用 site:域名便于归因；无结果或低产出时必须允许普通全网查询。不得输出或编造企业、网址或网页事实。必须通过 web_search 工具获取网页结果；每次决策最多调用一次工具。即使认为应该 STOP，也只能返回建议，不能替程序结束运行。若需要继续，请调用 web_search(query,count) 并给出新的搜索组合；完成后最终回答仅输出 CONTINUE、CHANGE_QUERY、CHANGE_SOURCE、EXPAND 或 STOP 及简短理由。")
                .user(context)
                .tools(searchTool)
                .call()
                .content();
        return new AgentTurn(decision, searchTool.getInvocations());
    }

    public void assertSearchReady() {
        if (!aiProperties.getWebSearch().isResultsVerified()) {
            throw new IllegalStateException("AIHubMix :surfing 搜索结果尚未验证，请先验证真实来源后设置 yudao.ai.web-search.results-verified=true");
        }
    }

    private String buildContext(AiSearchTaskDO task, int round,
                                int qualifiedAddedThisRun, int remainingQualifiedTarget) {
        List<AiSearchQueryDO> history = queryMapper.selectList(Wrappers.lambdaQuery(AiSearchQueryDO.class)
                .eq(AiSearchQueryDO::getTaskId, task.getId())
                .orderByDesc(AiSearchQueryDO::getId).last("LIMIT 20"));
        List<AiSearchSourceDO> sources = sourceMapper.selectList(Wrappers.lambdaQuery(AiSearchSourceDO.class)
                .in(AiSearchSourceDO::getStatus, "ACTIVE", "CANDIDATE")
                .orderByDesc(AiSearchSourceDO::getYieldScore)
                .orderByDesc(AiSearchSourceDO::getPriority).last("LIMIT 50"));
        List<AiSearchFrontierDO> frontier = frontierMapper.selectList(Wrappers.lambdaQuery(AiSearchFrontierDO.class)
                .eq(AiSearchFrontierDO::getTaskId, task.getId()).orderByDesc(AiSearchFrontierDO::getLastRoundNo).last("LIMIT 100"));
        List<AiSearchMemoryDO> memories = memoryMapper.selectList(Wrappers.lambdaQuery(AiSearchMemoryDO.class)
                .eq(AiSearchMemoryDO::getScopeType, "TASK").eq(AiSearchMemoryDO::getScopeId, task.getId())
                .orderByDesc(AiSearchMemoryDO::getScore).last("LIMIT 30"));
        Map<String, Object> context = new LinkedHashMap<>();
        context.put("round", round);
        context.put("qualifiedAddedThisRun", qualifiedAddedThisRun);
        context.put("remainingQualifiedTarget", remainingQualifiedTarget);
        context.put("goal", task.getUserGoal());
        context.put("country", task.getTargetCountry());
        context.put("customerType", task.getCustomerType());
        context.put("companyProduct", task.getCompanyProduct());
        context.put("leadKeywords", task.getLeadKeywords());
        context.put("targetNewQualifiedCompanies", task.getTargetCount());
        context.put("snapshot", task.getSearchSnapshotJson() == null ? Map.of()
                : JSONUtil.parseObj(task.getSearchSnapshotJson()));
        context.put("prioritySources", sources.stream().map(source -> {
            Map<String, Object> item = new LinkedHashMap<>();
            item.put("name", source.getName()); item.put("domain", source.getDomain());
            item.put("type", source.getSourceType()); item.put("status", source.getStatus());
            item.put("qualified", source.getQualifiedCompanyCount()); item.put("duplicates", source.getDuplicateCount());
            item.put("yieldScore", source.getYieldScore());
            return item;
        }).toList());
        context.put("recentSearches", history.stream().map(query -> {
            Map<String, Object> item = new LinkedHashMap<>();
            item.put("query", query.getQuery()); item.put("language", query.getLanguage());
            item.put("sourceId", query.getSourceId()); item.put("results", query.getResultCount());
            item.put("newCompanies", query.getNewCompanyCount()); item.put("qualified", query.getQualifiedCount());
            item.put("duplicates", query.getDuplicateCount()); item.put("dimensions", query.getDimensionsJson());
            return item;
        }).toList());
        context.put("searchFrontier", frontier.stream().map(item -> Map.of(
                "keyword", item.getKeyword() == null ? "" : item.getKeyword(),
                "product", item.getProduct() == null ? "" : item.getProduct(),
                "customerRole", item.getCustomerType() == null ? "" : item.getCustomerType(),
                "language", item.getLanguage() == null ? "" : item.getLanguage(),
                "status", item.getStatus() == null ? "" : item.getStatus(),
                "sourceId", item.getSourceId() == null ? 0 : item.getSourceId())).toList());
        context.put("searchMemory", memories.stream().map(item -> Map.of(
                "key", item.getMemoryKey(), "value", item.getMemoryValue() == null ? "" : item.getMemoryValue(),
                "score", item.getScore() == null ? 0 : item.getScore(),
                "uses", item.getUsageCount() == null ? 0 : item.getUsageCount())).toList());
        return JSONUtil.toJsonStr(context);
    }

    @Data
    public static class AgentTurn {
        private final String decision;
        private final List<SearchInvocation> invocations;
    }

    @Data
    public static class SearchInvocation {
        private final String query;
        private final List<AiWebSearchResponse.WebPage> pages;
    }

    public static final class SearchTool {
        private final int maxCalls;
        private final AiModelService aiModelService;
        private final AtomicInteger calls = new AtomicInteger();
        private final List<SearchInvocation> invocations = new ArrayList<>();

        public SearchTool(int maxCalls, AiModelService aiModelService) {
            this.maxCalls = maxCalls;
            this.aiModelService = aiModelService;
        }

        @Tool(name = "web_search", description = "执行真实互联网网页搜索。只返回搜索后端返回的真实网页标题、URL、摘要和来源，不生成企业或事实。")
        public String webSearch(
                @ToolParam(description = "当前搜索 query，可包含关键词、语言和可选来源域名") String query,
                @ToolParam(description = "最多返回的网页数量，1到50") Integer count) {
            int callNo = calls.incrementAndGet();
            if (callNo > maxCalls) {
                return "{\"error\":\"tool_budget_exhausted\",\"results\":[]}";
            }
            if (query == null || query.isBlank()) {
                throw new IllegalArgumentException("web_search query 不能为空");
            }
            int pageCount = Math.min(Math.max(count == null ? 10 : count, 1), 50);
            AiWebSearchResponse response = aiModelService.webSearch(query, pageCount);
            List<AiWebSearchResponse.WebPage> pages = response == null || response.getLists() == null
                    ? List.of() : response.getLists().stream().filter(SearchTool::isValidPage).toList();
            pages.forEach(page -> {
                if (page.getSource() == null || page.getSource().isBlank()) {
                    page.setSource(sourceFromUrl(page.getUrl()));
                }
            });
            invocations.add(new SearchInvocation(query, pages));
            return JSONUtil.toJsonStr(pages.stream().map(page -> {
                Map<String, Object> result = new LinkedHashMap<>();
                result.put("title", page.getTitle() == null
                        ? (page.getName() == null ? page.getUrl() : page.getName()) : page.getTitle());
                result.put("url", page.getUrl());
                result.put("snippet", page.getSnippet() == null ? "" : page.getSnippet());
                result.put("source", page.getSource() == null ? "" : page.getSource());
                return result;
            }).toList());
        }

        private List<SearchInvocation> getInvocations() {
            return List.copyOf(invocations);
        }

        private static boolean isValidPage(AiWebSearchResponse.WebPage page) {
            if (page == null || page.getUrl() == null) return false;
            try {
                URI uri = URI.create(page.getUrl());
                return ("http".equalsIgnoreCase(uri.getScheme()) || "https".equalsIgnoreCase(uri.getScheme()))
                        && uri.getHost() != null;
            } catch (Exception ignored) {
                return false;
            }
        }

        private static String sourceFromUrl(String url) {
            try {
                return URI.create(url).getHost();
            } catch (Exception ignored) {
                return "";
            }
        }
    }
}
