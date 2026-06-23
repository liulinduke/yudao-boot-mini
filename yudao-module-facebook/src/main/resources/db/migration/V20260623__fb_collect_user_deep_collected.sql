ALTER TABLE fb_collect_user
    ADD COLUMN deep_collected bit(1) NOT NULL DEFAULT b'0' COMMENT '是否已深度采集';
