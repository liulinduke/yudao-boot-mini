package cn.iocoder.yudao.module.facebook.service.aisearch;

import cn.iocoder.yudao.framework.common.util.object.BeanUtils;
import cn.iocoder.yudao.module.ai.framework.ai.core.webserch.AiWebSearchRequest;
import cn.iocoder.yudao.module.ai.framework.ai.core.webserch.AiWebSearchResponse;
import cn.iocoder.yudao.module.ai.enums.model.AiModelTypeEnum;
import cn.iocoder.yudao.module.ai.service.model.AiModelService;
import cn.iocoder.yudao.module.ai.service.workflow.AiWorkflowService;
import cn.iocoder.yudao.module.ai.dal.mysql.workflow.AiWorkflowMapper;
import cn.iocoder.yudao.module.ai.dal.dataobject.workflow.AiWorkflowDO;
import cn.iocoder.yudao.module.facebook.controller.admin.aisearch.vo.AiSearchExpansionReqVO;
import cn.iocoder.yudao.module.facebook.controller.admin.aisearch.vo.AiSearchExpansionRespVO;
import cn.iocoder.yudao.module.facebook.controller.admin.aisearch.vo.AiSearchTaskRespVO;
import cn.iocoder.yudao.module.facebook.controller.admin.aisearch.vo.AiSearchTaskSaveReqVO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.aisearch.AiSearchCompanyDO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.aisearch.AiSearchTaskDO;
import cn.iocoder.yudao.module.facebook.dal.mysql.aisearch.AiSearchCompanyMapper;
import cn.iocoder.yudao.module.facebook.dal.mysql.aisearch.AiSearchTaskMapper;
import cn.iocoder.yudao.module.facebook.dal.dataobject.aisearch.AiSearchRunDO;
import cn.iocoder.yudao.module.facebook.dal.mysql.aisearch.AiSearchRunMapper;
import cn.iocoder.yudao.module.facebook.dal.dataobject.aisearch.AiSearchSourceDO;
import cn.iocoder.yudao.module.facebook.dal.mysql.aisearch.AiSearchSourceMapper;
import cn.iocoder.yudao.module.facebook.dal.dataobject.aisearch.AiSearchFrontierDO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.aisearch.AiSearchMemoryDO;
import cn.iocoder.yudao.module.facebook.dal.mysql.aisearch.AiSearchFrontierMapper;
import cn.iocoder.yudao.module.facebook.dal.mysql.aisearch.AiSearchMemoryMapper;
import cn.iocoder.yudao.module.facebook.dal.dataobject.aisearch.AiSearchCompanyEvidenceDO;
import cn.iocoder.yudao.module.facebook.dal.mysql.aisearch.AiSearchCompanyEvidenceMapper;
import cn.iocoder.yudao.module.facebook.dal.dataobject.aisearch.AiSearchContactDO;
import cn.iocoder.yudao.module.facebook.dal.mysql.aisearch.AiSearchContactMapper;
import cn.iocoder.yudao.module.facebook.dal.dataobject.aisearch.AiSearchContactEvidenceDO;
import cn.iocoder.yudao.module.facebook.dal.mysql.aisearch.AiSearchContactEvidenceMapper;
import cn.iocoder.yudao.module.facebook.dal.dataobject.aisearch.AiSearchTradeEvidenceDO;
import cn.iocoder.yudao.module.facebook.dal.mysql.aisearch.AiSearchTradeEvidenceMapper;
import cn.iocoder.yudao.module.facebook.dal.dataobject.aisearch.AiSearchRoundDO;
import cn.iocoder.yudao.module.facebook.dal.dataobject.aisearch.AiSearchQueryDO;
import cn.iocoder.yudao.module.facebook.dal.mysql.aisearch.AiSearchRoundMapper;
import cn.iocoder.yudao.module.facebook.dal.mysql.aisearch.AiSearchQueryMapper;
import com.baomidou.mybatisplus.core.toolkit.Wrappers;
import jakarta.annotation.Resource;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.stereotype.Service;
import org.springframework.ai.chat.model.ChatModel;
import org.springframework.ai.chat.prompt.Prompt;
import org.springframework.ai.chat.messages.SystemMessage;
import org.springframework.ai.chat.messages.UserMessage;
import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import org.jsoup.Jsoup;
import org.jsoup.nodes.Document;
import org.jsoup.nodes.Element;

import java.net.URI;
import java.time.LocalDateTime;
import java.util.Collections;
import java.util.List;
import java.util.ArrayList;
import java.util.LinkedHashSet;
import java.util.Set;
import java.util.Locale;
import java.util.Map;

@Service
public class AiSearchTaskServiceImpl implements AiSearchTaskService {

    private static final int DEFAULT_RESULT_COUNT = 20;
    private static final int MAX_RESULT_COUNT = 50;
    private static final int MAX_SEARCH_ROUNDS = 20;
    private static final int MAX_TOOL_CALLS = 20;
    private static final long MAX_RUN_MILLIS = 10 * 60 * 1000L;

    @Resource
    private AiSearchTaskMapper taskMapper;
    @Resource
    private AiSearchCompanyMapper companyMapper;
    @Resource
    private AiSearchRunMapper runMapper;
    @Resource
    private AiSearchSourceMapper sourceMapper;
    @Resource
    private AiSearchFrontierMapper frontierMapper;
    @Resource
    private AiSearchMemoryMapper memoryMapper;
    @Resource
    private AiSearchCompanyEvidenceMapper evidenceMapper;
    @Resource
    private AiSearchContactMapper contactMapper;
    @Resource
    private AiSearchContactEvidenceMapper contactEvidenceMapper;
    @Resource
    private AiSearchTradeEvidenceMapper tradeEvidenceMapper;
    @Resource private AiSearchRoundMapper roundMapper;
    @Resource private AiSearchQueryMapper queryMapper;
    @Resource
    private AiModelService aiModelService;
    @Resource private AiWorkflowService aiWorkflowService;
    @Resource private AiWorkflowMapper aiWorkflowMapper;
    @Resource
    private AiSearchTradeService tradeService;
    @Resource
    private AiSearchAgentService searchAgentService;

