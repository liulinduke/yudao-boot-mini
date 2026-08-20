package cn.iocoder.yudao.module.ai.service.model;

import cn.iocoder.yudao.module.ai.enums.model.AiPlatformEnum;
import cn.iocoder.yudao.module.ai.framework.ai.core.model.AiModelFactory;
import cn.iocoder.yudao.module.ai.framework.ai.core.model.midjourney.api.MidjourneyApi;
import cn.iocoder.yudao.module.ai.framework.ai.core.model.suno.api.SunoApi;
import cn.iocoder.yudao.framework.common.enums.CommonStatusEnum;
import cn.iocoder.yudao.framework.common.pojo.PageResult;
import cn.iocoder.yudao.framework.common.util.object.BeanUtils;
import cn.iocoder.yudao.module.ai.controller.admin.model.vo.model.AiModelPageReqVO;
import cn.iocoder.yudao.module.ai.controller.admin.model.vo.model.AiModelSaveReqVO;
import cn.iocoder.yudao.module.ai.dal.dataobject.model.AiApiKeyDO;
import cn.iocoder.yudao.module.ai.dal.dataobject.model.AiModelDO;
import cn.iocoder.yudao.module.ai.dal.mysql.model.AiChatMapper;
import com.agentsflex.llm.deepseek.DeepseekConfig;
import com.agentsflex.llm.deepseek.DeepseekLlm;
import com.agentsflex.llm.deepseek.DeepseekLlmUtil;
import com.agentsflex.core.llm.ChatOptions;
import com.agentsflex.core.llm.response.AiMessageResponse;
import com.agentsflex.core.prompt.Prompt;
import com.agentsflex.llm.ollama.OllamaLlm;
import com.agentsflex.llm.ollama.OllamaLlmConfig;
import com.agentsflex.llm.qwen.QwenLlm;
import com.agentsflex.llm.qwen.QwenLlmConfig;
import dev.tinyflow.core.Tinyflow;
import jakarta.annotation.Resource;
import lombok.extern.slf4j.Slf4j;
import org.springframework.ai.chat.model.ChatModel;
import org.springframework.ai.embedding.EmbeddingModel;
import org.springframework.ai.image.ImageModel;
import org.springframework.ai.vectorstore.SimpleVectorStore;
import org.springframework.ai.vectorstore.VectorStore;
import org.springframework.stereotype.Service;
import org.springframework.validation.annotation.Validated;

import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;

import static cn.iocoder.yudao.framework.common.exception.util.ServiceExceptionUtil.exception0;
import static cn.iocoder.yudao.framework.common.exception.util.ServiceExceptionUtil.exception;
import static cn.iocoder.yudao.module.ai.enums.ErrorCodeConstants.*;

/**
 * AI 模型 Service 实现类
 *
 * @author fansili
 */
@Service
@Validated
@Slf4j
public class AiModelServiceImpl implements AiModelService {

    @Resource
    private AiApiKeyService apiKeyService;

    @Resource
    private AiChatMapper modelMapper;

    @Resource
    private AiModelFactory modelFactory;

    @Override
    public Long createModel(AiModelSaveReqVO createReqVO) {
        // 1. 校验
        AiPlatformEnum.validatePlatform(createReqVO.getPlatform());
        apiKeyService.validateApiKey(createReqVO.getKeyId());

        // 2. 插入
        AiModelDO model = BeanUtils.toBean(createReqVO, AiModelDO.class);
        modelMapper.insert(model);
        return model.getId();
    }

    @Override
    public void updateModel(AiModelSaveReqVO updateReqVO) {
        // 1. 校验
        validateModelExists(updateReqVO.getId());
        AiPlatformEnum.validatePlatform(updateReqVO.getPlatform());
        apiKeyService.validateApiKey(updateReqVO.getKeyId());

        // 2. 更新
        AiModelDO updateObj = BeanUtils.toBean(updateReqVO, AiModelDO.class);
        modelMapper.updateById(updateObj);
    }

    @Override
    public void deleteModel(Long id) {
        // 校验存在
        validateModelExists(id);
        // 删除
        modelMapper.deleteById(id);
    }

