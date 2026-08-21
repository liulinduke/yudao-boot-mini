ALTER TABLE fb_operation_task_detail
    ADD COLUMN scheduled_time DATETIME NULL COMMENT '计划进入账号执行队列的时间' AFTER status;

CREATE INDEX idx_fb_operation_detail_scheduled
    ON fb_operation_task_detail (tenant_id, status, scheduled_time, deleted);

INSERT INTO infra_job (
    name, status, handler_name, handler_param, cron_expression,
    retry_count, retry_interval, monitor_timeout, creator, create_time, updater, update_time, deleted
)
SELECT
    'Facebook刷粉到期调度 Job', 1, 'fbFollowTaskDispatchJob', '', '0/10 * * * * ?',
    0, 0, 0, 'system', NOW(), 'system', NOW(), b'0'
WHERE NOT EXISTS (
    SELECT 1 FROM infra_job WHERE handler_name = 'fbFollowTaskDispatchJob' AND deleted = b'0'
);
