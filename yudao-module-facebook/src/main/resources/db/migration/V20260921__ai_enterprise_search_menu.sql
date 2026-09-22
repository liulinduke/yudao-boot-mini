-- 独立的 AI 全网企业获客菜单，不归属于 Facebook 菜单。
INSERT INTO system_menu
    (id, name, permission, type, sort, parent_id, path, icon, component, component_name,
     status, visible, keep_alive, always_show, creator, create_time, updater, update_time, deleted)
SELECT 2099000000000000001, 'AI全网企业获客', '', 1, 35, 0, '/ai-search', 'ep:magic-stick', NULL, NULL,
       0, 1, 1, 1, 'system', NOW(), 'system', NOW(), 0
WHERE NOT EXISTS (SELECT 1 FROM system_menu WHERE id = 2099000000000000001);

INSERT INTO system_menu
    (id, name, permission, type, sort, parent_id, path, icon, component, component_name,
     status, visible, keep_alive, always_show, creator, create_time, updater, update_time, deleted)
SELECT 2099000000000000002, '获客任务', 'ai:search:task:query', 2, 1, 2099000000000000001,
       'task', 'ep:search', 'facebook/aiSearch/index', 'AiEnterpriseSearch',
       0, 1, 1, 1, 'system', NOW(), 'system', NOW(), 0
WHERE NOT EXISTS (SELECT 1 FROM system_menu WHERE id = 2099000000000000002);

INSERT INTO system_menu
    (id, name, permission, type, sort, parent_id, path, icon, component, component_name,
     status, visible, keep_alive, always_show, creator, create_time, updater, update_time, deleted)
SELECT 2099000000000000003, '搜索渠道', 'ai:search:source:query', 2, 2, 2099000000000000001,
       'source', 'ep:connection', 'facebook/aiSearch/source', 'AiSearchSource',
       0, 0, 1, 1, 'system', NOW(), 'system', NOW(), 0
WHERE NOT EXISTS (SELECT 1 FROM system_menu WHERE id = 2099000000000000003);