    @Override
    public AiSearchExpansionRespVO expand(AiSearchExpansionReqVO req) {
        AiSearchExpansionRespVO response = new AiSearchExpansionRespVO();
        int keywordLimit = Math.min(Math.max(req.getKeywordLimit() == null ? 6 : req.getKeywordLimit(), 1), 6);
        int sceneLimit = Math.min(Math.max(req.getSceneLimit() == null ? 4 : req.getSceneLimit(), 1), 4);
        AiWorkflowDO workflow = aiWorkflowMapper.selectByCode("ai_enterprise_search_expand_v1");
        if (workflow == null) throw new IllegalStateException("AI企业搜索扩展工作流不存在：ai_enterprise_search_expand_v1");
        Map<String, Object> params = new java.util.LinkedHashMap<>();
        params.put("company", req.getCompany() == null ? req.getUserGoal() : req.getCompany()); params.put("keywords", req.getKeywords() == null ? req.getUserGoal() : req.getKeywords());
        params.put("country", req.getTargetCountry()); params.put("customerType", req.getCustomerType());
        params.put("hsCode", req.getHsCode());
        Object raw = aiWorkflowService.executeWorkflow(workflow.getId(), params);
        String text = extractWorkflowText(raw);
        List<String> keywords = new ArrayList<>(); List<String> scenes = new ArrayList<>();
        List<String> productTerms = new ArrayList<>(); List<String> customerRoles = new ArrayList<>(); List<String> localTerms = new ArrayList<>();
        List<String> purchasingTerms = new ArrayList<>(); List<String> applications = new ArrayList<>();
        List<String> ecommerceChannelTerms = new ArrayList<>(); List<String> upstreamDownstreamTerms = new ArrayList<>();
        try {
            JsonNode node = new ObjectMapper().readTree(stripJsonFence(text));
            node.path("keywords").elements().forEachRemaining(n -> keywords.add(n.asText()));
            node.path("scenes").elements().forEachRemaining(n -> scenes.add(n.asText()));
            node.path("productTerms").elements().forEachRemaining(n -> productTerms.add(n.asText()));
            node.path("customerRoles").elements().forEachRemaining(n -> customerRoles.add(n.asText()));
            node.path("localTerms").elements().forEachRemaining(n -> localTerms.add(n.asText()));
            node.path("purchasingTerms").elements().forEachRemaining(n -> purchasingTerms.add(n.asText()));
            node.path("applications").elements().forEachRemaining(n -> applications.add(n.asText()));
            node.path("ecommerceChannelTerms").elements().forEachRemaining(n -> ecommerceChannelTerms.add(n.asText()));
            node.path("upstreamDownstreamTerms").elements().forEachRemaining(n -> upstreamDownstreamTerms.add(n.asText()));
        } catch (Exception ignored) { }
        response.setKeywords(keywords.stream().filter(s -> s != null && !s.isBlank()).distinct().limit(keywordLimit).toList());
        response.setScenes(scenes.stream().filter(s -> s != null && !s.isBlank()).distinct().limit(sceneLimit).toList());
        response.setProductTerms(productTerms.stream().filter(s -> s != null && !s.isBlank()).distinct().toList());
        response.setCustomerRoles(customerRoles.stream().filter(s -> s != null && !s.isBlank()).distinct().toList());
        response.setLocalTerms(localTerms.stream().filter(s -> s != null && !s.isBlank()).distinct().toList());
        response.setPurchasingTerms(purchasingTerms.stream().filter(s -> s != null && !s.isBlank()).distinct().toList());
        response.setApplications(applications.stream().filter(s -> s != null && !s.isBlank()).distinct().toList());
        response.setEcommerceChannelTerms(ecommerceChannelTerms.stream().filter(s -> s != null && !s.isBlank()).distinct().toList());
        response.setUpstreamDownstreamTerms(upstreamDownstreamTerms.stream().filter(s -> s != null && !s.isBlank()).distinct().toList());
        return response;
    }

    private String extractWorkflowText(Object raw) {
        if (raw instanceof Map<?, ?> map) {
            Object output = map.get("output");
            if (output != null) return String.valueOf(output);
        }
        String text = String.valueOf(raw);
        try {
            JsonNode node = new ObjectMapper().readTree(text);
            if (node.path("output").isTextual()) return node.path("output").asText();
        } catch (Exception ignored) { }
        return text;
    }

    @Override
    public Long create(AiSearchTaskSaveReqVO req) {
        AiSearchTaskDO task = BeanUtils.toBean(req, AiSearchTaskDO.class);
        task.setCompanyProduct(req.getCompany());
        task.setLeadKeywords(req.getKeywords());
        task.setSearchSnapshotJson(serializeSearchSnapshot(req.getSearchSnapshot()));
        if (task.getName() == null || task.getName().isBlank()) {
            task.setName(task.getUserGoal());
        }
        if (task.getTargetCount() == null || task.getTargetCount() < 1) {
            task.setTargetCount(100);
        }
        inferGoalFields(task);
        task.setStatus("DRAFT");
        taskMapper.insert(task);
        return task.getId();
    }

    private void inferGoalFields(AiSearchTaskDO task) {
        if (task.getUserGoal() == null || task.getUserGoal().isBlank()
                || (task.getTargetCountry() != null && task.getCustomerType() != null)) return;
        try {
            var model = aiModelService.getRequiredDefaultModel(AiModelTypeEnum.CHAT.getType());
            String prompt = "从以下外贸获客目标提取国家、客户类型、目标企业数量。只输出JSON：{\"country\":\"\",\"customerType\":\"\",\"count\":0}。无法确定的字段留空。目标：" + task.getUserGoal();
            String content = aiModelService.getChatModel(model.getId()).call(new Prompt(new UserMessage(prompt))).getResult().getOutput().getText();
            JsonNode node = new ObjectMapper().readTree(stripJsonFence(content));
            if ((task.getTargetCountry() == null || task.getTargetCountry().isBlank()) && node.path("country").isTextual()) task.setTargetCountry(node.path("country").asText());
            if ((task.getCustomerType() == null || task.getCustomerType().isBlank()) && node.path("customerType").isTextual()) task.setCustomerType(node.path("customerType").asText());
            if ((task.getTargetCount() == null || task.getTargetCount() == 100) && node.path("count").canConvertToInt() && node.path("count").asInt() > 0) task.setTargetCount(node.path("count").asInt());
        } catch (Exception ignored) {
            // Keep the original goal when the optional inference call is unavailable.
        }
    }

