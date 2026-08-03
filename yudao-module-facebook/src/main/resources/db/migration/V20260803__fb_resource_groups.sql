CREATE TABLE IF NOT EXISTS facebook_resource_group (
    id BIGINT NOT NULL PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    resource_type VARCHAR(16) NOT NULL COMMENT 'LEAD/GROUP/POST',
    is_default TINYINT NOT NULL DEFAULT 0 COMMENT '是否为未分组',
    tenant_id BIGINT NOT NULL DEFAULT 0,
    creator VARCHAR(64) NULL,
    create_time DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updater VARCHAR(64) NULL,
    update_time DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    deleted BIT NOT NULL DEFAULT 0,
    UNIQUE KEY uk_resource_group_type_name (tenant_id, resource_type, name, deleted)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='Facebook资源分组';

SET @resource_group_columns = (
    SELECT COUNT(*) FROM information_schema.columns
    WHERE table_schema = DATABASE() AND table_name = 'fb_collect_user'
      AND column_name = 'resource_group_id'
);
SET @resource_group_sql = IF(@resource_group_columns = 0,
    'ALTER TABLE fb_collect_user ADD COLUMN resource_group_id BIGINT NULL COMMENT ''资源分组ID'' AFTER group_id',
    'SELECT 1');
PREPARE resource_group_stmt FROM @resource_group_sql;
EXECUTE resource_group_stmt;
DEALLOCATE PREPARE resource_group_stmt;

SET @resource_group_columns = (SELECT COUNT(*) FROM information_schema.columns WHERE table_schema = DATABASE() AND table_name = 'facebook_collect' AND column_name = 'resource_group_id');
SET @resource_group_sql = IF(@resource_group_columns = 0, 'ALTER TABLE facebook_collect ADD COLUMN resource_group_id BIGINT NULL COMMENT ''采集结果资源分组ID'' AFTER account_selection_mode', 'SELECT 1');
PREPARE resource_group_stmt FROM @resource_group_sql;
EXECUTE resource_group_stmt;
DEALLOCATE PREPARE resource_group_stmt;

SET @resource_group_columns = (
    SELECT COUNT(*) FROM information_schema.columns
    WHERE table_schema = DATABASE() AND table_name = 'fb_collect_group'
      AND column_name = 'resource_group_id'
);
SET @resource_group_sql = IF(@resource_group_columns = 0,
    'ALTER TABLE fb_collect_group ADD COLUMN resource_group_id BIGINT NULL COMMENT ''资源分组ID'' AFTER group_id',
    'SELECT 1');
PREPARE resource_group_stmt FROM @resource_group_sql;
EXECUTE resource_group_stmt;
DEALLOCATE PREPARE resource_group_stmt;

SET @resource_group_columns = (
    SELECT COUNT(*) FROM information_schema.columns
    WHERE table_schema = DATABASE() AND table_name = 'fb_collect_post'
      AND column_name = 'resource_group_id'
);
SET @resource_group_sql = IF(@resource_group_columns = 0,
    'ALTER TABLE fb_collect_post ADD COLUMN resource_group_id BIGINT NULL COMMENT ''资源分组ID'' AFTER group_id',
    'SELECT 1');
PREPARE resource_group_stmt FROM @resource_group_sql;
EXECUTE resource_group_stmt;
DEALLOCATE PREPARE resource_group_stmt;
