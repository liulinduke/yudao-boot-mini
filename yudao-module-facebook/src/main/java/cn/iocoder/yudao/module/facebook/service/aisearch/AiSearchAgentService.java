package cn.iocoder.yudao.module.facebook.service.aisearch;

import cn.hutool.json.JSONUtil;
import cn.iocoder.yudao.module.ai.dal.dataobject.model.AiModelDO;
import cn.iocoder.yudao.module.ai.dal.dataobject.workflow.AiWorkflowDO;
import cn.iocoder.yudao.module.ai.enums.model.AiPlatformEnum;
import cn.iocoder.yudao.module.ai.dal.mysql.workflow.AiWorkflowMapper;
import cn.iocoder.yudao.module.ai.framework.ai.config.YudaoAiProperties;
import cn.iocoder.yudao.module.ai.framework.ai.core.webserch.AiWebSearchResponse;
import cn.iocoder.yudao.module.ai.service.model.AiModelService;
import cn.iocoder.yudao.module.ai.util.AiUtils;
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
import lombok.extern.slf4j.Slf4j;
import org.springframework.ai.chat.client.ChatClient;
import org.springframework.ai.tool.annotation.Tool;
import org.springframework.ai.tool.annotation.ToolParam;
import org.springframework.stereotype.Service;

import java.net.URI;
import java.util.ArrayList;
import java.util.Comparator;
import java.util.HashSet;
import java.util.LinkedHashMap;
import java.util.LinkedHashSet;
import java.util.List;
import java.util.Locale;
import java.util.Map;
import java.util.Set;
import java.util.concurrent.atomic.AtomicInteger;

@Service
@Slf4j
public class AiSearchAgentService {

