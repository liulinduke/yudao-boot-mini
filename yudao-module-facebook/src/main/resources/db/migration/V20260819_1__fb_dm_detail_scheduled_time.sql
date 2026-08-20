ALTER TABLE facebook_dm_task_detail
    ADD COLUMN scheduled_time DATETIME NULL COMMENT '计划进入账号执行队列的时间' AFTER send_time;

CREATE INDEX idx_fb_dm_detail_scheduled
    ON facebook_dm_task_detail (tenant_id, status, scheduled_time, deleted);
