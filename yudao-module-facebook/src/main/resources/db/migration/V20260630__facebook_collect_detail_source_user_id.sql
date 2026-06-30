ALTER TABLE `facebook_collect_detail`
    ADD COLUMN `source_user_id` bigint NULL COMMENT '来源资源库用户ID' AFTER `search_url`;

