-- MySQL 会将迁移 SQL 中的 \n 解释为真实换行；恢复为 JSON 合法的转义换行。
UPDATE ai_workflow
SET graph = REPLACE(REPLACE(graph, CHAR(13), ''), CHAR(10), '\\n'), update_time = NOW(), updater = '1'
WHERE code = 'ai_enterprise_search_expand_v1' AND deleted = b'0';
