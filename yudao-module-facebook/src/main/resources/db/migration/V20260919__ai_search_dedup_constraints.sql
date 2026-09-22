-- Protect entity deduplication when scheduled runs overlap.
-- Existing duplicate rows, if any, must be reviewed before applying this migration.
ALTER TABLE ai_search_company
  ADD UNIQUE KEY uk_ai_company_task_domain (tenant_id, task_id, domain);

ALTER TABLE ai_search_contact
  ADD UNIQUE KEY uk_ai_contact_company_name (tenant_id, company_id, normalized_name);
