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
        int count = Math.min(Math.max(request.getCount() == null ? 10 : request.getCount(), 1), 50);
        Map<String, Object> params = new LinkedHashMap<>();
        params.put("query", request.getQuery());
        params.put("count", count);
        Object raw = workflowServiceProvider.getObject().executeWorkflowByCode(workflowCode, params);
        String content = extractText(raw);
        List<AiWebSearchResponse.WebPage> pages = parsePages(content, count);
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
            JsonNode items = objectMapper.readTree(normalized);
            if (items.isObject()) {
                for (String key : List.of("results", "pages", "sources")) {
                    if (items.path(key).isArray()) {
                        items = items.path(key);
                        break;
                    }
                }
            }
            if (items.isArray()) {
                for (JsonNode item : items) {
                    String url = firstText(item, "url", "link");
                    if (url == null || !url.startsWith("http") || !urls.add(url)) {
                        continue;
                    }
                    String title = firstText(item, "title", "name");
                    String snippet = firstText(item, "snippet", "summary", "description");
                    String source = firstText(item, "source", "domain");
                    title = title == null ? url : title;
                    snippet = snippet == null ? "" : snippet;
                    pages.add(new AiWebSearchResponse.WebPage()
                            .setName(title).setTitle(title).setUrl(url).setSnippet(snippet).setSummary(snippet)
                            .setSource(source == null ? sourceFromUrl(url) : source));
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
