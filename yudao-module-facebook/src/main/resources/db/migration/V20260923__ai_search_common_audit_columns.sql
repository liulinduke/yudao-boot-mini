-- AI 全网企业获客表补齐 TenantBaseDO 公共审计字段。
-- 初始版本部分表只有 create_time、tenant_id，MyBatis-Plus 会按基类字段查询其余列。

ALTER TABLE ai_search_run
  ADD COLUMN update_time datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  ADD COLUMN creator varchar(64) DEFAULT NULL,
  ADD COLUMN updater varchar(64) DEFAULT NULL,
  ADD COLUMN deleted bit(1) NOT NULL DEFAULT b'0';

ALTER TABLE ai_search_round
  ADD COLUMN update_time datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  ADD COLUMN creator varchar(64) DEFAULT NULL,
  ADD COLUMN updater varchar(64) DEFAULT NULL,
  ADD COLUMN deleted bit(1) NOT NULL DEFAULT b'0';

ALTER TABLE ai_search_query
  ADD COLUMN update_time datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  ADD COLUMN creator varchar(64) DEFAULT NULL,
  ADD COLUMN updater varchar(64) DEFAULT NULL,
  ADD COLUMN deleted bit(1) NOT NULL DEFAULT b'0';

ALTER TABLE ai_search_source_registry
  ADD COLUMN update_time datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  ADD COLUMN creator varchar(64) DEFAULT NULL,
  ADD COLUMN updater varchar(64) DEFAULT NULL,
  ADD COLUMN deleted bit(1) NOT NULL DEFAULT b'0';

ALTER TABLE ai_search_memory
  ADD COLUMN update_time datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  ADD COLUMN creator varchar(64) DEFAULT NULL,
  ADD COLUMN updater varchar(64) DEFAULT NULL,
  ADD COLUMN deleted bit(1) NOT NULL DEFAULT b'0';

ALTER TABLE ai_search_frontier
  ADD COLUMN update_time datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  ADD COLUMN creator varchar(64) DEFAULT NULL,
  ADD COLUMN updater varchar(64) DEFAULT NULL,
  ADD COLUMN deleted bit(1) NOT NULL DEFAULT b'0';

ALTER TABLE ai_search_company_evidence
  ADD COLUMN update_time datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  ADD COLUMN creator varchar(64) DEFAULT NULL,
  ADD COLUMN updater varchar(64) DEFAULT NULL,
  ADD COLUMN deleted bit(1) NOT NULL DEFAULT b'0';

ALTER TABLE ai_search_contact_evidence
  ADD COLUMN update_time datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  ADD COLUMN creator varchar(64) DEFAULT NULL,
  ADD COLUMN updater varchar(64) DEFAULT NULL,
  ADD COLUMN deleted bit(1) NOT NULL DEFAULT b'0';

ALTER TABLE ai_search_trade_evidence
  ADD COLUMN update_time datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  ADD COLUMN creator varchar(64) DEFAULT NULL,
  ADD COLUMN updater varchar(64) DEFAULT NULL,
  ADD COLUMN deleted bit(1) NOT NULL DEFAULT b'0';
