-- 修正顶级菜单路径，确保动态路由使用绝对路径。
UPDATE system_menu
SET path = '/ai-search', update_time = NOW(), updater = 'system'
WHERE id = 2099000000000000001 AND deleted = 0;
