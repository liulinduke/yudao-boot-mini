package cn.iocoder.yudao.module.ai.service.workflow;

import cn.hutool.core.util.ObjUtil;
import cn.hutool.core.util.StrUtil;
import cn.iocoder.yudao.framework.common.pojo.PageResult;
import cn.iocoder.yudao.framework.common.util.object.BeanUtils;
import cn.iocoder.yudao.module.ai.controller.admin.workflow.vo.AiWorkflowPageReqVO;
import cn.iocoder.yudao.module.ai.controller.admin.workflow.vo.AiWorkflowSaveReqVO;
import cn.iocoder.yudao.module.ai.controller.admin.workflow.vo.AiWorkflowTestReqVO;
import cn.iocoder.yudao.module.ai.dal.dataobject.workflow.AiWorkflowDO;
import cn.iocoder.yudao.module.ai.dal.mysql.workflow.AiWorkflowMapper;
import cn.iocoder.yudao.module.ai.service.model.AiModelService;
import com.alibaba.fastjson.JSONArray;
import com.alibaba.fastjson.JSONObject;
import com.agentsflex.core.prompt.template.TextPromptTemplate;
import dev.tinyflow.core.Tinyflow;
import jakarta.annotation.Resource;
import lombok.extern.slf4j.Slf4j;
import org.springframework.stereotype.Service;

import java.util.LinkedHashMap;
import java.util.LinkedHashSet;
import java.util.Map;
import java.util.Set;
import java.util.regex.Matcher;
import java.util.regex.Pattern;

import static cn.iocoder.yudao.framework.common.exception.util.ServiceExceptionUtil.exception;
import static cn.iocoder.yudao.module.ai.enums.ErrorCodeConstants.WORKFLOW_CODE_EXISTS;
import static cn.iocoder.yudao.module.ai.enums.ErrorCodeConstants.WORKFLOW_NOT_EXISTS;

/**
 * AI 工作流 Service 实现类
 *
 * @author lesan
 */
@Service
@Slf4j
public class AiWorkflowServiceImpl implements AiWorkflowService {

    private static final Pattern PROMPT_VARIABLE_PATTERN = Pattern.compile("\\{\\{\\s*([A-Za-z_][A-Za-z0-9_]*)\\s*}}");

    @Resource
    private AiWorkflowMapper workflowMapper;

    @Resource
    private AiModelService apiModelService;

    @Override
    public Long createWorkflow(AiWorkflowSaveReqVO createReqVO) {
        // 1. 参数校验
        validateCodeUnique(null, createReqVO.getCode());

        // 2. 插入工作流配置
        AiWorkflowDO workflow = BeanUtils.toBean(createReqVO, AiWorkflowDO.class);
        workflowMapper.insert(workflow);
        return workflow.getId();
    }

    @Override
    public void updateWorkflow(AiWorkflowSaveReqVO updateReqVO) {
        // 1. 参数校验
        validateWorkflowExists(updateReqVO.getId());
        validateCodeUnique(updateReqVO.getId(), updateReqVO.getCode());

        // 2. 更新工作流配置
        AiWorkflowDO workflow = BeanUtils.toBean(updateReqVO, AiWorkflowDO.class);
        workflowMapper.updateById(workflow);
    }

    @Override
    public void deleteWorkflow(Long id) {
        // 1. 校验存在
        validateWorkflowExists(id);

        // 2. 删除工作流配置
        workflowMapper.deleteById(id);
    }

    private AiWorkflowDO validateWorkflowExists(Long id) {
        if (ObjUtil.isNull(id)) {
            throw exception(WORKFLOW_NOT_EXISTS);
        }
        AiWorkflowDO workflow = workflowMapper.selectById(id);
        if (ObjUtil.isNull(workflow)) {
            throw exception(WORKFLOW_NOT_EXISTS);
        }
        return workflow;
    }

