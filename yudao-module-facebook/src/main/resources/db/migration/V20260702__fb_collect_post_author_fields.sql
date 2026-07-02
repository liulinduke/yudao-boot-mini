ALTER TABLE fb_collect_post
    ADD COLUMN post_author_id varchar(128) NULL COMMENT '发帖人 Facebook 用户ID' AFTER post_user,
    ADD COLUMN post_author_url varchar(512) NULL COMMENT '发帖人主页链接' AFTER post_author_id;
