$sql = @"
CREATE TABLE IF NOT EXISTS `ai_api_key` (
    `id` BIGINT NOT NULL AUTO_INCREMENT COMMENT 'id',
    `name` VARCHAR(100) NOT NULL COMMENT 'name',
    `api_key` VARCHAR(255) NOT NULL COMMENT 'api key',
    `platform` VARCHAR(50) NOT NULL COMMENT 'platform',
    `url` VARCHAR(500) COMMENT 'API url',
    `status` TINYINT NOT NULL DEFAULT 1 COMMENT 'status',
    `create_time` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT 'create time',
    `update_time` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT 'update time',
    `create_by` BIGINT COMMENT 'creator',
    `update_by` BIGINT COMMENT 'updater',
    `deleted` TINYINT NOT NULL DEFAULT 0 COMMENT 'deleted',
    PRIMARY KEY (`id`),
    INDEX `idx_platform` (`platform`),
    INDEX `idx_status` (`status`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='AI API Key Table';

CREATE TABLE IF NOT EXISTS `ai_model` (
    `id` BIGINT NOT NULL AUTO_INCREMENT COMMENT 'id',
    `key_id` BIGINT COMMENT 'API key id',
    `name` VARCHAR(100) NOT NULL COMMENT 'model name',
    `model` VARCHAR(100) NOT NULL COMMENT 'model code',
    `platform` VARCHAR(50) NOT NULL COMMENT 'platform',
    `type` TINYINT NOT NULL COMMENT 'type',
    `sort` INT NOT NULL DEFAULT 0 COMMENT 'sort',
    `status` TINYINT NOT NULL DEFAULT 1 COMMENT 'status',
    `temperature` DECIMAL(5,2) COMMENT 'temperature',
    `max_tokens` INT COMMENT 'max tokens',
    `max_contexts` INT COMMENT 'max contexts',
    `create_time` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT 'create time',
    `update_time` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT 'update time',
    `create_by` BIGINT COMMENT 'creator',
    `update_by` BIGINT COMMENT 'updater',
    `deleted` TINYINT NOT NULL DEFAULT 0 COMMENT 'deleted',
    PRIMARY KEY (`id`),
    INDEX `idx_key_id` (`key_id`),
    INDEX `idx_platform` (`platform`),
    INDEX `idx_type` (`type`),
    INDEX `idx_status` (`status`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='AI Model Table';

CREATE TABLE IF NOT EXISTS `ai_tool` (
    `id` BIGINT NOT NULL AUTO_INCREMENT COMMENT 'id',
    `name` VARCHAR(100) NOT NULL COMMENT 'tool name',
    `description` VARCHAR(500) COMMENT 'description',
    `status` TINYINT NOT NULL DEFAULT 1 COMMENT 'status',
    `create_time` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT 'create time',
    `update_time` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT 'update time',
    `create_by` BIGINT COMMENT 'creator',
    `update_by` BIGINT COMMENT 'updater',
    `deleted` TINYINT NOT NULL DEFAULT 0 COMMENT 'deleted',
    PRIMARY KEY (`id`),
    UNIQUE KEY `uk_name` (`name`),
    INDEX `idx_status` (`status`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='AI Tool Table';

CREATE TABLE IF NOT EXISTS `ai_chat_role` (
    `id` BIGINT NOT NULL AUTO_INCREMENT COMMENT 'id',
    `name` VARCHAR(100) NOT NULL COMMENT 'role name',
    `avatar` VARCHAR(500) COMMENT 'avatar',
    `category` VARCHAR(50) COMMENT 'category',
    `description` VARCHAR(500) COMMENT 'description',
    `system_message` TEXT COMMENT 'system message',
    `user_id` BIGINT COMMENT 'user id',
    `model_id` BIGINT COMMENT 'model id',
    `knowledge_ids` TEXT COMMENT 'knowledge ids',
    `tool_ids` TEXT COMMENT 'tool ids',
    `mcp_client_names` TEXT COMMENT 'mcp client names',
    `public_status` TINYINT NOT NULL DEFAULT 0 COMMENT 'public',
    `sort` INT NOT NULL DEFAULT 0 COMMENT 'sort',
    `status` TINYINT NOT NULL DEFAULT 1 COMMENT 'status',
    `create_time` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT 'create time',
    `update_time` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT 'update time',
    `create_by` BIGINT COMMENT 'creator',
    `update_by` BIGINT COMMENT 'updater',
    `deleted` TINYINT NOT NULL DEFAULT 0 COMMENT 'deleted',
    PRIMARY KEY (`id`),
    INDEX `idx_user_id` (`user_id`),
    INDEX `idx_model_id` (`model_id`),
    INDEX `idx_public_status` (`public_status`),
    INDEX `idx_status` (`status`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='AI Chat Role Table';

CREATE TABLE IF NOT EXISTS `ai_chat_conversation` (
    `id` BIGINT NOT NULL AUTO_INCREMENT COMMENT 'id',
    `user_id` BIGINT NOT NULL COMMENT 'user id',
    `title` VARCHAR(200) NOT NULL DEFAULT 'New Conversation' COMMENT 'title',
    `pinned` TINYINT NOT NULL DEFAULT 0 COMMENT 'pinned',
    `pinned_time` DATETIME COMMENT 'pinned time',
    `role_id` BIGINT COMMENT 'role id',
    `model_id` BIGINT COMMENT 'model id',
    `model` VARCHAR(100) COMMENT 'model code',
    `system_message` TEXT COMMENT 'system message',
    `temperature` DECIMAL(5,2) COMMENT 'temperature',
    `max_tokens` INT COMMENT 'max tokens',
    `max_contexts` INT COMMENT 'max contexts',
    `create_time` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT 'create time',
    `update_time` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT 'update time',
    `create_by` BIGINT COMMENT 'creator',
    `update_by` BIGINT COMMENT 'updater',
    `deleted` TINYINT NOT NULL DEFAULT 0 COMMENT 'deleted',
    PRIMARY KEY (`id`),
    INDEX `idx_user_id` (`user_id`),
    INDEX `idx_role_id` (`role_id`),
    INDEX `idx_model_id` (`model_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='AI Chat Conversation Table';

CREATE TABLE IF NOT EXISTS `ai_chat_message` (
    `id` BIGINT NOT NULL AUTO_INCREMENT COMMENT 'id',
    `conversation_id` BIGINT NOT NULL COMMENT 'conversation id',
    `reply_id` BIGINT COMMENT 'reply id',
    `type` VARCHAR(50) NOT NULL COMMENT 'type',
    `user_id` BIGINT NOT NULL COMMENT 'user id',
    `role_id` BIGINT COMMENT 'role id',
    `model` VARCHAR(100) COMMENT 'model code',
    `model_id` BIGINT COMMENT 'model id',
    `content` LONGTEXT COMMENT 'content',
    `reasoning_content` LONGTEXT COMMENT 'reasoning content',
    `use_context` TINYINT NOT NULL DEFAULT 1 COMMENT 'use context',
    `segment_ids` TEXT COMMENT 'segment ids',
    `web_search_pages` LONGTEXT COMMENT 'web search pages',
    `attachment_urls` TEXT COMMENT 'attachment urls',
    `create_time` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT 'create time',
    `update_time` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT 'update time',
    `create_by` BIGINT COMMENT 'creator',
    `update_by` BIGINT COMMENT 'updater',
    `deleted` TINYINT NOT NULL DEFAULT 0 COMMENT 'deleted',
    PRIMARY KEY (`id`),
    INDEX `idx_conversation_id` (`conversation_id`),
    INDEX `idx_reply_id` (`reply_id`),
    INDEX `idx_user_id` (`user_id`),
    INDEX `idx_role_id` (`role_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='AI Chat Message Table';

CREATE TABLE IF NOT EXISTS `ai_knowledge` (
    `id` BIGINT NOT NULL AUTO_INCREMENT COMMENT 'id',
    `name` VARCHAR(100) NOT NULL COMMENT 'knowledge name',
    `description` VARCHAR(500) COMMENT 'description',
    `embedding_model_id` BIGINT COMMENT 'embedding model id',
    `embedding_model` VARCHAR(100) COMMENT 'embedding model',
    `top_k` INT NOT NULL DEFAULT 5 COMMENT 'topK',
    `similarity_threshold` DECIMAL(5,4) COMMENT 'similarity threshold',
    `status` TINYINT NOT NULL DEFAULT 1 COMMENT 'status',
    `create_time` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT 'create time',
    `update_time` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT 'update time',
    `create_by` BIGINT COMMENT 'creator',
    `update_by` BIGINT COMMENT 'updater',
    `deleted` TINYINT NOT NULL DEFAULT 0 COMMENT 'deleted',
    PRIMARY KEY (`id`),
    INDEX `idx_embedding_model_id` (`embedding_model_id`),
    INDEX `idx_status` (`status`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='AI Knowledge Table';

CREATE TABLE IF NOT EXISTS `ai_knowledge_document` (
    `id` BIGINT NOT NULL AUTO_INCREMENT COMMENT 'id',
    `knowledge_id` BIGINT NOT NULL COMMENT 'knowledge id',
    `name` VARCHAR(200) NOT NULL COMMENT 'document name',
    `url` VARCHAR(500) COMMENT 'url',
    `content` LONGTEXT COMMENT 'content',
    `content_length` INT COMMENT 'content length',
    `tokens` INT COMMENT 'tokens',
    `segment_max_tokens` INT COMMENT 'segment max tokens',
    `retrieval_count` INT NOT NULL DEFAULT 0 COMMENT 'retrieval count',
    `status` TINYINT NOT NULL DEFAULT 1 COMMENT 'status',
    `create_time` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT 'create time',
    `update_time` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT 'update time',
    `create_by` BIGINT COMMENT 'creator',
    `update_by` BIGINT COMMENT 'updater',
    `deleted` TINYINT NOT NULL DEFAULT 0 COMMENT 'deleted',
    PRIMARY KEY (`id`),
    INDEX `idx_knowledge_id` (`knowledge_id`),
    INDEX `idx_status` (`status`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='AI Knowledge Document Table';

CREATE TABLE IF NOT EXISTS `ai_knowledge_segment` (
    `id` BIGINT NOT NULL AUTO_INCREMENT COMMENT 'id',
    `knowledge_id` BIGINT NOT NULL COMMENT 'knowledge id',
    `document_id` BIGINT COMMENT 'document id',
    `content` LONGTEXT COMMENT 'content',
    `content_length` INT COMMENT 'content length',
    `vector_id` VARCHAR(255) COMMENT 'vector id',
    `tokens` INT COMMENT 'tokens',
    `retrieval_count` INT NOT NULL DEFAULT 0 COMMENT 'retrieval count',
    `status` TINYINT NOT NULL DEFAULT 1 COMMENT 'status',
    `create_time` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT 'create time',
    `update_time` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT 'update time',
    `create_by` BIGINT COMMENT 'creator',
    `update_by` BIGINT COMMENT 'updater',
    `deleted` TINYINT NOT NULL DEFAULT 0 COMMENT 'deleted',
    PRIMARY KEY (`id`),
    INDEX `idx_knowledge_id` (`knowledge_id`),
    INDEX `idx_document_id` (`document_id`),
    INDEX `idx_vector_id` (`vector_id`),
    INDEX `idx_status` (`status`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='AI Knowledge Segment Table';

CREATE TABLE IF NOT EXISTS `ai_image` (
    `id` BIGINT NOT NULL AUTO_INCREMENT COMMENT 'id',
    `user_id` BIGINT NOT NULL COMMENT 'user id',
    `prompt` TEXT NOT NULL COMMENT 'prompt',
    `platform` VARCHAR(50) NOT NULL COMMENT 'platform',
    `model_id` BIGINT COMMENT 'model id',
    `model` VARCHAR(100) COMMENT 'model code',
    `width` INT COMMENT 'width',
    `height` INT COMMENT 'height',
    `status` TINYINT NOT NULL DEFAULT 0 COMMENT 'status',
    `finish_time` DATETIME COMMENT 'finish time',
    `error_message` VARCHAR(1000) COMMENT 'error message',
    `pic_url` VARCHAR(500) COMMENT 'picture url',
    `public_status` TINYINT NOT NULL DEFAULT 0 COMMENT 'public',
    `options` LONGTEXT COMMENT 'options',
    `buttons` LONGTEXT COMMENT 'buttons',
    `task_id` VARCHAR(100) COMMENT 'task id',
    `create_time` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT 'create time',
    `update_time` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT 'update time',
    `create_by` BIGINT COMMENT 'creator',
    `update_by` BIGINT COMMENT 'updater',
    `deleted` TINYINT NOT NULL DEFAULT 0 COMMENT 'deleted',
    PRIMARY KEY (`id`),
    INDEX `idx_user_id` (`user_id`),
    INDEX `idx_platform` (`platform`),
    INDEX `idx_status` (`status`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='AI Image Table';

CREATE TABLE IF NOT EXISTS `ai_mind_map` (
    `id` BIGINT NOT NULL AUTO_INCREMENT COMMENT 'id',
    `user_id` BIGINT NOT NULL COMMENT 'user id',
    `platform` VARCHAR(50) NOT NULL COMMENT 'platform',
    `model_id` BIGINT COMMENT 'model id',
    `model` VARCHAR(100) COMMENT 'model code',
    `prompt` TEXT COMMENT 'prompt',
    `generated_content` LONGTEXT COMMENT 'generated content',
    `error_message` VARCHAR(1000) COMMENT 'error message',
    `create_time` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT 'create time',
    `update_time` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT 'update time',
    `create_by` BIGINT COMMENT 'creator',
    `update_by` BIGINT COMMENT 'updater',
    `deleted` TINYINT NOT NULL DEFAULT 0 COMMENT 'deleted',
    PRIMARY KEY (`id`),
    INDEX `idx_user_id` (`user_id`),
    INDEX `idx_platform` (`platform`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='AI Mind Map Table';

CREATE TABLE IF NOT EXISTS `ai_music` (
    `id` BIGINT NOT NULL AUTO_INCREMENT COMMENT 'id',
    `user_id` BIGINT NOT NULL COMMENT 'user id',
    `title` VARCHAR(200) COMMENT 'title',
    `lyric` TEXT COMMENT 'lyric',
    `image_url` VARCHAR(500) COMMENT 'image url',
    `audio_url` VARCHAR(500) COMMENT 'audio url',
    `video_url` VARCHAR(500) COMMENT 'video url',
    `status` TINYINT NOT NULL DEFAULT 0 COMMENT 'status',
    `generate_mode` TINYINT COMMENT 'generate mode',
    `description` VARCHAR(500) COMMENT 'description',
    `platform` VARCHAR(50) COMMENT 'platform',
    `model` VARCHAR(100) COMMENT 'model code',
    `tags` TEXT COMMENT 'tags',
    `duration` DECIMAL(10,2) COMMENT 'duration',
    `public_status` TINYINT NOT NULL DEFAULT 0 COMMENT 'public',
    `task_id` VARCHAR(100) COMMENT 'task id',
    `error_message` VARCHAR(1000) COMMENT 'error message',
    `create_time` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT 'create time',
    `update_time` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT 'update time',
    `create_by` BIGINT COMMENT 'creator',
    `update_by` BIGINT COMMENT 'updater',
    `deleted` TINYINT NOT NULL DEFAULT 0 COMMENT 'deleted',
    PRIMARY KEY (`id`),
    INDEX `idx_user_id` (`user_id`),
    INDEX `idx_platform` (`platform`),
    INDEX `idx_status` (`status`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='AI Music Table';

CREATE TABLE IF NOT EXISTS `ai_write` (
    `id` BIGINT NOT NULL AUTO_INCREMENT COMMENT 'id',
    `user_id` BIGINT NOT NULL COMMENT 'user id',
    `type` TINYINT NOT NULL COMMENT 'type',
    `platform` VARCHAR(50) NOT NULL COMMENT 'platform',
    `model_id` BIGINT COMMENT 'model id',
    `model` VARCHAR(100) COMMENT 'model code',
    `prompt` TEXT COMMENT 'prompt',
    `generated_content` LONGTEXT COMMENT 'generated content',
    `original_content` LONGTEXT COMMENT 'original content',
    `length` TINYINT COMMENT 'length',
    `format` TINYINT COMMENT 'format',
    `tone` TINYINT COMMENT 'tone',
    `language` TINYINT COMMENT 'language',
    `error_message` VARCHAR(1000) COMMENT 'error message',
    `create_time` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT 'create time',
    `update_time` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT 'update time',
    `create_by` BIGINT COMMENT 'creator',
    `update_by` BIGINT COMMENT 'updater',
    `deleted` TINYINT NOT NULL DEFAULT 0 COMMENT 'deleted',
    PRIMARY KEY (`id`),
    INDEX `idx_user_id` (`user_id`),
    INDEX `idx_platform` (`platform`),
    INDEX `idx_type` (`type`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='AI Write Table';

CREATE TABLE IF NOT EXISTS `ai_workflow` (
    `id` BIGINT NOT NULL AUTO_INCREMENT COMMENT 'id',
    `name` VARCHAR(100) NOT NULL COMMENT 'name',
    `code` VARCHAR(100) NOT NULL COMMENT 'code',
    `graph` LONGTEXT COMMENT 'graph',
    `remark` VARCHAR(500) COMMENT 'remark',
    `status` TINYINT NOT NULL DEFAULT 1 COMMENT 'status',
    `create_time` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT 'create time',
    `update_time` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT 'update time',
    `create_by` BIGINT COMMENT 'creator',
    `update_by` BIGINT COMMENT 'updater',
    `deleted` TINYINT NOT NULL DEFAULT 0 COMMENT 'deleted',
    PRIMARY KEY (`id`),
    UNIQUE KEY `uk_code` (`code`),
    INDEX `idx_status` (`status`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='AI Workflow Table';
"@

$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText("d:\Work\yudao-boot-mini\yudao-module-ai\src\main\resources\schema\mysql-schema.sql", $sql, $utf8NoBom)

Write-Host "SQL file created successfully"

& "C:\Program Files\MySQL\MySQL Server 8.0\bin\mysql.exe" -h 127.0.0.1 -P 3306 -u root -pmxline1808 ruoyi-vue-pro -e $sql

if ($LASTEXITCODE -eq 0) {
    Write-Host "All tables created successfully!"
} else {
    Write-Host "Failed to create tables. Exit code: $LASTEXITCODE"
}