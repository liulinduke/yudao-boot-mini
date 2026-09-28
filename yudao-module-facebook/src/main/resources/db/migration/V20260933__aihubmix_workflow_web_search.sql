INSERT INTO system_dict_data
(sort, label, value, dict_type, status, color_type, css_class, remark,
 creator, create_time, updater, update_time, deleted)
SELECT 90, 'AIHubMix', 'AIHubMix', 'ai_platform', 0, '', '', 'AIHubMix OpenAI 兼容平台',
       '1', NOW(), '1', NOW(), b'0'
WHERE NOT EXISTS (
    SELECT 1 FROM system_dict_data WHERE dict_type = 'ai_platform' AND value = 'AIHubMix' AND deleted = b'0'
);

INSERT INTO ai_workflow
(id, name, code, graph, remark, status, create_time, update_time, deleted, creator, updater, tenant_id)
SELECT 2099000000000000102, 'AI全网企业获客联网搜索 V1', 'ai_enterprise_search_web_v1',
'{"edges":[{"id":"edge-search-start-llm","source":"start-enterprise-search","target":"llm-enterprise-search"}],"nodes":[{"id":"start-enterprise-search","type":"startNode","position":{"x":80,"y":120},"data":{"title":"开始","parameters":[{"id":"query","name":"query","dataType":"String","required":true,"description":"企业获客联网搜索词"},{"id":"count","name":"count","dataType":"Number","required":true,"description":"返回结果数量"}]}},{"id":"llm-enterprise-search","type":"llmNode","position":{"x":420,"y":120},"data":{"title":"AIHubMix 联网搜索","llmId":1,"temperature":0,"parameters":[{"id":"p-query","name":"query","refType":"ref","ref":"start-enterprise-search.query","dataType":"String"},{"id":"p-count","name":"count","refType":"ref","ref":"start-enterprise-search.count","dataType":"Number"}],"userPrompt":"query={{query}}\\ncount={{count}}","systemPrompt":"你是企业获客联网搜索助手。根据 query 执行真实互联网搜索，优先寻找符合搜索词的企业官方网站和可靠企业目录。只使用搜索结果中真实存在的网页链接，不得编造企业、网址或事实。最多返回 count 条结果。只输出 JSON 数组，每项字段为 name、title、url、snippet、summary；找不到结果时输出 []。不要 markdown 或数组外文本。"}}]}',
'AI 全网企业获客首轮联网搜索工作流。请在 AI 工作流页面将大模型节点模型选择为 AIHubMix 的联网模型（模型标识带 :surfing）。',
0, NOW(), NOW(), b'0', '1', '1', 1
WHERE NOT EXISTS (SELECT 1 FROM ai_workflow WHERE code = 'ai_enterprise_search_web_v1' AND deleted = b'0');