    private AiModelDO validateModelExists(Long id) {
        AiModelDO model = modelMapper.selectById(id);
        if (modelMapper.selectById(id) == null) {
            throw exception(MODEL_NOT_EXISTS);
        }
        return model;
    }

    @Override
    public AiModelDO getModel(Long id) {
        return modelMapper.selectById(id);
    }

    @Override
    public AiModelDO getRequiredDefaultModel(Integer type) {
        AiModelDO model = modelMapper.selectFirstByStatus(type, CommonStatusEnum.ENABLE.getStatus());
        if (model == null) {
            throw exception(MODEL_DEFAULT_NOT_EXISTS);
        }
        return model;
    }

    @Override
    public PageResult<AiModelDO> getModelPage(AiModelPageReqVO pageReqVO) {
        return modelMapper.selectPage(pageReqVO);
    }

    @Override
    public AiModelDO validateModel(Long id) {
        AiModelDO model = validateModelExists(id);
        if (CommonStatusEnum.isDisable(model.getStatus())) {
            throw exception(MODEL_DISABLE);
        }
        return model;
    }

    @Override
    public List<AiModelDO> getModelListByStatusAndType(Integer status, Integer type, String platform) {
        return modelMapper.selectListByStatusAndType(status, type, platform);
    }

    // ========== 与 Spring AI 集成 ==========

    @Override
    public ChatModel getChatModel(Long id) {
        AiModelDO model = validateModel(id);
        AiApiKeyDO apiKey = apiKeyService.validateApiKey(model.getKeyId());
        AiPlatformEnum platform = AiPlatformEnum.validatePlatform(apiKey.getPlatform());
        return modelFactory.getOrCreateChatModel(platform, apiKey.getApiKey(), apiKey.getUrl());
    }

    @Override
    public ImageModel getImageModel(Long id) {
        AiModelDO model = validateModel(id);
        AiApiKeyDO apiKey = apiKeyService.validateApiKey(model.getKeyId());
        AiPlatformEnum platform = AiPlatformEnum.validatePlatform(apiKey.getPlatform());
        return modelFactory.getOrCreateImageModel(platform, apiKey.getApiKey(), apiKey.getUrl());
    }

    @Override
    public MidjourneyApi getMidjourneyApi(Long id) {
        AiModelDO model = validateModel(id);
        AiApiKeyDO apiKey = apiKeyService.validateApiKey(model.getKeyId());
        return modelFactory.getOrCreateMidjourneyApi(apiKey.getApiKey(), apiKey.getUrl());
    }

    @Override
    public SunoApi getSunoApi() {
        AiApiKeyDO apiKey = apiKeyService.getRequiredDefaultApiKey(
                AiPlatformEnum.SUNO.getPlatform(), CommonStatusEnum.ENABLE.getStatus());
        return modelFactory.getOrCreateSunoApi(apiKey.getApiKey(), apiKey.getUrl());
    }

    @Override
    public VectorStore getOrCreateVectorStore(Long id, Map<String, Class<?>> metadataFields) {
        // 获取模型 + 密钥
        AiModelDO model = validateModel(id);
        AiApiKeyDO apiKey = apiKeyService.validateApiKey(model.getKeyId());
        AiPlatformEnum platform = AiPlatformEnum.validatePlatform(apiKey.getPlatform());

        // 创建或获取 EmbeddingModel 对象
        EmbeddingModel embeddingModel = modelFactory.getOrCreateEmbeddingModel(
                platform, apiKey.getApiKey(), apiKey.getUrl(), model.getModel());

        // 创建或获取 VectorStore 对象
         return modelFactory.getOrCreateVectorStore(SimpleVectorStore.class, embeddingModel, metadataFields);
//         return modelFactory.getOrCreateVectorStore(QdrantVectorStore.class, embeddingModel, metadataFields);
//         return modelFactory.getOrCreateVectorStore(RedisVectorStore.class, embeddingModel, metadataFields);
//         return modelFactory.getOrCreateVectorStore(MilvusVectorStore.class, embeddingModel, metadataFields);
    }

