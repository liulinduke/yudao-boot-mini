-- 补齐 AI 搜索记忆表的 TenantBaseDO 审计字段。
-- 该表初始版本缺少 creator、updater、deleted，导致查询时字段不存在。
ALTER TABLE ai_search_memory
  ADD COLUMN creator varchar(64) DEFAULT NULL,
  ADD COLUMN updater varchar(64) DEFAULT NULL,
  ADD COLUMN deleted bit(1) NOT NULL DEFAULT b'0';