    @Override
    public void update(AiSearchTaskSaveReqVO req) {
        AiSearchTaskDO task = BeanUtils.toBean(req, AiSearchTaskDO.class);
        task.setCompanyProduct(req.getCompany());
        task.setLeadKeywords(req.getKeywords());
        task.setSearchSnapshotJson(serializeSearchSnapshot(req.getSearchSnapshot()));
        taskMapper.updateById(task);
    }

    private String serializeSearchSnapshot(cn.iocoder.yudao.module.facebook.controller.admin.aisearch.vo.AiSearchSnapshotVO snapshot) {
        if (snapshot == null) return null;
        snapshot.setVersion(1);
        if (snapshot.getSelectedKeywords() != null && snapshot.getSelectedKeywords().size() > 6) {
            snapshot.setSelectedKeywords(snapshot.getSelectedKeywords().subList(0, 6));
        }
        if (snapshot.getScenarios() != null && snapshot.getScenarios().size() > 4) {
            snapshot.setScenarios(snapshot.getScenarios().subList(0, 4));
        }
        return cn.hutool.json.JSONUtil.toJsonStr(snapshot);
    }

    @Override
    public void delete(Long id) {
        AiSearchTaskDO task = taskMapper.selectById(id);
        if (task == null) throw new IllegalArgumentException("任务不存在");
        taskMapper.deleteById(id);
    }

    @Override
    public void updateStatus(Long id, String status) {
        if (!List.of("DRAFT", "READY", "RUNNING", "PAUSED", "STOPPED", "COMPLETED", "FAILED", "SEARCH_SPACE_EXHAUSTED").contains(status)) {
            throw new IllegalArgumentException("任务状态不正确");
        }
        AiSearchTaskDO task = taskMapper.selectById(id);
        if (task == null) throw new IllegalArgumentException("任务不存在");
        task.setStatus(status);
        taskMapper.updateById(task);
    }

    @Override
    public AiSearchTaskRespVO get(Long id) {
        AiSearchTaskDO task = taskMapper.selectById(id);
        return task == null ? null : AiSearchTaskRespVO.from(task);
    }

    @Override
    public List<AiSearchTaskRespVO> list() {
        return taskMapper.selectList(Wrappers.lambdaQuery(AiSearchTaskDO.class)
                        .orderByDesc(AiSearchTaskDO::getId))
                .stream().map(AiSearchTaskRespVO::from).toList();
    }

    @Override
    public void start(Long id) {
        AiSearchTaskDO task = taskMapper.selectById(id);
        if (task == null) {
            throw new IllegalArgumentException("任务不存在");
        }
        if ("STOPPED".equals(task.getStatus())) {
            throw new IllegalStateException("已停止的任务不能直接重新启动，请先复制任务");
        }
        if ("RUNNING".equals(task.getStatus())) {
            throw new IllegalStateException("任务正在运行，请勿重复启动");
        }
        searchAgentService.assertSearchReady();
        int companyBaseline = companyCount(task.getId());
        int qualifiedBaseline = qualifiedCompanyCount(task.getId());
        task.setStatus("RUNNING");
        task.setStartedAt(LocalDateTime.now());
        task.setCompletedAt(null);
        taskMapper.updateById(task);
        AiSearchRunDO run = new AiSearchRunDO();
        run.setTaskId(task.getId());
        run.setStatus("RUNNING");
        run.setStartedAt(LocalDateTime.now());
        run.setRoundCount(0);
        run.setQueryCount(0);
        run.setNewCompanyCount(0);
        run.setQualifiedCompanyCount(0);
        run.setErrorCount(0);
        int target = task.getTargetCount() == null ? DEFAULT_RESULT_COUNT : task.getTargetCount();
        run.setTargetQualifiedCount(target);
        run.setQualifiedCountBefore(qualifiedBaseline);
        runMapper.insert(run);
        try {
            ensureFrontier(task);
            int rounds = 0;
            int lowYieldRounds = 0;
            int toolCalls = 0;
            long runDeadline = System.currentTimeMillis() + MAX_RUN_MILLIS;
            while (qualifiedCompanyCount(task.getId()) - qualifiedBaseline < target
                    && rounds < MAX_SEARCH_ROUNDS
                    && toolCalls < MAX_TOOL_CALLS
                    && lowYieldRounds < 2 && System.currentTimeMillis() < runDeadline) {
                int round = nextRound(task.getId());
                int qualifiedAddedThisRun = qualifiedCompanyCount(task.getId()) - qualifiedBaseline;
                int remainingQualifiedTarget = Math.max(0, target - qualifiedAddedThisRun);
                AiSearchAgentService.AgentTurn turn = searchAgentService.search(task, round,
                        MAX_TOOL_CALLS - toolCalls, qualifiedAddedThisRun, remainingQualifiedTarget);
                rounds++;
                int qualifiedAddedThisRound = 0;
                for (AiSearchAgentService.SearchInvocation invocation : turn.getInvocations()) {
                    toolCalls++;
                    qualifiedAddedThisRound += searchOnce(task, run.getId(), round, invocation);
                }
                lowYieldRounds = qualifiedAddedThisRound == 0 ? lowYieldRounds + 1 : 0;
            }
            int added = companyCount(task.getId()) - companyBaseline;
            int qualifiedAdded = qualifiedCompanyCount(task.getId()) - qualifiedBaseline;
            remember(task, qualifiedAdded);
            run.setNewCompanyCount(added);
            run.setQualifiedCompanyCount(qualifiedAdded);
            run.setContactCount(Math.toIntExact(contactMapper.selectCount(Wrappers.lambdaQuery(AiSearchContactDO.class)
                    .eq(AiSearchContactDO::getTaskId, task.getId()))));
            run.setTradeVerificationCount(task.getHsCodes() == null || task.getHsCodes().isBlank() ? 0 : added);
            run.setTradeFoundCount(Math.toIntExact(tradeEvidenceMapper.selectCount(Wrappers.lambdaQuery(AiSearchTradeEvidenceDO.class)
                    .inSql(AiSearchTradeEvidenceDO::getCompanyId, "SELECT id FROM ai_search_company WHERE task_id = " + task.getId()))));
            run.setRoundCount(rounds);
            run.setQueryCount(toolCalls);
            boolean targetReached = qualifiedAdded >= target;
            run.setStatus(targetReached ? "COMPLETED" : "SEARCH_SPACE_EXHAUSTED");
            run.setCompletedAt(LocalDateTime.now());
            runMapper.updateById(run);
            task.setStatus(targetReached ? "COMPLETED" : "SEARCH_SPACE_EXHAUSTED");
            task.setCompletedAt(LocalDateTime.now());
            taskMapper.updateById(task);
        } catch (RuntimeException ex) {
            task.setStatus("FAILED");
            task.setCompletedAt(LocalDateTime.now());
            taskMapper.updateById(task);
            run.setStatus("FAILED");
            run.setErrorCount(1);
            run.setCompletedAt(LocalDateTime.now());
            runMapper.updateById(run);
            throw ex;
        }
    }

