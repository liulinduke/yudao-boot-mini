-- Facebook账号界面语言代码。旧 language 字段保留用于兼容历史数据。
SET @column_exists := (
    SELECT COUNT(*)
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'facebook_account'
      AND COLUMN_NAME = 'language_code'
);
SET @sql := IF(@column_exists = 0,
    'ALTER TABLE facebook_account ADD COLUMN language_code VARCHAR(32) NULL COMMENT ''Facebook界面语言代码'' AFTER language',
    'SELECT 1');
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;
