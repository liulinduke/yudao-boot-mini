-- 深度采集命中已有潜客时，user.task_id 会被 upsertDeepCollectedUser 改写为 deep 任务 id，
-- 导致 page 任务日志无法按 task_id 统计自己原始采集到的客户。新增 original_task_id 永久保留
-- 首轮主页采集任务 id，供 FbAiAgentServiceImpl.getPageDiscoveryLeadIds 按关键词行统计。
ALTER TABLE `fb_collect_user`
    ADD COLUMN `original_task_id` bigint NULL COMMENT '首轮主页采集任务ID（深度采集后保留）' AFTER `task_id`;

-- 历史数据回填：现有用户按 task_id 回填 original_task_id。已被 deep 任务改写 task_id 的用户
-- 仍能通过 facebook_collect_detail.source_user_id 反查其原始 page 任务：deep 任务的 collectDetail
-- 记录了原始用户 id，page 任务的 collectDetail 与 facebook_collect.total_collected_count 保留计数。
-- 这里直接用现有 task_id 回填，对新启动的 Agent 立刻生效；老数据若 task_id 已被 deep 改写，
-- original_task_id 暂留 NULL，等下一次 page 任务采集时自动写入。
UPDATE `fb_collect_user` SET `original_task_id` = `task_id` WHERE `original_task_id` IS NULL;

ALTER TABLE `fb_collect_user`
    ADD INDEX `idx_original_task_id` (`original_task_id`);
