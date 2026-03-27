-- Migration: audit trail for directory group role mappings.

CREATE TABLE IF NOT EXISTS directory_mapping_audit_log (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    actor_user_id BIGINT REFERENCES app_users(id) ON DELETE SET NULL,
    event_type VARCHAR(40) NOT NULL,
    entity_type VARCHAR(40) NOT NULL,
    detail TEXT,
    old_value JSONB,
    new_value JSONB,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_directory_mapping_audit_log_created_at
    ON directory_mapping_audit_log(created_at DESC);

CREATE INDEX IF NOT EXISTS idx_directory_mapping_audit_log_actor
    ON directory_mapping_audit_log(actor_user_id)
    WHERE actor_user_id IS NOT NULL;
