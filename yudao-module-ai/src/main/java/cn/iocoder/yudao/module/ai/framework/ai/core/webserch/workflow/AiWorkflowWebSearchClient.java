package cn.iocoder.yudao.module.ai.framework.ai.core.webserch.workflow;

import cn.iocoder.yudao.module.ai.framework.ai.core.webserch.AiWebSearchClient;
import cn.iocoder.yudao.module.ai.framework.ai.core.webserch.AiWebSearchRequest;
import cn.iocoder.yudao.module.ai.framework.ai.core.webserch.AiWebSearchResponse;
import cn.iocoder.yudao.module.ai.service.workflow.AiWorkflowService;
import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import lombok.extern.slf4j.Slf4j;
import org.springframework.beans.factory.ObjectProvider;

import java.util.ArrayList;
import java.util.LinkedHashMap;
import java.util.LinkedHashSet;
import java.util.List;
import java.util.Map;
import java.util.Set;
import java.util.regex.Matcher;
import java.util.regex.Pattern;

@Slf4j
public class AiWorkflowWebSearchClient implements AiWebSearchClient {

    private static final Pattern MARKDOWN_LINK = Pattern.compile("\\[([^\\]]+)]\\((https?://[^)\\s]+)\\)");
    private final ObjectProvider<AiWorkflowService> workflowServiceProvider;
    private final String workflowCode;
    private final ObjectMapper objectMapper = new ObjectMapper();

    public AiWorkflowWebSearchClient(ObjectProvider<AiWorkflowService> workflowServiceProvider, String workflowCode) {
        this.workflowServiceProvider = workflowServiceProvider;
        this.workflowCode = workflowCode;
    }

    @Override
    public AiWebSearchResponse search(AiWebSearchRequest request) {
        Integer incomingCount = request.getCount();
        int count = Math.min(Math.max(incomingCount == null ? 10 : incomingCount, 1), 500);
        Map<String, Object> params = new LinkedHashMap<>();
        params.put("query", request.getQuery());
        params.put("count", count);
        log.info("[AiWorkflowWebSearchClient] 搜索请求参数, workflowCode={}, query={}, incomingCount={}, defaultCountApplied={}, effectiveCount={}",
                workflowCode, request.getQuery(), incomingCount, incomingCount == null, count);
        Object raw = workflowServiceProvider.getObject().executeWorkflowByCode(workflowCode, params);
        String content = extractText(raw);
        List<AiWebSearchResponse.WebPage> pages = parsePages(content, count);
        log.info("[AiWorkflowWebSearchClient] 搜索响应, workflowCode={}, query={}, requestedCount={}, rawType={}, parsedCount={}",
                workflowCode, request.getQuery(), count, raw == null ? "null" : raw.getClass().getName(), pages.size());
        if (pages.isEmpty()) {
            log.warn("[AiWorkflowWebSearchClient] 工作流有返回但未解析到网页结果, extractedContent={}",
                    content.length() > 1500 ? content.substring(0, 1500) : content);
        }
        return new AiWebSearchResponse().setTotal((long) pages.size()).setLists(pages);
    }

    private String extractText(Object raw) {
        if (raw instanceof CharSequence text) {
            return text.toString();
        }
        if (raw instanceof Map<?, ?> map) {
            for (String key : List.of("output", "content", "text", "result")) {
                Object value = map.get(key);
                if (value != null) {
                    return value instanceof CharSequence ? value.toString() : objectMapper.valueToTree(value).toString();
                }
            }
        }
        return objectMapper.valueToTree(raw).toString();
    }

    private List<AiWebSearchResponse.WebPage> parsePages(String content, int count) {
        String normalized = content.trim().replaceAll("(?s)^```(?:json)?\\s*|\\s*```$", "").trim();
        Set<String> urls = new LinkedHashSet<>();
        List<AiWebSearchResponse.WebPage> pages = new ArrayList<>();
        try {
            JsonNode items = findResults(objectMapper.readTree(normalized));
            if (items.isArray()) {
                for (JsonNode item : items) {
                    String url = firstText(item, "companyUrl", "url", "link");
                    if (url == null || !url.startsWith("http") || !urls.add(url)) {
                        continue;
                    }
                    String title = firstText(item, "companyName", "title", "name");
                    String evidence = firstText(item, "evidence");
                    String snippet = firstText(item, "snippet", "summary", "description", "evidence");
                    String source = firstText(item, "source", "domain");
                    String companyType = firstText(item, "companyType");
                    title = title == null ? url : title;
                    snippet = snippet == null ? "" : snippet;
                    pages.add(new AiWebSearchResponse.WebPage()
                            .setName(title).setTitle(title).setUrl(url).setSnippet(snippet).setSummary(snippet)
                            .setSource(source == null ? sourceFromUrl(url) : source)
                            .setCompanyType(companyType).setEvidence(evidence));
                    if (pages.size() >= count) {
                        break;
                    }
                }
                return pages;
            }
        } catch (Exception ex) {
            log.debug("[AiWorkflowWebSearchClient] 工作流结果不是 JSON，尝试提取 Markdown 来源链接");
        }

        Matcher matcher = MARKDOWN_LINK.matcher(content);
        while (matcher.find() && pages.size() < count) {
            String title = matcher.group(1).trim();
            String url = matcher.group(2).trim();
            if (urls.add(url)) {
                pages.add(new AiWebSearchResponse.WebPage()
                        .setName(title).setTitle(title).setUrl(url).setSnippet(content).setSummary(content)
                        .setSource(sourceFromUrl(url)));
            }
        }
        return pages;
    }

    private JsonNode findResults(JsonNode node) {
        if (node == null || node.isNull()) {
            return null;
        }
        if (node.isObject()) {
            for (String key : List.of("results", "pages", "sources")) {
                JsonNode results = node.get(key);
                if (results != null && results.isArray()) {
                    return results;
                }
            }
            var fields = node.fields();
            while (fields.hasNext()) {
                JsonNode nested = findResults(fields.next().getValue());
                if (nested != null) {
                    return nested;
                }
            }
            return null;
        }
        if (node.isArray()) {
            if (!node.isEmpty() && node.get(0).isObject()
                    && (node.get(0).has("url") || node.get(0).has("link"))) {
                return node;
            }
            for (JsonNode item : node) {
                JsonNode nested = findResults(item);
                if (nested != null) {
                    return nested;
                }
            }
            return null;
        }
        if (node.isTextual()) {
            String text = node.asText().trim();
            if (text.startsWith("{") || text.startsWith("[")) {
                try {
                    return findResults(objectMapper.readTree(text));
                } catch (Exception ignored) {
                    return null;
                }
            }
        }
        return null;
    }

    private String firstText(JsonNode node, String... keys) {
        for (String key : keys) {
            String value = node.path(key).asText("");
            if (!value.isBlank()) {
                return value;
            }
        }
        return null;
    }

    private String sourceFromUrl(String url) {
        try {
            return java.net.URI.create(url).getHost();
        } catch (Exception ignored) {
            return "";
        }
    }
}
