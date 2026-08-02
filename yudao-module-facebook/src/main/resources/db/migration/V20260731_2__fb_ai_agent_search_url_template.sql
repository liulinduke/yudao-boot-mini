-- AI帖子获客、AI公共主页获客的链接搜索模板。
-- 使用 information_schema 兼容已有数据库，避免 MySQL 不支持 ADD COLUMN IF NOT EXISTS。
SET @sql = IF(
    (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
     WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'fb_ai_agent_config'
       AND COLUMN_NAME = 'search_url_template') = 0,
    'ALTER TABLE fb_ai_agent_config ADD COLUMN search_url_template TEXT NULL COMMENT ''关键词搜索链接模板''',
    'SELECT 1'
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;