    @Override
    public int dispatchScheduled() {
        LocalDateTime now = LocalDateTime.now();
        List<AiSearchTaskDO> tasks = taskMapper.selectList(Wrappers.lambdaQuery(AiSearchTaskDO.class)
                .in(AiSearchTaskDO::getStatus, "DRAFT", "COMPLETED", "SEARCH_SPACE_EXHAUSTED")
                .ne(AiSearchTaskDO::getScheduleType, "ONCE"));
        int dispatched = 0;
        for (AiSearchTaskDO task : tasks) {
            if (!isDue(task, now)) continue;
            try {
                start(task.getId());
                dispatched++;
            } catch (RuntimeException ex) {
                // One unavailable provider/source must not block unrelated scheduled tasks.
                task.setStatus("FAILED");
                task.setCompletedAt(LocalDateTime.now());
                taskMapper.updateById(task);
            }
        }
        return dispatched;
    }

    private boolean isDue(AiSearchTaskDO task, LocalDateTime now) {
        if (task.getStartedAt() == null && task.getCompletedAt() == null) return true;
        LocalDateTime last = task.getCompletedAt() == null ? task.getStartedAt() : task.getCompletedAt();
        long amount = 1;
        if (task.getScheduleInterval() != null && task.getScheduleInterval().matches("\\d+")) {
            amount = Math.max(1, Long.parseLong(task.getScheduleInterval()));
        }
        String type = task.getScheduleType() == null ? "DAILY" : task.getScheduleType().toUpperCase(Locale.ROOT);
        long days = "WEEKLY".equals(type) ? amount * 7 : "MONTHLY".equals(type) ? amount * 30 : amount;
        return !last.plusDays(days).isAfter(now);
    }

    private void ensureFrontier(AiSearchTaskDO task) {
        if (frontierMapper.selectCount(Wrappers.lambdaQuery(AiSearchFrontierDO.class)
                .eq(AiSearchFrontierDO::getTaskId, task.getId())) > 0) return;
        AiSearchFrontierDO frontier = new AiSearchFrontierDO();
        frontier.setTaskId(task.getId()); frontier.setCountry(task.getTargetCountry());
        frontier.setCustomerType(task.getCustomerType()); frontier.setLanguage(languageFor(task.getTargetCountry())); frontier.setKeyword(task.getUserGoal());
        frontier.setStatus("UNEXPLORED"); frontier.setLastRoundNo(0); frontierMapper.insert(frontier);
    }

    private void remember(AiSearchTaskDO task, int added) {
        AiSearchMemoryDO memory = memoryMapper.selectOne(Wrappers.lambdaQuery(AiSearchMemoryDO.class)
                .eq(AiSearchMemoryDO::getScopeType, "TASK").eq(AiSearchMemoryDO::getScopeId, task.getId())
                .eq(AiSearchMemoryDO::getMemoryKey, "goal").last("LIMIT 1"));
        if (memory == null) {
            memory = new AiSearchMemoryDO(); memory.setScopeType("TASK"); memory.setScopeId(task.getId());
            memory.setMemoryKey("goal"); memory.setUsageCount(0);
        }
        memory.setMemoryValue(task.getUserGoal()); memory.setScore(java.math.BigDecimal.valueOf(added));
        memory.setUsageCount((memory.getUsageCount() == null ? 0 : memory.getUsageCount()) + 1);
        if (memory.getId() == null) memoryMapper.insert(memory); else memoryMapper.updateById(memory);
        frontierMapper.update(null, Wrappers.lambdaUpdate(AiSearchFrontierDO.class)
                .eq(AiSearchFrontierDO::getTaskId, task.getId()).set(AiSearchFrontierDO::getStatus, "EXPLORED")
                .set(AiSearchFrontierDO::getLastRoundNo, currentRound(task.getId())));
    }

    private int currentRound(Long taskId) {
        AiSearchFrontierDO frontier = frontierMapper.selectOne(Wrappers.lambdaQuery(AiSearchFrontierDO.class)
                .eq(AiSearchFrontierDO::getTaskId, taskId).orderByDesc(AiSearchFrontierDO::getLastRoundNo).last("LIMIT 1"));
        return frontier == null || frontier.getLastRoundNo() == null ? 1 : frontier.getLastRoundNo();
    }

