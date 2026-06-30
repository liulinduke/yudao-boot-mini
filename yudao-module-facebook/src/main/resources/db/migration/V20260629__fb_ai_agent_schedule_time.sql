-- AI主页获客 Agent 每日执行时间。

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
