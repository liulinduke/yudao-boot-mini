package cn.iocoder.yudao.module.ai.service.tenant;

import cn.hutool.core.collection.CollUtil;
import cn.iocoder.yudao.framework.common.enums.CommonStatusEnum;
import cn.iocoder.yudao.framework.tenant.core.util.TenantUtils;
import cn.iocoder.yudao.module.ai.dal.dataobject.model.AiApiKeyDO;
import cn.iocoder.yudao.module.ai.dal.dataobject.model.AiModelDO;
import cn.iocoder.yudao.module.ai.dal.dataobject.workflow.AiWorkflowDO;
import cn.iocoder.yudao.module.ai.dal.mysql.model.AiApiKeyMapper;
import cn.iocoder.yudao.module.ai.dal.mysql.model.AiChatMapper;
import cn.iocoder.yudao.module.ai.dal.mysql.workflow.AiWorkflowMapper;
import cn.iocoder.yudao.module.system.service.tenant.TenantService;
import com.alibaba.fastjson.JSONArray;
import com.alibaba.fastjson.JSONObject;
import com.baomidou.mybatisplus.core.conditions.query.LambdaQueryWrapper;
import jakarta.annotation.Resource;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import java.util.HashMap;
import java.util.List;
import java.util.Map;

import static cn.iocoder.yudao.framework.common.exception.util.ServiceExceptionUtil.exception;
import static cn.iocoder.yudao.module.ai.enums.ErrorCodeConstants.TENANT_AI_CONFIG_CAN_NOT_INITIALIZE_TEMPLATE;
import static cn.iocoder.yudao.module.ai.enums.ErrorCodeConstants.TENANT_AI_CONFIG_MODEL_MAPPING_NOT_EXISTS;
import static cn.iocoder.yudao.module.ai.enums.ErrorCodeConstants.TENANT_AI_CONFIG_TEMPLATE_NOT_EXISTS;

/**
 * 从模板租户复制 AI 工作流、模型和 API Key 配置。
 */
@Service
public class AiTenantConfigServiceImpl implements AiTenantConfigService {

    private static final Long TEMPLATE_TENANT_ID = 1L;

    @Resource
    private TenantService tenantService;
    @Resource
    private AiApiKeyMapper apiKeyMapper;
    @Resource
    private AiChatMapper modelMapper;
    @Resource
    private AiWorkflowMapper workflowMapper;

    @Override
    @Transactional(rollbackFor = Exception.class)
    public void initializeTenantConfig(Long tenantId) {
        tenantService.validTenant(tenantId);
        if (TEMPLATE_TENANT_ID.equals(tenantId)) {
            throw exception(TENANT_AI_CONFIG_CAN_NOT_INITIALIZE_TEMPLATE);
        }

        TemplateData template = TenantUtils.execute(TEMPLATE_TENANT_ID, this::loadTemplate);
        if (CollUtil.isEmpty(template.apiKeys) || CollUtil.isEmpty(template.models) || CollUtil.isEmpty(template.workflows)) {
            throw exception(TENANT_AI_CONFIG_TEMPLATE_NOT_EXISTS);
        }
        TenantUtils.execute(tenantId, () -> copyToTenant(template));
    }

    private TemplateData loadTemplate() {
        return new TemplateData(apiKeyMapper.selectList(), modelMapper.selectList(), workflowMapper.selectList());
    }

    private void copyToTenant(TemplateData template) {
        Map<Long, Long> apiKeyIdMapping = copyApiKeys(template.apiKeys);
        Map<Long, Long> modelIdMapping = copyModels(template.models, apiKeyIdMapping);
        copyWorkflows(template.workflows, modelIdMapping);
    }

