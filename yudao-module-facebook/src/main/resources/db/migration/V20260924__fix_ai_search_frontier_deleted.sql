-- 补齐 AI 搜索空间表的逻辑删除字段。
ALTER TABLE ai_search_frontier
  ADD COLUMN deleted bit(1) NOT NULL DEFAULT b'0';