    private int companyCount(Long taskId) {
        return Math.toIntExact(companyMapper.selectCount(Wrappers.lambdaQuery(AiSearchCompanyDO.class)
                .eq(AiSearchCompanyDO::getTaskId, taskId)));
    }

    private int qualifiedCompanyCount(Long taskId) {
        return Math.toIntExact(companyMapper.selectCount(Wrappers.lambdaQuery(AiSearchCompanyDO.class)
                .eq(AiSearchCompanyDO::getTaskId, taskId)
                .in(AiSearchCompanyDO::getIcpLevel, "A", "B")));
    }

    private int searchOnce(AiSearchTaskDO task, Long runId, int round,
                           AiSearchAgentService.SearchInvocation invocation) {
        String queryText = invocation.getQuery();
        List<AiWebSearchResponse.WebPage> pages = invocation.getPages();
        frontierMapper.update(null, Wrappers.lambdaUpdate(AiSearchFrontierDO.class)
                .eq(AiSearchFrontierDO::getTaskId, task.getId())
                .set(AiSearchFrontierDO::getLastRoundNo, round));
        int before = companyCount(task.getId());
        int qualifiedBefore = qualifiedCompanyCount(task.getId());
        int duplicates = 0;
        Long sourceId = sourceIdFromQuery(queryText);
        int newSources = registerDiscoveredSources(task, pages);
        for (AiWebSearchResponse.WebPage page : pages) {
            if (page == null || page.getUrl() == null || page.getUrl().isBlank()) {
                duplicates++;
                continue;
            }
            String domain = canonicalDomain(page.getUrl());
            if (domain.isBlank() || companyMapper.selectCount(Wrappers.lambdaQuery(AiSearchCompanyDO.class)
                    .eq(AiSearchCompanyDO::getNormalizedName, normalize(domain))) > 0) {
                duplicates++;
                continue;
            }
            page = enrichPage(page);
            AiSearchCompanyDO company = new AiSearchCompanyDO();
            String name = firstNonBlank(page.getName(), page.getTitle(), canonicalDomain(page.getUrl()));
            company.setTaskId(task.getId());
            company.setName(name);
            company.setWebsite(page.getUrl());
            company.setDomain(canonicalDomain(page.getUrl()));
            company.setNormalizedName(normalize(company.getDomain()));
            company.setDescription(firstNonBlank(page.getSummary(), page.getSnippet()));
            company.setCountry(task.getTargetCountry());
            company.setCustomerType(task.getCustomerType());
            company.setVerificationStatus("UNKNOWN");
            evaluateIcp(task, company);
            companyMapper.insert(company);
            saveEvidence(company, page, sourceId, runId);
            if (Boolean.TRUE.equals(task.getContactEnrichment())) {
                saveBasicContact(task, company, page);
                discoverAdditionalContacts(task, company, page);
            }
            if (task.getHsCodes() != null && !task.getHsCodes().isBlank()) {
                String hsCode = task.getHsCodes().split(",")[0].trim();
                tradeService.verify(company.getId(), hsCode, task.getUserGoal());
            }
        }
        int added = companyCount(task.getId()) - before;
        int qualifiedAdded = Math.max(0, qualifiedCompanyCount(task.getId()) - qualifiedBefore);
        AiSearchQueryDO query = new AiSearchQueryDO(); query.setTaskId(task.getId()); query.setRoundNo(round);
        query.setRunId(runId);
        query.setQuery(queryText); query.setCountry(task.getTargetCountry()); query.setLanguage(languageFor(task.getTargetCountry())); query.setResultCount(pages.size());
        Map<String, Object> dimensions = new java.util.LinkedHashMap<>();
        dimensions.put("snapshot", task.getSearchSnapshotJson() == null ? Map.of()
                : cn.hutool.json.JSONUtil.parseObj(task.getSearchSnapshotJson()));
        dimensions.put("query", queryText);
        dimensions.put("language", languageFor(task.getTargetCountry()));
        dimensions.put("sourceId", sourceId);
        query.setDimensionsJson(cn.hutool.json.JSONUtil.toJsonStr(dimensions));
        query.setNewCompanyCount(added); query.setDuplicateCount(duplicates); query.setQualifiedCount(qualifiedAdded);
        query.setSourceId(sourceId);
        query.setYieldScore(java.math.BigDecimal.valueOf(added)); query.setStatus("COMPLETED"); queryMapper.insert(query);
        saveFrontier(task, queryText, round, sourceId);
        rememberSearchQuery(task, queryText, qualifiedAdded, pages.size(), duplicates);
        updateSourceYield(queryText, pages.size(), added, qualifiedAdded);
        AiSearchRoundDO searchRound = new AiSearchRoundDO(); searchRound.setTaskId(task.getId()); searchRound.setRunId(runId);
        searchRound.setRoundNo(round); searchRound.setStrategy(queryText); searchRound.setQueryCount(1);
        searchRound.setResultCount(pages.size()); searchRound.setNewCompanyCount(added); searchRound.setQualifiedCompanyCount(qualifiedAdded);
        searchRound.setDuplicateCount(duplicates); searchRound.setNewSourceCount(newSources); roundMapper.insert(searchRound);
        return qualifiedAdded;
    }

    private int registerDiscoveredSources(AiSearchTaskDO task, List<AiWebSearchResponse.WebPage> pages) {
        int added = 0;
        for (AiWebSearchResponse.WebPage page : pages) {
            if (page == null || page.getUrl() == null || page.getUrl().isBlank()) continue;
            String domain = canonicalDomain(page.getUrl());
            if (domain.isBlank() || domain.contains("google.") || domain.contains("bing.com")) continue;
            if (sourceMapper.selectCount(Wrappers.lambdaQuery(AiSearchSourceDO.class).eq(AiSearchSourceDO::getDomain, domain)) > 0) continue;
            AiSearchSourceDO source = new AiSearchSourceDO(); source.setName(domain); source.setDomain(domain);
            source.setSourceType("OTHER"); source.setSourceRole("COMPANY_DISCOVERY"); source.setDiscoveryMethods("SEARCH_ENGINE,DETAIL_PAGE");
            source.setCountries(task.getTargetCountry()); source.setCustomerTypes(task.getCustomerType()); source.setKeywords(task.getUserGoal());
            source.setStatus("CANDIDATE"); source.setVerified(false); source.setPriority(0); source.setUsageCount(0);
            source.setCompanyCount(0); source.setQualifiedCompanyCount(0); sourceMapper.insert(source); added++;
        }
        return added;
    }

