ALTER TABLE ai_search_task
  ADD COLUMN company_product varchar(1000) DEFAULT NULL COMMENT 'User company or product',
  ADD COLUMN lead_keywords varchar(1000) DEFAULT NULL COMMENT 'User lead keywords',
  ADD COLUMN search_snapshot TEXT NULL COMMENT 'User-confirmed AI search expansion snapshot';

ALTER TABLE ai_search_run
  ADD COLUMN target_qualified_count int NOT NULL DEFAULT 0 COMMENT 'New qualified companies targeted in this run',
  ADD COLUMN qualified_count_before int NOT NULL DEFAULT 0 COMMENT 'Qualified company baseline at run start';

ALTER TABLE ai_search_query
  ADD COLUMN run_id bigint DEFAULT NULL COMMENT 'Search run id',
  ADD COLUMN dimensions_json TEXT DEFAULT NULL COMMENT 'Search dimensions used for this query';

ALTER TABLE ai_search_company_evidence
  ADD COLUMN source_id bigint DEFAULT NULL COMMENT 'Source registry id',
  ADD COLUMN run_id bigint DEFAULT NULL COMMENT 'Search run id';

UPDATE ai_workflow
SET graph = JSON_SET(CAST(graph AS JSON), '$.nodes[1].data.systemPrompt',
    '你是全网企业获客搜索扩展专家。围绕用户输入生成可编辑的搜索建议，不要执行联网搜索。只输出 JSON 对象，字段为 keywords、productTerms、customerRoles、purchasingTerms、localTerms、applications、scenes、ecommerceChannelTerms、upstreamDownstreamTerms，值均为字符串数组。keywords最多6项，scenes最多4项。不得改变用户产品、国家和客户类型；不得编造企业或网址。') ,
    update_time = NOW(), updater = '1'
WHERE code = 'ai_enterprise_search_expand_v1' AND deleted = b'0';

UPDATE ai_workflow
SET graph = JSON_SET(CAST(graph AS JSON), '$.nodes[1].data.systemPrompt',
    '你是企业获客联网搜索执行工具。根据输入 query 和 count 执行互联网搜索，只返回搜索服务实际检索到的网页。不得根据常识补写企业、URL、标题或事实。输出 JSON 对象：{"results":[{"name":"","title":"","url":"","snippet":"","summary":"","source":""}]}；source填写网页结果的来源域名或搜索来源标识。没有真实结果时 results 返回空数组。不要输出数组外说明。'),
    update_time = NOW(), updater = '1'
WHERE code = 'ai_enterprise_search_web_v1' AND deleted = b'0';
