-- A collect task may contain multiple newline-separated target URLs.
-- Keep the complete task-level summary while individual URLs remain in
-- facebook_collect_detail.search_url for execution.
ALTER TABLE facebook_collect
    MODIFY COLUMN search_url TEXT NULL COMMENT '采集链接（支持多个换行分隔）';
