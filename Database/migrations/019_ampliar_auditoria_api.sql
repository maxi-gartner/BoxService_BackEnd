ALTER TABLE auth_audit_events
    ADD COLUMN IF NOT EXISTS http_method VARCHAR(10),
    ADD COLUMN IF NOT EXISTS resource_path TEXT;
