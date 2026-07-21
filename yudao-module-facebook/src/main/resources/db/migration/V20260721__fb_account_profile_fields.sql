ALTER TABLE facebook_account
    ADD COLUMN avatar_url VARCHAR(1000) NULL COMMENT 'Facebook头像地址' AFTER id,
    ADD COLUMN cover_url VARCHAR(1000) NULL COMMENT 'Facebook主页封面地址' AFTER avatar_url,
    ADD COLUMN profile_nickname VARCHAR(255) NULL COMMENT 'Facebook主页昵称' AFTER cover_url,
    ADD COLUMN profile_signature VARCHAR(500) NULL COMMENT 'Facebook个人签名' AFTER profile_nickname,
    ADD COLUMN profile_update_status VARCHAR(20) NULL COMMENT '资料上传状态' AFTER profile_signature,
    ADD COLUMN profile_update_time DATETIME NULL COMMENT '资料上传时间' AFTER profile_update_status,
    ADD COLUMN profile_update_error VARCHAR(1000) NULL COMMENT '资料上传失败原因' AFTER profile_update_time;
