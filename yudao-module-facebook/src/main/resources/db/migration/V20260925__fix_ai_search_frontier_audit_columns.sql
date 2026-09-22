-- 补齐 AI 搜索空间表的创建人和更新人字段。
ALTER TABLE ai_search_frontier
  ADD COLUMN creator varchar(64) DEFAULT NULL,
  ADD COLUMN updater varchar(64) DEFAULT NULL;