    // TODO @lesan：是不是返回 Llm 对象会好点哈？
    @Override
    public void getLLmProvider4Tinyflow(Tinyflow tinyflow, Long modelId) {
        if (modelId == null) {
            throw exception0(MODEL_NOT_EXISTS.getCode(), "AI 工作流大模型节点未配置模型");
        }
        AiModelDO model = validateModel(modelId);
        AiApiKeyDO apiKey = apiKeyService.validateApiKey(model.getKeyId());
        AiPlatformEnum platform = AiPlatformEnum.validatePlatform(apiKey.getPlatform());
        switch (platform) {
            // TODO @lesan 考虑到未来不需要使用agents-flex 现在仅测试通义千问
            // TODO @lesan：【重要】是不是可以实现一个 SpringAiLlm，这样的话，内部全部用它就好了。只实现 chat 部分；这样，就把 flex 作为一个 agent 框架，内部调用，还是 spring ai 相关的。成本可能低一点？！
            case TONG_YI:
                QwenLlmConfig qwenLlmConfig = new QwenLlmConfig();
                qwenLlmConfig.setApiKey(apiKey.getApiKey());
                qwenLlmConfig.setModel(model.getModel());
                // TODO @lesan：这个有点奇怪。。。如果一个链式里，有多个模型，咋整呀。。。
                tinyflow.setLlmProvider(id -> new QwenLlm(qwenLlmConfig));
                break;
            case DEEP_SEEK:
                DeepseekConfig deepseekConfig = new DeepseekConfig();
                deepseekConfig.setApiKey(apiKey.getApiKey());
                deepseekConfig.setModel(model.getModel());
                if (apiKey.getUrl() != null && !apiKey.getUrl().isBlank()) {
                    deepseekConfig.setEndpoint(apiKey.getUrl());
                }
                tinyflow.setLlmProvider(id -> new LoggingDeepseekLlm(deepseekConfig));
                break;
            case OLLAMA:
                OllamaLlmConfig ollamaLlmConfig = new OllamaLlmConfig();
                ollamaLlmConfig.setEndpoint(apiKey.getUrl());
                ollamaLlmConfig.setModel(model.getModel());
                tinyflow.setLlmProvider(id -> new OllamaLlm(ollamaLlmConfig));
                break;
            default:
                throw exception0(MODEL_USE_TYPE_ERROR.getCode(),
                        "AI 工作流暂不支持 {} 平台，请在工作流大模型节点选择通义千问、DeepSeek 或 Ollama 模型", platform.getName());
        }
    }

    private static class LoggingDeepseekLlm extends DeepseekLlm {

        private final DeepseekConfig config;

        private LoggingDeepseekLlm(DeepseekConfig config) {
            super(config);
            this.config = config;
        }

        @Override
        public AiMessageResponse chat(Prompt prompt, ChatOptions options) {
            // AgentsFlex DeepSeek serializer omits ChatOptions.topK; forward the
            // workflow node's value through extra so the HTTP body contains top_k.
            if (options.getTopK() != null) {
                options.addExtra("top_k", options.getTopK());
            }
            // DeepSeek V4 defaults to high-effort reasoning. Workflow nodes that only
            // generate structured data should return directly instead of producing a
            // long reasoning_content response.
            options.addExtra("thinking", Map.of("type", "disabled"));
            String payload = DeepseekLlmUtil.promptToPayload(prompt, config, options, false);
            Map<String, String> headers = new LinkedHashMap<>();
            headers.put("Content-Type", "application/json");
            headers.put("Accept", "application/json");
            headers.put("Authorization", "Bearer " + maskApiKey(config.getApiKey()));
            log.info("DeepSeek最终HTTP请求, url={}, headers={}, body={}",
                    config.getEndpoint() + "/chat/completions", headers, payload);
            AiMessageResponse response = super.chat(prompt, options);
            log.info("DeepSeek最终HTTP响应, isError={}, errorCode={}, errorType={}, errorMessage={}, raw={}",
                    response.isError(), response.getErrorCode(), response.getErrorType(),
                    response.getErrorMessage(), response.getResponse());
            return response;
        }

        private static String maskApiKey(String apiKey) {
            if (apiKey == null || apiKey.length() <= 8) {
                return "****";
            }
            return apiKey.substring(0, 4) + "****" + apiKey.substring(apiKey.length() - 4);
        }
    }

}