    private void validateCodeUnique(Long id, String code) {
        if (StrUtil.isBlank(code)) {
            return;
        }
        AiWorkflowDO workflow = workflowMapper.selectByCode(code);
        if (ObjUtil.isNull(workflow)) {
            return;
        }
        if (ObjUtil.isNull(id)) {
            throw exception(WORKFLOW_CODE_EXISTS);
        }
        if (ObjUtil.notEqual(workflow.getId(), id)) {
            throw exception(WORKFLOW_CODE_EXISTS);
        }
    }

    @Override
    public AiWorkflowDO getWorkflow(Long id) {
        return workflowMapper.selectById(id);
    }

    @Override
    public PageResult<AiWorkflowDO> getWorkflowPage(AiWorkflowPageReqVO pageReqVO) {
        return workflowMapper.selectPage(pageReqVO);
    }

    @Override
    public Object testWorkflow(AiWorkflowTestReqVO testReqVO) {
        // 加载 graph
        String graph = testReqVO.getGraph() != null ? testReqVO.getGraph()
                : validateWorkflowExists(testReqVO.getId()).getGraph();

        return executeWorkflowGraph(graph, testReqVO.getParams(), testReqVO.getId());
    }

    @Override
    public Object executeWorkflow(Long id, Map<String, Object> params) {
        AiWorkflowDO workflow = validateWorkflowExists(id);
        return executeWorkflowGraph(workflow.getGraph(), params, id);
    }

    private Object executeWorkflowGraph(String graph, Map<String, Object> params, Long workflowId) {
        graph = normalizeWorkflowGraphParameters(workflowId, graph);
        logWorkflowPrompts(workflowId, graph, params);

        // 构建 TinyFlow 执行链
        Tinyflow tinyflow = parseFlowParam(graph);

        // 执行
        return tinyflow.toChain().executeForResult(params);
    }

    /**
     * 兼容旧图：开始节点声明参数后，LLM 节点仍需要显式配置输入参数映射。
     * 如果 LLM 节点只在 Prompt 中写了 {{xxx}}，但 data.parameters 为空，TinyFlow 不会把开始节点参数传进去。
     */
    private String normalizeWorkflowGraphParameters(Long workflowId, String graph) {
        try {
            JSONObject json = JSONObject.parseObject(graph);
            JSONArray nodes = json.getJSONArray("nodes");
            if (nodes == null || nodes.isEmpty()) {
                return graph;
            }

            JSONObject startNode = null;
            for (int i = 0; i < nodes.size(); i++) {
                JSONObject node = nodes.getJSONObject(i);
                if ("startNode".equals(node.getString("type"))) {
                    startNode = node;
                    break;
                }
            }
            if (startNode == null || startNode.getJSONObject("data") == null) {
                return graph;
            }

            Map<String, JSONObject> startParameters = new LinkedHashMap<>();
            JSONArray startParameterArray = startNode.getJSONObject("data").getJSONArray("parameters");
            if (startParameterArray == null || startParameterArray.isEmpty()) {
                return graph;
            }
            for (int i = 0; i < startParameterArray.size(); i++) {
                JSONObject parameter = startParameterArray.getJSONObject(i);
                if (StrUtil.isNotBlank(parameter.getString("name"))) {
                    startParameters.put(parameter.getString("name"), parameter);
                }
            }

            boolean changed = false;
            for (int i = 0; i < nodes.size(); i++) {
                JSONObject node = nodes.getJSONObject(i);
                if (!"llmNode".equals(node.getString("type"))) {
                    continue;
                }
                JSONObject data = node.getJSONObject("data");
                if (data == null) {
                    continue;
                }
                JSONArray parameters = data.getJSONArray("parameters");
                if (parameters != null && !parameters.isEmpty()) {
                    continue;
                }

                Set<String> variableNames = extractPromptVariables(data.getString("systemPrompt"), data.getString("userPrompt"));
                JSONArray generatedParameters = new JSONArray();
                for (String variableName : variableNames) {
                    JSONObject startParameter = startParameters.get(variableName);
                    if (startParameter == null) {
                        continue;
                    }
                    JSONObject parameter = new JSONObject();
                    parameter.put("id", node.getString("id") + "-param-" + variableName);
                    parameter.put("name", variableName);
                    parameter.put("dataType", startParameter.getString("dataType"));
                    parameter.put("refType", "ref");
                    parameter.put("ref", startNode.getString("id") + "." + variableName);
                    parameter.put("description", startParameter.getString("description"));
                    generatedParameters.add(parameter);
                }

                if (!generatedParameters.isEmpty()) {
                    data.put("parameters", generatedParameters);
                    changed = true;
                    log.info("AI工作流自动补齐LLM节点输入参数, workflowId={}, nodeId={}, parameters={}",
                            workflowId, node.getString("id"), generatedParameters);
                }
            }
            return changed ? json.toJSONString() : graph;
        } catch (Exception ex) {
            log.warn("AI工作流自动补齐输入参数失败, workflowId={}, reason={}", workflowId, ex.getMessage(), ex);
            return graph;
        }
    }

