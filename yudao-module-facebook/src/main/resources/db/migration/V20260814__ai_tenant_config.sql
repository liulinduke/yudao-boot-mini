ALTER TABLE ai_workflow
    DROP INDEX uk_code,
    ADD UNIQUE INDEX uk_tenant_code_deleted (tenant_id, code, deleted);