    private Map<Long, Long> copyApiKeys(List<AiApiKeyDO> templateApiKeys) {
        Map<Long, Long> idMapping = new HashMap<>();
        for (AiApiKeyDO template : templateApiKeys) {
            AiApiKeyDO target = apiKeyMapper.selectOne(new LambdaQueryWrapper<AiApiKeyDO>()
                    .eq(AiApiKeyDO::getPlatform, template.getPlatform())
                    .eq(AiApiKeyDO::getName, template.getName())
                    .last("LIMIT 1"));
            if (target == null) {
                target = AiApiKeyDO.builder()
                        .name(template.getName())
                        .apiKey("")
                        .platform(template.getPlatform())
                        .url(template.getUrl())
                        .status(CommonStatusEnum.DISABLE.getStatus())
                        .build();
                apiKeyMapper.insert(target);
            }
            idMapping.put(template.getId(), target.getId());
        }
        return idMapping;
    }

    private Map<Long, Long> copyModels(List<AiModelDO> templateModels, Map<Long, Long> apiKeyIdMapping) {
        Map<Long, Long> idMapping = new HashMap<>();
        for (AiModelDO template : templateModels) {
            Long targetKeyId = apiKeyIdMapping.get(template.getKeyId());
            if (targetKeyId == null) {
                throw exception(TENANT_AI_CONFIG_MODEL_MAPPING_NOT_EXISTS, template.getKeyId());
            }
            AiModelDO target = modelMapper.selectOne(new LambdaQueryWrapper<AiModelDO>()
                    .eq(AiModelDO::getKeyId, targetKeyId)
                    .eq(AiModelDO::getModel, template.getModel())
                    .eq(AiModelDO::getType, template.getType())
                    .last("LIMIT 1"));
            if (target == null) {
                target = AiModelDO.builder()
                        .keyId(targetKeyId)
                        .name(template.getName())
                        .model(template.getModel())
                        .platform(template.getPlatform())
                        .type(template.getType())
                        .sort(template.getSort())
                        .status(CommonStatusEnum.DISABLE.getStatus())
                        .temperature(template.getTemperature())
                        .maxTokens(template.getMaxTokens())
                        .maxContexts(template.getMaxContexts())
                        .build();
                modelMapper.insert(target);
            }
            idMapping.put(template.getId(), target.getId());
        }
        return idMapping;
    }

    private void copyWorkflows(List<AiWorkflowDO> templateWorkflows, Map<Long, Long> modelIdMapping) {
        for (AiWorkflowDO template : templateWorkflows) {
            if (workflowMapper.selectByCode(template.getCode()) != null) {
                continue;
            }
            AiWorkflowDO target = new AiWorkflowDO();
            target.setName(template.getName());
            target.setCode(template.getCode());
            target.setGraph(replaceModelIds(template.getGraph(), modelIdMapping));
            target.setRemark(template.getRemark());
            target.setStatus(template.getStatus());
            workflowMapper.insert(target);
        }
    }

    private String replaceModelIds(String graph, Map<Long, Long> modelIdMapping) {
        if (graph == null || graph.isBlank()) {
            return graph;
        }
        JSONObject json = JSONObject.parseObject(graph);
        JSONArray nodes = json.getJSONArray("nodes");
        if (nodes == null) {
            return graph;
        }
        for (int i = 0; i < nodes.size(); i++) {
            JSONObject node = nodes.getJSONObject(i);
            if (!"llmNode".equals(node.getString("type"))) {
                continue;
            }
            JSONObject data = node.getJSONObject("data");
            if (data == null || !data.containsKey("llmId")) {
                continue;
            }
            Long sourceModelId = data.getLong("llmId");
            Long targetModelId = modelIdMapping.get(sourceModelId);
            if (targetModelId == null) {
                throw exception(TENANT_AI_CONFIG_MODEL_MAPPING_NOT_EXISTS, sourceModelId);
            }
            data.put("llmId", targetModelId);
        }
        return json.toJSONString();
    }

    private record TemplateData(List<AiApiKeyDO> apiKeys, List<AiModelDO> models, List<AiWorkflowDO> workflows) {
    }

}
