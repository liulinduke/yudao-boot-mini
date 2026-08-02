SET @schedule_times_exists = (
    SELECT COUNT(*)
    FROM information_schema.columns
    WHERE table_schema = DATABASE()
      AND table_name = 'facebook_message_monitor_account'
      AND column_name = 'schedule_times'
);
SET @schedule_times_sql = IF(
    @schedule_times_exists = 0,
    'ALTER TABLE facebook_message_monitor_account ADD COLUMN schedule_times VARCHAR(255) NULL COMMENT ''每日定时接收时间，多个时间用逗号分隔'' AFTER check_interval_minutes',
    'SELECT 1'
);
PREPARE schedule_times_stmt FROM @schedule_times_sql;
EXECUTE schedule_times_stmt;
DEALLOCATE PREPARE schedule_times_stmt;
