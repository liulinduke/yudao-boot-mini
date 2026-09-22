-- AI 全网企业获客 Phase 1 基础模型
-- 与 Facebook Agent 解耦；所有数据按租户隔离。

CREATE TABLE IF NOT EXISTS ai_search_task (
  id bigint NOT NULL,
  name varchar(255) NOT NULL,
  user_goal varchar(2000) NOT NULL,
  target_country varchar(255) DEFAULT NULL,
  target_count int NOT NULL DEFAULT 100,
  customer_type varchar(64) DEFAULT NULL,
  reference_website varchar(500) DEFAULT NULL,
  hs_codes varchar(500) DEFAULT NULL,
  ai_keyword_expand bit(1) NOT NULL DEFAULT b'1',
  contact_enrichment bit(1) NOT NULL DEFAULT b'1',
  position_strategy varchar(1000) DEFAULT NULL,
  status varchar(32) NOT NULL DEFAULT 'DRAFT',
  schedule_type varchar(32) NOT NULL DEFAULT 'ONCE',
  schedule_interval varchar(100) DEFAULT NULL,
  started_at datetime DEFAULT NULL,
  completed_at datetime DEFAULT NULL,
  creator varchar(64) DEFAULT NULL,
  updater varchar(64) DEFAULT NULL,
  create_time datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  update_time datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  deleted bit(1) NOT NULL DEFAULT b'0',
  tenant_id bigint NOT NULL,
  PRIMARY KEY (id), KEY idx_ai_search_task_tenant_status (tenant_id, status)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='AI全网企业获客任务';

CREATE TABLE IF NOT EXISTS ai_search_company (
  id bigint NOT NULL,
  task_id bigint DEFAULT NULL,
  name varchar(255) NOT NULL,
  normalized_name varchar(255) DEFAULT NULL,
  legal_name varchar(255) DEFAULT NULL,
  country varchar(100) DEFAULT NULL,
  region varchar(255) DEFAULT NULL,
  website varchar(500) DEFAULT NULL,
  domain varchar(255) DEFAULT NULL,
  industry varchar(255) DEFAULT NULL,
  customer_type varchar(64) DEFAULT NULL,
  description text,
  address varchar(500) DEFAULT NULL,
  postal_code varchar(64) DEFAULT NULL,
  phone varchar(255) DEFAULT NULL,
  fax varchar(255) DEFAULT NULL,
  email varchar(500) DEFAULT NULL,
  linkedin_url varchar(500) DEFAULT NULL,
  facebook_url varchar(500) DEFAULT NULL,
  employee_count int DEFAULT NULL,
  company_size varchar(64) DEFAULT NULL,
  main_products text,
  purchasing_products text,
  founded_year int DEFAULT NULL,
  revenue varchar(100) DEFAULT NULL,
  company_note text,
  customer_stage varchar(64) DEFAULT NULL,
  customer_tags varchar(1000) DEFAULT NULL,
  icp_score int DEFAULT NULL,
  icp_level varchar(8) DEFAULT NULL,
  verification_status varchar(32) DEFAULT 'UNKNOWN',
  confidence decimal(5,2) DEFAULT NULL,
  creator varchar(64) DEFAULT NULL,
  updater varchar(64) DEFAULT NULL,
  create_time datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  update_time datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  deleted bit(1) NOT NULL DEFAULT b'0',
  tenant_id bigint NOT NULL,
  PRIMARY KEY (id), KEY idx_ai_company_task (tenant_id, task_id), KEY idx_ai_company_domain (tenant_id, domain), KEY idx_ai_company_name (tenant_id, normalized_name)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='AI获客企业';

CREATE TABLE IF NOT EXISTS ai_search_contact (
  id bigint NOT NULL,
  company_id bigint NOT NULL,
  task_id bigint DEFAULT NULL,
  name varchar(255) DEFAULT NULL,
  normalized_name varchar(255) DEFAULT NULL,
  job_title varchar(255) DEFAULT NULL,
  email varchar(500) DEFAULT NULL,
  phone varchar(255) DEFAULT NULL,
  linkedin_url varchar(500) DEFAULT NULL,
  other_social_url varchar(500) DEFAULT NULL,
  gender varchar(32) DEFAULT NULL,
  note text,
  priority_score int DEFAULT NULL,
  ai_recommended bit(1) NOT NULL DEFAULT b'0',
  verification_status varchar(32) DEFAULT 'UNKNOWN',
  confidence decimal(5,2) DEFAULT NULL,
  creator varchar(64) DEFAULT NULL,
  updater varchar(64) DEFAULT NULL,
  create_time datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  update_time datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  deleted bit(1) NOT NULL DEFAULT b'0',
  tenant_id bigint NOT NULL,
  PRIMARY KEY (id), KEY idx_ai_contact_company (tenant_id, company_id), KEY idx_ai_contact_email (tenant_id, email)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='AI获客联系人';

CREATE TABLE IF NOT EXISTS ai_search_company_evidence (
  id bigint NOT NULL,
  company_id bigint NOT NULL,
  url varchar(1000) NOT NULL,
  evidence_type varchar(64) DEFAULT NULL,
  evidence_text text,
  confidence decimal(5,2) DEFAULT NULL,
  collected_at datetime DEFAULT NULL,
  create_time datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  tenant_id bigint NOT NULL,
  PRIMARY KEY (id), KEY idx_ai_company_evidence (tenant_id, company_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='企业证据';

CREATE TABLE IF NOT EXISTS ai_search_contact_evidence (
  id bigint NOT NULL,
  contact_id bigint NOT NULL,
  url varchar(1000) NOT NULL,
  evidence_text text,
  confidence decimal(5,2) DEFAULT NULL,
  collected_at datetime DEFAULT NULL,
  create_time datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  tenant_id bigint NOT NULL,
  PRIMARY KEY (id), KEY idx_ai_contact_evidence (tenant_id, contact_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='联系人证据';

CREATE TABLE IF NOT EXISTS ai_search_trade_evidence (
  id bigint NOT NULL,
  company_id bigint NOT NULL,
  url varchar(1000) DEFAULT NULL,
  hs_code varchar(100) DEFAULT NULL,
  product varchar(500) DEFAULT NULL,
  trade_type varchar(64) DEFAULT NULL,
  trade_date date DEFAULT NULL,
  partner_country varchar(100) DEFAULT NULL,
  evidence_text text,
  confidence decimal(5,2) DEFAULT NULL,
  collected_at datetime DEFAULT NULL,
  create_time datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  tenant_id bigint NOT NULL,
  PRIMARY KEY (id), KEY idx_ai_trade_company (tenant_id, company_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='贸易背景证据';

CREATE TABLE IF NOT EXISTS ai_search_run (
  id bigint NOT NULL, task_id bigint NOT NULL, status varchar(32) NOT NULL DEFAULT 'RUNNING',
  started_at datetime DEFAULT NULL, completed_at datetime DEFAULT NULL, round_count int NOT NULL DEFAULT 0,
  query_count int NOT NULL DEFAULT 0, source_count int NOT NULL DEFAULT 0, new_company_count int NOT NULL DEFAULT 0,
  qualified_company_count int NOT NULL DEFAULT 0, trade_verification_count int NOT NULL DEFAULT 0,
  trade_found_count int NOT NULL DEFAULT 0, contact_count int NOT NULL DEFAULT 0, error_count int NOT NULL DEFAULT 0,
  create_time datetime NOT NULL DEFAULT CURRENT_TIMESTAMP, tenant_id bigint NOT NULL,
  PRIMARY KEY (id), KEY idx_ai_search_run_task (tenant_id, task_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='AI企业获客运行记录';

CREATE TABLE IF NOT EXISTS ai_search_round (
  id bigint NOT NULL, task_id bigint NOT NULL, run_id bigint NOT NULL, round_no int NOT NULL,
  strategy text, query_count int NOT NULL DEFAULT 0, result_count int NOT NULL DEFAULT 0,
  new_company_count int NOT NULL DEFAULT 0, qualified_company_count int NOT NULL DEFAULT 0,
  duplicate_count int NOT NULL DEFAULT 0, trade_check_count int NOT NULL DEFAULT 0,
  trade_found_count int NOT NULL DEFAULT 0, new_source_count int NOT NULL DEFAULT 0,
  create_time datetime NOT NULL DEFAULT CURRENT_TIMESTAMP, tenant_id bigint NOT NULL,
  PRIMARY KEY (id), KEY idx_ai_search_round_task (tenant_id, task_id, round_no)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='AI企业获客搜索轮次';

CREATE TABLE IF NOT EXISTS ai_search_query (
  id bigint NOT NULL, task_id bigint NOT NULL, round_no int NOT NULL, query varchar(1000) NOT NULL,
  language varchar(64) DEFAULT NULL, country varchar(100) DEFAULT NULL, source_id bigint DEFAULT NULL,
  result_count int NOT NULL DEFAULT 0, new_company_count int NOT NULL DEFAULT 0, qualified_count int NOT NULL DEFAULT 0,
  duplicate_count int NOT NULL DEFAULT 0, yield_score decimal(8,2) DEFAULT NULL, status varchar(32) DEFAULT NULL,
  create_time datetime NOT NULL DEFAULT CURRENT_TIMESTAMP, tenant_id bigint NOT NULL,
  PRIMARY KEY (id), KEY idx_ai_search_query_task (tenant_id, task_id, round_no)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='AI企业获客搜索查询';

CREATE TABLE IF NOT EXISTS ai_search_source_registry (
  id bigint NOT NULL, name varchar(255) NOT NULL, domain varchar(255) DEFAULT NULL,
  source_type varchar(64) NOT NULL, source_role varchar(64) DEFAULT NULL, countries text,
  industries text, customer_types text, keywords text, languages text, entity_types text,
  discovery_methods text, capabilities text, priority int NOT NULL DEFAULT 0, status varchar(32) NOT NULL DEFAULT 'CANDIDATE',
  verified bit(1) NOT NULL DEFAULT b'0', usage_count int NOT NULL DEFAULT 0, company_count int NOT NULL DEFAULT 0,
  qualified_company_count int NOT NULL DEFAULT 0, duplicate_count int NOT NULL DEFAULT 0, yield_score decimal(8,2) DEFAULT NULL,
  error_rate decimal(8,2) DEFAULT NULL, avg_new_company decimal(10,2) DEFAULT NULL, avg_qualified_company decimal(10,2) DEFAULT NULL,
  avg_contacts decimal(10,2) DEFAULT NULL, last_tested_at datetime DEFAULT NULL, last_used_at datetime DEFAULT NULL,
  creator varchar(64) DEFAULT NULL, updater varchar(64) DEFAULT NULL, create_time datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  update_time datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP, deleted bit(1) NOT NULL DEFAULT b'0', tenant_id bigint NOT NULL,
  PRIMARY KEY (id), KEY idx_ai_source_status (tenant_id, status), KEY idx_ai_source_type (tenant_id, source_type)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='AI企业获客渠道资产';

CREATE TABLE IF NOT EXISTS ai_search_memory (
  id bigint NOT NULL, scope_type varchar(16) NOT NULL, scope_id bigint DEFAULT NULL, memory_key varchar(255) NOT NULL,
  memory_value text, score decimal(8,2) DEFAULT NULL, usage_count int NOT NULL DEFAULT 0,
  create_time datetime NOT NULL DEFAULT CURRENT_TIMESTAMP, update_time datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  tenant_id bigint NOT NULL, PRIMARY KEY (id), KEY idx_ai_memory_scope (tenant_id, scope_type, scope_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='AI搜索记忆';

CREATE TABLE IF NOT EXISTS ai_search_frontier (
  id bigint NOT NULL, task_id bigint NOT NULL, country varchar(100) DEFAULT NULL, product varchar(500) DEFAULT NULL,
  customer_type varchar(100) DEFAULT NULL, language varchar(64) DEFAULT NULL, keyword varchar(500) DEFAULT NULL,
  source_id bigint DEFAULT NULL, status varchar(32) NOT NULL DEFAULT 'UNEXPLORED', last_round_no int DEFAULT NULL,
  create_time datetime NOT NULL DEFAULT CURRENT_TIMESTAMP, update_time datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  tenant_id bigint NOT NULL, PRIMARY KEY (id), KEY idx_ai_frontier_task (tenant_id, task_id, status)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='AI搜索空间';
