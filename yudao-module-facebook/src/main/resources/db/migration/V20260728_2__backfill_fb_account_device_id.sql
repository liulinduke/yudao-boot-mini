-- 为历史账号补齐固定设备 ID。
-- 使用账号主键保证同一账号稳定且不同账号不会重复，不修改已有设备 ID。
UPDATE facebook_account
SET device_id = id
WHERE device_id IS NULL OR device_id = 0;