    private Set<String> extractPromptVariables(String... prompts) {
        Set<String> variableNames = new LinkedHashSet<>();
        for (String prompt : prompts) {
            if (StrUtil.isBlank(prompt)) {
                continue;
            }
            Matcher matcher = PROMPT_VARIABLE_PATTERN.matcher(prompt);
            while (matcher.find()) {
                variableNames.add(matcher.group(1));
            }
        }
        return variableNames;
    }

    private void logWorkflowPrompts(Long workflowId, String graph, Map<String, Object> params) {
        try {
            JSONObject json = JSONObject.parseObject(graph);
            JSONArray nodeArr = json.getJSONArray("nodes");
            if (nodeArr == null) {
                log.info("AI工作流执行参数, workflowId={}, params={}, graphNodes=0", workflowId, params);
                return;
            }
            for (int i = 0; i < nodeArr.size(); i++) {
                JSONObject node = nodeArr.getJSONObject(i);
                if (!"llmNode".equals(node.getString("type"))) {
                    continue;
                }
                JSONObject data = node.getJSONObject("data");
                String systemPrompt = data == null ? null : data.getString("systemPrompt");
                String userPrompt = data == null ? null : data.getString("userPrompt");
                String renderedSystemPrompt = renderPrompt(systemPrompt, params);
                String renderedUserPrompt = renderPrompt(userPrompt, params);
                log.info("AI工作流LLM节点, workflowId={}, nodeId={}, title={}, llmId={}, systemPrompt={}, userPrompt={}, params={}",
                        workflowId,
                        node.getString("id"),
                        data == null ? null : data.getString("title"),
                        data == null ? null : data.getLong("llmId"),
                        systemPrompt,
                        userPrompt,
                        params);
                log.info("AI工作流LLM最终请求, workflowId={}, nodeId={}, renderedSystemPrompt={}, renderedUserPrompt={}",
                        workflowId, node.getString("id"), renderedSystemPrompt, renderedUserPrompt);
            }
        } catch (Exception ex) {
            log.warn("打印AI工作流提示词失败, workflowId={}, reason={}", workflowId, ex.getMessage(), ex);
        }
    }

    private String renderPrompt(String prompt, Map<String, Object> params) {
        if (StrUtil.isBlank(prompt)) {
            return "";
        }
        try {
            return TextPromptTemplate.of(prompt).formatToString(params);
        } catch (Exception ex) {
            log.warn("渲染AI工作流提示词失败, prompt={}, params={}, reason={}", prompt, params, ex.getMessage());
            return prompt;
        }
    }

    private Tinyflow parseFlowParam(String graph) {
        // TODO @lesan：可以使用 jackson 哇？
        JSONObject json = JSONObject.parseObject(graph);
        JSONArray nodeArr = json.getJSONArray("nodes");
        Tinyflow tinyflow = new Tinyflow(json.toJSONString());
        for (int i = 0; i < nodeArr.size(); i++) {
            JSONObject node = nodeArr.getJSONObject(i);
            switch (node.getString("type")) {
                case "llmNode":
                    JSONObject data = node.getJSONObject("data");
                    apiModelService.getLLmProvider4Tinyflow(tinyflow, data.getLong("llmId"));
                    break;
                case "internalNode":
                    break;
                default:
                    break;
            }
        }
        return tinyflow;
    }

}
