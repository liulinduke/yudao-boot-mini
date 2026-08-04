-- 群组选择器按租户、任务、状态、账号和加组时间筛选，并按账号聚合。
ALTER TABLE fb_operation_add_group_result
    ADD INDEX idx_fb_add_group_selector (tenant_id, task_id, join_status, account_id, join_time, group_id),
    ADD INDEX idx_fb_add_group_group (tenant_id, group_id, join_status, account_id);

ALTER TABLE fb_operation_task
    ADD INDEX idx_fb_operation_task_type_tenant (tenant_id, task_type, id);