    private Long sourceIdFromQuery(String query) {
        java.util.regex.Matcher matcher = java.util.regex.Pattern.compile("\\bsite:([^\\s]+)", java.util.regex.Pattern.CASE_INSENSITIVE)
                .matcher(query == null ? "" : query);
        if (!matcher.find()) return null;
        String domain = canonicalDomain(matcher.group(1));
        if (domain.isBlank()) return null;
        AiSearchSourceDO source = sourceMapper.selectOne(Wrappers.lambdaQuery(AiSearchSourceDO.class)
                .eq(AiSearchSourceDO::getDomain, domain).last("LIMIT 1"));
        return source == null ? null : source.getId();
    }

    private void saveFrontier(AiSearchTaskDO task, String query, int round, Long sourceId) {
        AiSearchFrontierDO frontier = new AiSearchFrontierDO();
        frontier.setTaskId(task.getId());
        frontier.setCountry(task.getTargetCountry());
        frontier.setProduct(task.getCompanyProduct());
        frontier.setCustomerType(task.getCustomerType());
        frontier.setLanguage(languageFor(task.getTargetCountry()));
        frontier.setKeyword(query.length() > 500 ? query.substring(0, 500) : query);
        frontier.setSourceId(sourceId);
        frontier.setStatus("EXPLORED");
        frontier.setLastRoundNo(round);
        frontierMapper.insert(frontier);
    }

    private void rememberSearchQuery(AiSearchTaskDO task, String query, int qualified, int results, int duplicates) {
        String key = "query:" + Integer.toUnsignedString(query.trim().toLowerCase(Locale.ROOT).hashCode(), 36);
        AiSearchMemoryDO memory = memoryMapper.selectOne(Wrappers.lambdaQuery(AiSearchMemoryDO.class)
                .eq(AiSearchMemoryDO::getScopeType, "TASK").eq(AiSearchMemoryDO::getScopeId, task.getId())
                .eq(AiSearchMemoryDO::getMemoryKey, key).last("LIMIT 1"));
        if (memory == null) {
            memory = new AiSearchMemoryDO();
            memory.setScopeType("TASK"); memory.setScopeId(task.getId()); memory.setMemoryKey(key);
            memory.setUsageCount(0);
        }
        java.util.Map<String, Object> value = new java.util.LinkedHashMap<>();
        value.put("query", query); value.put("qualified", qualified); value.put("results", results); value.put("duplicates", duplicates);
        memory.setMemoryValue(cn.hutool.json.JSONUtil.toJsonStr(value));
        memory.setScore(java.math.BigDecimal.valueOf(qualified));
        memory.setUsageCount(memory.getUsageCount() + 1);
        if (memory.getId() == null) memoryMapper.insert(memory); else memoryMapper.updateById(memory);
    }

    /** Fetches only the public landing page to improve entity evidence; failures keep search-engine evidence. */
    private AiWebSearchResponse.WebPage enrichPage(AiWebSearchResponse.WebPage page) {
        try {
            Document document = Jsoup.connect(page.getUrl()).userAgent("Mozilla/5.0").timeout(8000).followRedirects(true).get();
            String description = document.select("meta[name=description],meta[property=og:description]").stream()
                    .map(element -> element.attr("content")).filter(value -> value != null && !value.isBlank()).findFirst().orElse(null);
            String body = document.body() == null ? null : document.body().text();
            if (body != null && body.length() > 1200) body = body.substring(0, 1200);
            String baseHost = canonicalDomain(page.getUrl());
            StringBuilder deepText = new StringBuilder();
            int followed = 0;
            Set<String> visited = new LinkedHashSet<>();
            for (Element link : document.select("a[href]")) {
                if (followed >= 8) break;
                String label = (link.text() + " " + link.attr("href")).toLowerCase(Locale.ROOT);
                if (!(label.contains("contact") || label.contains("about") || label.contains("team")
                        || label.contains("company") || label.contains("supplier") || label.contains("seller")
                        || label.contains("catalog") || label.contains("category") || label.contains("directory")
                        || label.contains("page=") || label.matches(".*(next|older|more|detail|profile).*"))) continue;
                URI resolved;
                try { resolved = URI.create(page.getUrl()).resolve(link.attr("href")); } catch (Exception ignored) { continue; }
                if (!List.of("http", "https").contains(resolved.getScheme()) || !baseHost.equals(canonicalDomain(resolved.toString()))) continue;
                String resolvedUrl = resolved.toString().split("#", 2)[0];
                if (!visited.add(resolvedUrl) || resolvedUrl.equals(page.getUrl())) continue;
                try {
                    Document detail = Jsoup.connect(resolvedUrl).userAgent("Mozilla/5.0").timeout(5000).followRedirects(true).get();
                    String detailText = detail.body() == null ? "" : detail.body().text();
                    if (!detailText.isBlank()) {
                        if (deepText.length() > 0) deepText.append(" ");
                        String heading = firstNonBlank(detail.title(), link.text(), resolvedUrl);
                        deepText.append(heading).append(": ").append(detailText, 0, Math.min(detailText.length(), 900));
                    }
                    followed++;
                } catch (Exception ignored) { }
            }
            page.setTitle(firstNonBlank(document.title(), page.getTitle()));
            page.setSummary(firstNonBlank(description, body, deepText.toString(), page.getSummary(), page.getSnippet()));
        } catch (Exception ignored) {
            // Search result data remains valid when a site blocks or times out.
        }
        return page;
    }

