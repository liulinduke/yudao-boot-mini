ALTER TABLE facebook_message_monitor_account
    ADD COLUMN receive_enabled TINYINT NOT NULL DEFAULT 0 AFTER account_id,
    ADD COLUMN online_status TINYINT NOT NULL DEFAULT 0 AFTER receive_enabled;

UPDATE facebook_message_monitor_account
SET receive_enabled = CASE WHEN mode IN ('realtime', 'scheduled') THEN 1 ELSE 0 END,
    online_status = 0,
    mode = CASE WHEN mode IN ('realtime', 'scheduled') THEN 'scheduled' ELSE 'disabled' END;
