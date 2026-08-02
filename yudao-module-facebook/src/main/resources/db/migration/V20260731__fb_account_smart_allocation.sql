-- Facebook账号长期操作统计与任务分配模式
CREATE TABLE IF NOT EXISTS facebook_account_action_stat (
    id BIGINT NOT NULL COMMENT '主键',
    tenant_id BIGINT NOT NULL DEFAULT 0 COMMENT '租户编号',
    account_id BIGINT NOT NULL COMMENT 'Facebook账号ID',
    action_type VARCHAR(32) NOT NULL COMMENT '操作类型：dm/repost/join_group/comment/follow/collect',
    total_task_count BIGINT NOT NULL DEFAULT 0 COMMENT '累计成功任务数',
    total_action_count BIGINT NOT NULL DEFAULT 0 COMMENT '累计成功操作数',
    total_collect_count BIGINT NOT NULL DEFAULT 0 COMMENT '累计采集条数',
    last_execute_time DATETIME NULL COMMENT '最近执行时间',
    last_success_time DATETIME NULL COMMENT '最近成功时间',
    creator VARCHAR(64) DEFAULT '' COMMENT '创建者',
    create_time DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    updater VARCHAR(64) DEFAULT '' COMMENT '更新者',
    update_time DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
    deleted BIT NOT NULL DEFAULT b'0' COMMENT '是否删除',
    PRIMARY KEY (id),
    UNIQUE KEY uk_tenant_account_action (tenant_id, account_id, action_type),
    KEY idx_tenant_account (tenant_id, account_id),
    KEY idx_tenant_last_execute (tenant_id, last_execute_time)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='Facebook账号长期操作统计';

-- 兼容 MySQL 5.7：ADD COLUMN IF NOT EXISTS 在部分版本不支持。
-- 用 information_schema 判断后再执行，脚本可重复运行。
SET @sql = IF(
    EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = DATABASE()
          AND table_name = 'fb_ai_agent_config'
          AND column_name = 'account_selection_mode'
    ),
    'SELECT 1',
    'ALTER TABLE fb_ai_agent_config ADD COLUMN account_selection_mode VARCHAR(16) NOT NULL DEFAULT ''AUTO'' COMMENT ''账号分配模式：AUTO/MANUAL'''
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql = IF(
    EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = DATABASE()
          AND table_name = 'facebook_collect'
          AND column_name = 'account_selection_mode'
    ),
    'SELECT 1',
    'ALTER TABLE facebook_collect ADD COLUMN account_selection_mode VARCHAR(16) NOT NULL DEFAULT ''AUTO'' COMMENT ''账号分配模式：AUTO/MANUAL'''
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql = IF(
    EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = DATABASE()
          AND table_name = 'fb_operation_task'
          AND column_name = 'account_selection_mode'
    ),
    'SELECT 1',
    'ALTER TABLE fb_operation_task ADD COLUMN account_selection_mode VARCHAR(16) NOT NULL DEFAULT ''AUTO'' COMMENT ''账号分配模式：AUTO/MANUAL'''
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql = IF(
    EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = DATABASE()
          AND table_name = 'facebook_dm_task'
          AND column_name = 'account_selection_mode'
    ),
    'SELECT 1',
    'ALTER TABLE facebook_dm_task ADD COLUMN account_selection_mode VARCHAR(16) NOT NULL DEFAULT ''AUTO'' COMMENT ''账号分配模式：AUTO/MANUAL'''
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;