    private void updateSourceYield(String query, int resultCount, int added, int qualifiedAdded) {
        java.util.regex.Matcher matcher = java.util.regex.Pattern.compile("\\bsite:([^\\s]+)", java.util.regex.Pattern.CASE_INSENSITIVE).matcher(query == null ? "" : query);
        if (!matcher.find()) return;
        String domain = matcher.group(1).toLowerCase(Locale.ROOT);
        AiSearchSourceDO source = sourceMapper.selectOne(Wrappers.lambdaQuery(AiSearchSourceDO.class).eq(AiSearchSourceDO::getDomain, domain).last("LIMIT 1"));
        if (source == null) return;
        int usage = (source.getUsageCount() == null ? 0 : source.getUsageCount()) + 1;
        int companies = (source.getCompanyCount() == null ? 0 : source.getCompanyCount()) + resultCount;
        int qualified = (source.getQualifiedCompanyCount() == null ? 0 : source.getQualifiedCompanyCount()) + qualifiedAdded;
        int duplicates = (source.getDuplicateCount() == null ? 0 : source.getDuplicateCount()) + Math.max(0, resultCount - added);
        source.setUsageCount(usage); source.setCompanyCount(companies); source.setQualifiedCompanyCount(qualified); source.setDuplicateCount(duplicates);
        source.setYieldScore(java.math.BigDecimal.valueOf((qualified * 100.0) / Math.max(1, usage)));
        source.setAvgNewCompany(java.math.BigDecimal.valueOf(companies * 1.0 / usage));
        source.setAvgQualifiedCompany(java.math.BigDecimal.valueOf(qualified * 1.0 / usage));
        if (usage >= 3 && qualifiedAdded > 0 && "CANDIDATE".equals(source.getStatus())) source.setStatus("ACTIVE");
        if (usage >= 3 && added == 0 && qualifiedAdded == 0 && ("ACTIVE".equals(source.getStatus()) || "CANDIDATE".equals(source.getStatus()))) source.setStatus("LOW_YIELD");
        source.setLastUsedAt(LocalDateTime.now()); source.setLastTestedAt(LocalDateTime.now()); sourceMapper.updateById(source);
    }

    private int nextRound(Long taskId) {
        AiSearchFrontierDO frontier = frontierMapper.selectOne(Wrappers.lambdaQuery(AiSearchFrontierDO.class)
                .eq(AiSearchFrontierDO::getTaskId, taskId).orderByDesc(AiSearchFrontierDO::getLastRoundNo).last("LIMIT 1"));
        return frontier == null || frontier.getLastRoundNo() == null ? 1 : frontier.getLastRoundNo() + 1;
    }

    private void saveEvidence(AiSearchCompanyDO company, AiWebSearchResponse.WebPage page, Long sourceId, Long runId) {
        AiSearchCompanyEvidenceDO evidence = new AiSearchCompanyEvidenceDO();
        evidence.setCompanyId(company.getId()); evidence.setSourceId(sourceId); evidence.setRunId(runId);
        evidence.setUrl(page.getUrl()); evidence.setEvidenceType("WEB_SEARCH");
        evidence.setEvidenceText(firstNonBlank(page.getSummary(), page.getSnippet(), page.getTitle()));
        evidence.setCollectedAt(LocalDateTime.now()); evidenceMapper.insert(evidence);
    }

    private void saveBasicContact(AiSearchTaskDO task, AiSearchCompanyDO company, AiWebSearchResponse.WebPage page) {
        String text = firstNonBlank(page.getSummary(), page.getSnippet());
        String email = match(text, "[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\\.[A-Za-z]{2,}");
        String phone = match(text, "(?:\\+?\\d[\\d ()-]{7,}\\d)");
        String linkedin = text.toLowerCase(Locale.ROOT).contains("linkedin.com/") ? match(text, "https?://[^\\s]+linkedin\\.com/[^\\s]+") : null;
        if (email == null && phone == null && linkedin == null) return;
        if (contactMapper.selectCount(Wrappers.lambdaQuery(AiSearchContactDO.class)
                .eq(AiSearchContactDO::getCompanyId, company.getId())
                .and(w -> w.eq(email != null, AiSearchContactDO::getEmail, email)
                        .or().eq(phone != null, AiSearchContactDO::getPhone, phone)
                        .or().eq(linkedin != null, AiSearchContactDO::getLinkedinUrl, linkedin))) > 0) return;
        AiSearchContactDO contact = new AiSearchContactDO(); contact.setTaskId(task.getId()); contact.setCompanyId(company.getId());
        contact.setEmail(email); contact.setPhone(phone); contact.setLinkedinUrl(linkedin); contact.setVerificationStatus("UNKNOWN");
        contact.setAiRecommended(false); contact.setConfidence(java.math.BigDecimal.valueOf(.5)); contactMapper.insert(contact);
        AiSearchContactEvidenceDO evidence = new AiSearchContactEvidenceDO();
        evidence.setContactId(contact.getId()); evidence.setUrl(page.getUrl()); evidence.setEvidenceText(text);
        evidence.setConfidence(java.math.BigDecimal.valueOf(.5)); evidence.setCollectedAt(LocalDateTime.now());
        contactEvidenceMapper.insert(evidence);
    }