    @Resource
    private AiModelService aiModelService;
    @Resource
    private AiWorkflowMapper workflowMapper;
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
        AiModelDO workflowModel = getWorkflowModel();
        SearchTool searchTool = new SearchTool(task.getId(), round, Math.min(3, Math.max(1, remainingToolCalls)),
                Math.max(1, remainingQualifiedTarget), aiModelService);
        String context = buildContext(task, round, qualifiedAddedThisRun, remainingQualifiedTarget,
                selectRelevantSources(task, round));
        String searchPolicy = round < 5
                ? "当前是首轮搜索阶段（第1至4轮）。严格只使用用户确认的搜索快照、任务目标及程序筛选出的优先渠道，不得自行扩展新的产品词、客户角色、语言、场景或渠道。每条 query 只表达一个搜索方向，使用简短组合：一个核心产品/关键词 + 国家 + 一个客户角色，可选一个渠道域名；不得把多个同义产品词、角色或渠道堆进同一 query。每轮最多调用3次 web_search，每次调用使用一个不同方向的 query。"
                : "当前进入扩展搜索阶段（第5轮及以后）。先检查已有搜索记录及效果，再按需扩展一个产品词、客户角色、采购词、语言、应用场景或搜索来源。每条 query 只表达一个搜索方向，使用简短组合，不得堆叠同义词、多个角色或多个渠道；本轮最多调用3次 web_search，每次调用使用一个不同方向的 query。";
        String decision = ChatClient.create(aiModelService.getChatModel(workflowModel.getId()))
                .prompt()
                .options(AiUtils.buildChatOptions(AiPlatformEnum.validatePlatform(workflowModel.getPlatform()),
                        workflowModel.getModel(), workflowModel.getTemperature(), workflowModel.getMaxTokens()))
                .system("你是 AI 企业获客 Search Agent。" + searchPolicy + "根据任务目标、搜索快照、搜索历史和来源效果选择搜索方向。重点记忆搜过的 query、关键词/角色/场景/语言/来源及产出，不需要企业名单；企业去重由后端数据库负责。来源仅为优先提示，不是白名单；定向来源无结果时允许普通全网查询。不得输出或编造企业、网址或网页事实，必须通过 web_search 获取真实网页。STOP 仅是建议，不能绕过程序硬限制。完成后最终回答仅输出 CONTINUE、CHANGE_QUERY、CHANGE_SOURCE、EXPAND 或 STOP 及简短理由。")
                .user(context)
                .tools(searchTool)
                .call()
                .content();
        return new AgentTurn(decision, searchTool.getInvocations());
    }

    private AiModelDO getWorkflowModel() {
        String workflowCode = aiProperties.getWebSearch().getWorkflowCode();
        AiWorkflowDO workflow = workflowMapper.selectByCode(workflowCode);
        if (workflow == null || workflow.getGraph() == null || workflow.getGraph().isBlank()) {
            throw new IllegalStateException("AI 搜索工作流不存在或未配置：" + workflowCode);
        }
        var nodes = JSONUtil.parseObj(workflow.getGraph()).getJSONArray("nodes");
        if (nodes != null) {
            for (int index = 0; index < nodes.size(); index++) {
                var node = nodes.getJSONObject(index);
                if (!"llmNode".equals(node.getStr("type")) || node.getJSONObject("data") == null) {
                    continue;
                }
                Long modelId = node.getJSONObject("data").getLong("llmId");
                if (modelId != null) {
                    return aiModelService.validateModel(modelId);
                }
            }
        }
        throw new IllegalStateException("AI 搜索工作流未配置可用的 LLM 节点：" + workflowCode);
    }


    private String buildContext(AiSearchTaskDO task, int round,
                                int qualifiedAddedThisRun, int remainingQualifiedTarget,
                                List<AiSearchSourceDO> sources) {
        List<AiSearchQueryDO> history = queryMapper.selectList(Wrappers.lambdaQuery(AiSearchQueryDO.class)
                .eq(AiSearchQueryDO::getTaskId, task.getId())
                .orderByDesc(AiSearchQueryDO::getId).last("LIMIT 20"));
        List<AiSearchFrontierDO> frontier = frontierMapper.selectList(Wrappers.lambdaQuery(AiSearchFrontierDO.class)
                .eq(AiSearchFrontierDO::getTaskId, task.getId()).orderByDesc(AiSearchFrontierDO::getLastRoundNo).last("LIMIT 100"));
        List<AiSearchMemoryDO> memories = memoryMapper.selectList(Wrappers.lambdaQuery(AiSearchMemoryDO.class)
                .eq(AiSearchMemoryDO::getScopeType, "TASK").eq(AiSearchMemoryDO::getScopeId, task.getId())
                .orderByDesc(AiSearchMemoryDO::getScore).last("LIMIT 30"));
        Map<String, Object> context = new LinkedHashMap<>();
        context.put("round", round);
        context.put("searchPhase", round < 5 ? "SEED_ONLY" : "EXPANSION_ALLOWED");
        context.put("maxQueriesThisRound", 3);
        context.put("maxQueriesPerRun", 15);
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

    private List<AiSearchSourceDO> selectRelevantSources(AiSearchTaskDO task, int round) {
        List<AiSearchSourceDO> sources = sourceMapper.selectList(Wrappers.lambdaQuery(AiSearchSourceDO.class)
                .eq(AiSearchSourceDO::getSourceType, "ENTERPRISE")
                .in(AiSearchSourceDO::getStatus, "ACTIVE", "CANDIDATE"));
        Set<String> countryAliases = countryAliases(task.getTargetCountry());
        Set<String> industryTerms = getIndustryTerms(task);
        List<AiSearchSourceDO> countryMatched = sources.stream()
                .filter(source -> !hasExplicitCountryMismatch(source.getCountries(), countryAliases)).toList();
        List<RankedSource> ranked = countryMatched.stream()
                .map(source -> new RankedSource(source, relevanceScore(source, countryAliases, industryTerms,
                        task.getCustomerType())))
                .sorted(Comparator.comparingInt(RankedSource::score).reversed()
                        .thenComparing(r -> r.source().getId()))
                .limit(50)
                .toList();
        List<AiSearchSourceDO> selected = ranked.stream().map(RankedSource::source).toList();
        List<Map<String, Object>> selectedLog = ranked.stream().map(item -> {
            AiSearchSourceDO source = item.source();
            Map<String, Object> fields = new LinkedHashMap<>();
            fields.put("id", source.getId());
            fields.put("name", source.getName());
            fields.put("domain", source.getDomain());
            fields.put("score", item.score());
            fields.put("countries", source.getCountries());
            fields.put("industries", source.getIndustries());
            fields.put("customerTypes", source.getCustomerTypes());
            fields.put("status", source.getStatus());
            return fields;
        }).toList();
        log.info("AI 搜索渠道筛选结果: taskId={}, round={}, targetCountry={}, eligibleCount={}, excludedCountryCount={}, selectedCount={}, selectedSources={}",
                task.getId(), round, task.getTargetCountry(), sources.size(), sources.size() - countryMatched.size(),
                selected.size(), JSONUtil.toJsonStr(selectedLog));
        return selected;
    }

    private int relevanceScore(AiSearchSourceDO source, Set<String> countryAliases, Set<String> industryTerms,
                               String customerType) {
        int score = 0;
        Set<String> countries = splitTags(source.getCountries());
        if (countries.isEmpty()) {
            score += 10;
        } else if (countries.stream().anyMatch(this::isGlobalCountry)) {
            score += 55;
        } else if (countries.stream().map(this::normalize).anyMatch(countryAliases::contains)) {
            score += 100;
        } else if (countries.stream().anyMatch(country -> sameRegion(country, countryAliases))) {
            score += 65;
        }

        Set<String> industries = splitTags(source.getIndustries());
        if (industries.isEmpty() || industries.stream().anyMatch(this::isBroadIndustryTag)) {
            score += 15;
        } else if (hasTermOverlap(industryTerms, industries)) {
            score += 100;
        } else {
            score -= 100;
        }
        Set<String> customerTypes = splitTags(source.getCustomerTypes());
        if (!customerTypes.isEmpty() && hasTermOverlap(customerAliasesForTask(customerType), customerTypes)) {
            score += 35;
        }
        if (hasTermOverlap(industryTerms, splitTags(source.getKeywords()))) score += 25;
        score += sourceRoleScore(source.getSourceRole());
        score += Math.max(-20, Math.min(40, source.getPriority() == null ? 0 : source.getPriority()));
        if (source.getYieldScore() != null) {
            score += Math.max(0, Math.min(30, source.getYieldScore().intValue()));
        }
        if (Boolean.TRUE.equals(source.getVerified())) {
            score += 5;
        }
        return score;
    }

    private Set<String> getIndustryTerms(AiSearchTaskDO task) {
        Set<String> terms = new LinkedHashSet<>();
        addTerms(terms, task.getCompanyProduct());
        addTerms(terms, task.getLeadKeywords());
        if (task.getSearchSnapshotJson() != null && !task.getSearchSnapshotJson().isBlank()) {
            var snapshot = JSONUtil.parseObj(task.getSearchSnapshotJson());
            for (String key : List.of("selectedKeywords", "productTerms", "applications", "scenarios")) {
                var values = snapshot.getJSONArray(key);
                if (values != null) {
                    values.forEach(value -> addTerms(terms, String.valueOf(value)));
                }
            }
        }
        if (terms.contains("led")) {
            terms.addAll(List.of("lighting", "illumination", "electrical"));
        }
        if (terms.contains("lighting")) {
            terms.addAll(List.of("led", "illumination"));
        }
        return terms;
    }

    private void addTerms(Set<String> terms, String value) {
        if (value == null || value.isBlank()) return;
        for (String token : value.toLowerCase(Locale.ROOT).split("[^\\p{L}\\p{N}]+")) {
            if (token.length() >= 3 || "led".equals(token)) terms.add(token);
        }
    }

    private boolean hasTermOverlap(Set<String> searchTerms, Set<String> tags) {
        if (searchTerms.isEmpty() || tags.isEmpty()) return false;
        Set<String> tagTerms = new HashSet<>();
        tags.forEach(tag -> addTerms(tagTerms, tag));
        return searchTerms.stream().anyMatch(term -> tagTerms.contains(term)
                || tagTerms.stream().anyMatch(tagTerm -> tagTerm.contains(term) || term.contains(tagTerm)));
    }

    private Set<String> splitTags(String value) {
        Set<String> tags = new LinkedHashSet<>();
        if (value == null || value.isBlank()) return tags;
        for (String tag : value.split("[,，;；|\\n\\r]+")) {
            if (!tag.isBlank()) tags.add(tag.trim());
        }
        return tags;
    }

    private Set<String> countryAliases(String country) {
        Set<String> aliases = new HashSet<>();
        String normalized = normalize(country);
        if (normalized.isBlank()) return aliases;
        aliases.add(normalized);
        if (Set.of("us", "usa", "united states", "united states of america", "美国").contains(normalized)) {
            aliases.addAll(Set.of("us", "usa", "united states", "united states of america", "美国"));
        }
        return aliases;
    }

    private boolean hasExplicitCountryMismatch(String countriesValue, Set<String> countryAliases) {
        Set<String> countries = splitTags(countriesValue);
        if (countries.isEmpty() || countryAliases.isEmpty()
                || countries.stream().anyMatch(this::isGlobalCountry)
                || countries.stream().anyMatch(country -> sameRegion(country, countryAliases))) return false;
        return countries.stream().map(this::normalize).noneMatch(countryAliases::contains);
    }

    private boolean sameRegion(String sourceCountry, Set<String> countryAliases) {
        String sourceRegion = regionFor(sourceCountry);
        if (sourceRegion.isBlank()) return false;
        return countryAliases.stream().map(this::regionFor).anyMatch(sourceRegion::equals);
    }

    private String regionFor(String country) {
        String normalized = normalize(country);
        if (Set.of("us", "usa", "united states", "united states of america", "canada", "mexico",
                "north america", "北美", "北美洲").contains(normalized)) return "north america";
        if (Set.of("germany", "france", "united kingdom", "uk", "italy", "spain", "netherlands",
                "europe", "欧盟", "欧洲").contains(normalized)) return "europe";
        if (Set.of("china", "japan", "south korea", "india", "asia", "亚洲").contains(normalized)) return "asia";
        return "";
    }

    private boolean isGlobalCountry(String country) {
        return Set.of("global", "worldwide", "international", "全球", "全球通用", "all countries")
                .contains(normalize(country));
    }

    private boolean isBroadIndustryTag(String industry) {
        return Set.of("all", "all industries", "multi industry", "multi industries", "general", "通用", "全部行业")
                .contains(normalize(industry));
    }

    private String normalize(String value) {
        return value == null ? "" : value.trim().toLowerCase(Locale.ROOT)
                .replaceAll("[_-]+", " ").replaceAll("\\s+", " ");
    }

    private int sourceRoleScore(String role) {
        String normalized = normalize(role);
        if (normalized.contains("discover") || normalized.contains("directory") || normalized.contains("发现")) return 30;
        if (normalized.contains("enrich") || normalized.contains("verify") || normalized.contains("验证")) return 5;
        return 15;
    }

    private Set<String> customerAliasesForTask(String customerType) {
        Set<String> terms = new LinkedHashSet<>();
        addTerms(terms, customerType);
        String normalized = normalize(customerType);
        if (normalized.contains("import")) terms.addAll(List.of("importer", "buyer", "wholesaler", "distributor"));
        if (normalized.contains("distribut")) terms.addAll(List.of("distributor", "dealer", "wholesaler"));
        return terms;
    }

    private record RankedSource(AiSearchSourceDO source, int score) {}

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
        private final Long taskId;
        private final int round;
        private final int maxCalls;
        private final int targetResultCount;
        private final AiModelService aiModelService;
        private final AtomicInteger calls = new AtomicInteger();
        private final List<SearchInvocation> invocations = new ArrayList<>();

        public SearchTool(Long taskId, int round, int maxCalls, int targetResultCount, AiModelService aiModelService) {
            this.taskId = taskId;
            this.round = round;
            this.maxCalls = maxCalls;
            this.targetResultCount = targetResultCount;
            this.aiModelService = aiModelService;
        }

        @Tool(name = "web_search", description = "执行真实互联网网页搜索。只返回搜索后端返回的真实网页标题、URL、摘要和来源，不生成企业或事实。")
        public String webSearch(
                @ToolParam(description = "当前搜索 query，可包含关键词、语言和可选来源域名") String query,
                @ToolParam(description = "最多返回的网页数量，1到100；实际数量由本次剩余目标控制") Integer count) {
            int callNo = calls.incrementAndGet();
            if (callNo > maxCalls) {
                return "{\"error\":\"tool_budget_exhausted\",\"results\":[]}";
            }
            if (query == null || query.isBlank()) {
                throw new IllegalArgumentException("web_search query 不能为空");
            }
            int pageCount = Math.min(targetResultCount, 100);
            log.info("AI搜索工具调用: taskId={}, runRound={}/5, toolCall={}/{}, query={}, modelCount={}, modelCountIgnored={}, targetRemaining={}, effectiveCount={}",
                    taskId, round, callNo, maxCalls, query, count, count != null && count != pageCount,
                    targetResultCount, pageCount);
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
