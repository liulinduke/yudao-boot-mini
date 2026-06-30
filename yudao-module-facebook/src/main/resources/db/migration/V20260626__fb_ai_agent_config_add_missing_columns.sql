-- fb_ai_agent_config existed before AI Agent V1 fields were added.
-- CREATE TABLE IF NOT EXISTS will not patch an existing table, so add missing columns explicitly.

SET @sql = IF(
    (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'fb_ai_agent_config' AND COLUMN_NAME = 'agent_type') = 0,
    'ALTER TABLE fb_ai_agent_config ADD COLUMN agent_type VARCHAR(64) NULL COMMENT ''Agent类型''',
    'SELECT 1'
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql = IF(
    (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'fb_ai_agent_config' AND COLUMN_NAME = 'search_mode') = 0,
    'ALTER TABLE fb_ai_agent_config ADD COLUMN search_mode VARCHAR(32) NULL COMMENT ''搜索方式''',
    'SELECT 1'
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql = IF(
    (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'fb_ai_agent_config' AND COLUMN_NAME = 'export_product') = 0,
    'ALTER TABLE fb_ai_agent_config ADD COLUMN export_product VARCHAR(255) NULL COMMENT ''用户主营/出口产品''',
    'SELECT 1'
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql = IF(
    (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'fb_ai_agent_config' AND COLUMN_NAME = 'keyword_pool') = 0,
    'ALTER TABLE fb_ai_agent_config ADD COLUMN keyword_pool TEXT NULL COMMENT ''关键词池JSON''',
    'SELECT 1'
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql = IF(
    (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'fb_ai_agent_config' AND COLUMN_NAME = 'keyword_cursor') = 0,
    'ALTER TABLE fb_ai_agent_config ADD COLUMN keyword_cursor INT NOT NULL DEFAULT 0 COMMENT ''关键词轮询游标''',
    'SELECT 1'
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql = IF(
    (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'fb_ai_agent_config' AND COLUMN_NAME = 'keywords_per_run') = 0,
    'ALTER TABLE fb_ai_agent_config ADD COLUMN keywords_per_run INT NOT NULL DEFAULT 5 COMMENT ''每轮执行关键词数量''',
    'SELECT 1'
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql = IF(
    (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'fb_ai_agent_config' AND COLUMN_NAME = 'ai_keyword_expand_enabled') = 0,
    'ALTER TABLE fb_ai_agent_config ADD COLUMN ai_keyword_expand_enabled BIT(1) NOT NULL DEFAULT b''0'' COMMENT ''是否启用AI扩展关键词''',
    'SELECT 1'
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql = IF(
    (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'fb_ai_agent_config' AND COLUMN_NAME = 'ai_keyword_expand_count') = 0,
    'ALTER TABLE fb_ai_agent_config ADD COLUMN ai_keyword_expand_count INT NOT NULL DEFAULT 20 COMMENT ''AI扩展关键词数量''',
    'SELECT 1'
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql = IF(
    (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'fb_ai_agent_config' AND COLUMN_NAME = 'target_customer_count') = 0,
    'ALTER TABLE fb_ai_agent_config ADD COLUMN target_customer_count INT NOT NULL DEFAULT 1000 COMMENT ''目标客户数量''',
    'SELECT 1'
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql = IF(
    (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'fb_ai_agent_config' AND COLUMN_NAME = 'execute_frequency') = 0,
    'ALTER TABLE fb_ai_agent_config ADD COLUMN execute_frequency VARCHAR(32) NULL COMMENT ''执行频率''',
    'SELECT 1'
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql = IF(
    (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'fb_ai_agent_config' AND COLUMN_NAME = 'execute_time') = 0,
    'ALTER TABLE fb_ai_agent_config ADD COLUMN execute_time VARCHAR(8) NOT NULL DEFAULT ''09:00'' COMMENT ''每日执行时间''',
    'SELECT 1'
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql = IF(
    (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'fb_ai_agent_config' AND COLUMN_NAME = 'last_execute_time') = 0,
    'ALTER TABLE fb_ai_agent_config ADD COLUMN last_execute_time DATETIME NULL COMMENT ''最近一次自动调度执行时间''',
    'SELECT 1'
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql = IF(
    (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'fb_ai_agent_config' AND COLUMN_NAME = 'target_countries') = 0,
    'ALTER TABLE fb_ai_agent_config ADD COLUMN target_countries TEXT NULL COMMENT ''目标国家JSON''',
    'SELECT 1'
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql = IF(
    (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'fb_ai_agent_config' AND COLUMN_NAME = 'target_languages') = 0,
    'ALTER TABLE fb_ai_agent_config ADD COLUMN target_languages TEXT NULL COMMENT ''目标语言JSON''',
    'SELECT 1'
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql = IF(
    (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'fb_ai_agent_config' AND COLUMN_NAME = 'monitor_group_ids') = 0,
    'ALTER TABLE fb_ai_agent_config ADD COLUMN monitor_group_ids TEXT NULL COMMENT ''监控群组ID列表''',
    'SELECT 1'
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql = IF(
    (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'fb_ai_agent_config' AND COLUMN_NAME = 'touch_score_threshold') = 0,
    'ALTER TABLE fb_ai_agent_config ADD COLUMN touch_score_threshold INT NOT NULL DEFAULT 90 COMMENT ''触达评分阈值''',
    'SELECT 1'
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql = IF(
    (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'fb_ai_agent_config' AND COLUMN_NAME = 'lead_score_workflow_id') = 0,
    'ALTER TABLE fb_ai_agent_config ADD COLUMN lead_score_workflow_id BIGINT NULL COMMENT ''线索评分工作流ID''',
    'SELECT 1'
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql = IF(
    (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'fb_ai_agent_config' AND COLUMN_NAME = 'comment_workflow_id') = 0,
    'ALTER TABLE fb_ai_agent_config ADD COLUMN comment_workflow_id BIGINT NULL COMMENT ''评论生成工作流ID''',
    'SELECT 1'
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql = IF(
    (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'fb_ai_agent_config' AND COLUMN_NAME = 'dm_workflow_id') = 0,
    'ALTER TABLE fb_ai_agent_config ADD COLUMN dm_workflow_id BIGINT NULL COMMENT ''私信生成工作流ID''',
    'SELECT 1'
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql = IF(
    (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'fb_ai_agent_config' AND COLUMN_NAME = 'auto_comment_enabled') = 0,
    'ALTER TABLE fb_ai_agent_config ADD COLUMN auto_comment_enabled BIT(1) NOT NULL DEFAULT b''0'' COMMENT ''是否自动评论''',
    'SELECT 1'
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql = IF(
    (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'fb_ai_agent_config' AND COLUMN_NAME = 'auto_dm_enabled') = 0,
    'ALTER TABLE fb_ai_agent_config ADD COLUMN auto_dm_enabled BIT(1) NOT NULL DEFAULT b''0'' COMMENT ''是否自动私信''',
    'SELECT 1'
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql = IF(
    (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'fb_ai_agent_config' AND COLUMN_NAME = 'daily_comment_limit') = 0,
    'ALTER TABLE fb_ai_agent_config ADD COLUMN daily_comment_limit INT NOT NULL DEFAULT 50 COMMENT ''每日评论上限''',
    'SELECT 1'
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql = IF(
    (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'fb_ai_agent_config' AND COLUMN_NAME = 'daily_dm_limit') = 0,
    'ALTER TABLE fb_ai_agent_config ADD COLUMN daily_dm_limit INT NOT NULL DEFAULT 30 COMMENT ''每日私信上限''',
    'SELECT 1'
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql = IF(
    (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'fb_ai_agent_config' AND COLUMN_NAME = 'reply_delay_range') = 0,
    'ALTER TABLE fb_ai_agent_config ADD COLUMN reply_delay_range VARCHAR(128) NULL COMMENT ''回复延迟范围JSON''',
    'SELECT 1'
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql = IF(
    (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'fb_ai_agent_config' AND COLUMN_NAME = 'persona_type') = 0,
    'ALTER TABLE fb_ai_agent_config ADD COLUMN persona_type VARCHAR(64) NULL COMMENT ''AI业务员人设类型''',
    'SELECT 1'
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;
