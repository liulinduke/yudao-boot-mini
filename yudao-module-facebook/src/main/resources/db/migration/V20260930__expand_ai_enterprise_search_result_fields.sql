-- 扩展工作流返回分类，供第二步分别展示产品词、客户角色和当地语言。
UPDATE ai_workflow
SET graph = REPLACE(graph,
  '只输出JSON：{\\"keywords\\":[\\"关键词1\\"],\\"scenes\\":[\\"场景1\\"]}，不要输出markdown或其他文字。',
  '只输出JSON：{\\"keywords\\":[\\"关键词1\\"],\\"scenes\\":[\\"场景1\\"],\\"productTerms\\":[\\"产品词1\\"],\\"customerRoles\\":[\\"客户角色1\\"],\\"localTerms\\":[\\"当地语言词1\\"]}，不要输出markdown或其他文字。'),
    update_time = NOW(), updater = '1'
WHERE code = 'ai_enterprise_search_expand_v1' AND deleted = b'0';
