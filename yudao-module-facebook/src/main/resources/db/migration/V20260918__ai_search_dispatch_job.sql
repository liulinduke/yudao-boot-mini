-- 注册 AI 全网企业获客定期搜索调度器；任务自身的周期字段决定是否到期执行。
INSERT INTO infra_job (
    name, status, handler_name, handler_param, cron_expression,
    retry_count, retry_interval, monitor_timeout, creator, create_time, updater, update_time, deleted
)
SELECT
    'AI企业获客定期搜索 Job', 1, 'aiSearchTaskDispatchJob', '', '0/30 * * * * ?',
    0, 0, 0, 'system', NOW(), 'system', NOW(), b'0'
WHERE NOT EXISTS (
    SELECT 1 FROM infra_job WHERE handler_name = 'aiSearchTaskDispatchJob' AND deleted = b'0'
);
