CREATE TABLE IF NOT EXISTS workflow_audit_log (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    workflow_id BIGINT NOT NULL REFERENCES workflows(id) ON DELETE CASCADE,
    task_id BIGINT REFERENCES workflow_tasks(id) ON DELETE SET NULL,
    actor_user_id BIGINT REFERENCES app_users(id) ON DELETE SET NULL,
    event_type VARCHAR(80) NOT NULL,
    old_value VARCHAR(80),
    new_value VARCHAR(80),
    detail TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_workflow_audit_log_workflow_id
    ON workflow_audit_log(workflow_id, created_at DESC);