    private void discoverAdditionalContacts(AiSearchTaskDO task, AiSearchCompanyDO company, AiWebSearchResponse.WebPage page) {
        String evidence = firstNonBlank(page.getSummary(), page.getSnippet());
        if (evidence.length() < 40) return;
        try {
            var model = aiModelService.getRequiredDefaultModel(AiModelTypeEnum.CHAT.getType());
            String prompt = "从下面企业公开资料中提取所有明确出现的联系人。只输出JSON数组，每项包含name,jobTitle,email,phone,linkedinUrl；没有明确联系人则输出[]。不得猜测、不得把公司邮箱当联系人。"
                    + "优先关注职位：" + firstNonBlank(task.getPositionStrategy(), "采购、进口、老板、销售负责人") + "。资料：" + evidence;
            String text = aiModelService.getChatModel(model.getId()).call(new Prompt(new UserMessage(prompt)))
                    .getResult().getOutput().getText();
            JsonNode node = new ObjectMapper().readTree(stripJsonFence(text));
            if (!node.isArray()) return;
            for (JsonNode item : node) {
                String name = textValue(item, "name");
                String email = textValue(item, "email");
                String phone = textValue(item, "phone");
                String linkedin = textValue(item, "linkedinUrl");
                if (name == null && email == null && phone == null && linkedin == null) continue;
                boolean exists = contactMapper.selectCount(Wrappers.lambdaQuery(AiSearchContactDO.class)
                        .eq(AiSearchContactDO::getCompanyId, company.getId())
                        .and(w -> w.eq(email != null, AiSearchContactDO::getEmail, email)
                                .or().eq(phone != null, AiSearchContactDO::getPhone, phone)
                                .or().eq(linkedin != null, AiSearchContactDO::getLinkedinUrl, linkedin)
                                .or().eq(name != null, AiSearchContactDO::getNormalizedName, normalize(name)))) > 0;
                if (exists) continue;
                AiSearchContactDO contact = new AiSearchContactDO(); contact.setTaskId(task.getId()); contact.setCompanyId(company.getId());
                contact.setName(name); contact.setNormalizedName(normalize(name)); contact.setJobTitle(textValue(item, "jobTitle"));
                contact.setEmail(email); contact.setPhone(phone); contact.setLinkedinUrl(linkedin);
                contact.setVerificationStatus("UNKNOWN"); contact.setAiRecommended(false); contact.setConfidence(java.math.BigDecimal.valueOf(.5));
                contactMapper.insert(contact);
                AiSearchContactEvidenceDO ce = new AiSearchContactEvidenceDO(); ce.setContactId(contact.getId()); ce.setUrl(page.getUrl());
                ce.setEvidenceText(evidence); ce.setConfidence(java.math.BigDecimal.valueOf(.5)); ce.setCollectedAt(LocalDateTime.now()); contactEvidenceMapper.insert(ce);
            }
        } catch (Exception ignored) {
            // Basic public contact extraction remains available when AI enrichment fails.
        }
    }

    private String textValue(JsonNode node, String field) {
        JsonNode value = node == null ? null : node.get(field);
        if (value == null || !value.isTextual() || value.asText().isBlank()) return null;
        return value.asText().trim();
    }

    private String match(String text, String regex) {
        if (text == null) return null;
        java.util.regex.Matcher matcher = java.util.regex.Pattern.compile(regex, java.util.regex.Pattern.CASE_INSENSITIVE).matcher(text);
        return matcher.find() ? matcher.group().replaceAll("[),.;]+$", "") : null;
    }

    private void evaluateIcp(AiSearchTaskDO task, AiSearchCompanyDO company) {
        try {
            var model = aiModelService.getRequiredDefaultModel(AiModelTypeEnum.CHAT.getType());
            ChatModel chatModel = aiModelService.getChatModel(model.getId());
            String system = "你是企业客户筛选助手。只输出JSON对象：{\\\"level\\\":\\\"A/B/C/D\\\",\\\"score\\\":0,\\\"reason\\\":\\\"不超过40字\\\"}。"
                    + "A高度符合，B大概率符合，C相关但证据不足，D明显不符合。只能依据给出的证据，不要猜测。";
            String user = "目标：" + firstNonBlank(task.getUserGoal()) + "\\n客户类型："
                    + firstNonBlank(task.getCustomerType()) + "\\n国家：" + firstNonBlank(task.getTargetCountry())
                    + "\\n企业：" + company.getName() + "\\n官网：" + company.getWebsite()
                    + "\\n证据：" + firstNonBlank(company.getDescription());
            String content = chatModel.call(new Prompt(List.of(new SystemMessage(system), new UserMessage(user))))
                    .getResult().getOutput().getText();
            JsonNode json = new ObjectMapper().readTree(stripJsonFence(content));
            String level = json.path("level").asText(null);
            if (level != null && level.matches("[ABCD]")) company.setIcpLevel(level);
            if (json.has("score") && json.get("score").canConvertToInt()) company.setIcpScore(json.get("score").asInt());
            if (json.has("reason")) company.setCompanyNote(json.get("reason").asText());
        } catch (Exception ignored) {
            company.setIcpLevel("UNKNOWN");
        }
    }

    private String stripJsonFence(String value) {
        if (value == null) return "{}";
        String text = value.trim();
        if (text.startsWith("```")) {
            text = text.replaceFirst("^```(?:json)?\\s*", "").replaceFirst("\\s*```$", "");
        }
        return text;
    }

    private String languageFor(String country) {
        if (country == null) return "en";
        String value = country.toLowerCase(Locale.ROOT);
        if (value.contains("德国") || value.contains("germany") || value.contains("deutsch")) return "de";
        if (value.contains("法国") || value.contains("france") || value.contains("français")) return "fr";
        if (value.contains("西班牙") || value.contains("spain") || value.contains("españa")) return "es";
        if (value.contains("意大利") || value.contains("italy") || value.contains("italia")) return "it";
        if (value.contains("日本") || value.contains("japan")) return "ja";
        return "en";
    }

    private String firstNonBlank(String... values) {
        for (String value : values) {
            if (value != null && !value.isBlank()) return value.trim();
        }
        return "未命名企业";
    }

    private String normalize(String value) {
        return value == null ? null : value.trim().toLowerCase(Locale.ROOT).replaceAll("\\s+", " ");
    }

    private String canonicalDomain(String url) {
        try {
            String host = URI.create(url).getHost();
            return host == null ? url : host.replaceFirst("^www\\.", "").toLowerCase(Locale.ROOT);
        } catch (Exception ignored) {
            return url;
        }
    }
}
