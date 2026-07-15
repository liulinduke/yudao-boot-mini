ALTER TABLE facebook_message_monitor_account
    ADD COLUMN IF NOT EXISTS messenger_unread_count INT NOT NULL DEFAULT 0 COMMENT 'Messenger红圈未读数' AFTER error_message,
    ADD COLUMN IF NOT EXISTS notification_unread_count INT NOT NULL DEFAULT 0 COMMENT '通知红圈未读数' AFTER messenger_unread_count,
    ADD COLUMN IF NOT EXISTS last_badge_check_time DATETIME NULL COMMENT '红圈最后读取时间' AFTER notification_unread_count;
