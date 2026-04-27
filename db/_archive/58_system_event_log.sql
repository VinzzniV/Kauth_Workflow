CREATE TABLE IF NOT EXISTS system_event_log (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    severity VARCHAR(16) NOT NULL
        CHECK (severity IN ('info', 'warning', 'error')),
    source VARCHAR(32) NOT NULL
        CHECK (source IN ('frontend', 'api', 'system', 'mail', 'entra', 'directory', 'automation', 'workflow', 'task', 'rotation', 'admin')),
    category VARCHAR(64) NOT NULL,
    event_key VARCHAR(128) NOT NULL,
    message TEXT NOT NULL,
    user_message TEXT,
    actor_user_id BIGINT REFERENCES app_users(id) ON DELETE SET NULL,
    client_route VARCHAR(500),
    client_function VARCHAR(160),
    http_method VARCHAR(16),
    http_path VARCHAR(500),
    http_status INTEGER,
    trace_identifier VARCHAR(128),
    workflow_uid UUID,
    rotation_plan_id BIGINT,
    task_ref VARCHAR(160),
    entity_type VARCHAR(64),
    entity_id VARCHAR(128),
    details_json JSONB
);

CREATE INDEX IF NOT EXISTS idx_system_event_log_created_at
    ON system_event_log(created_at DESC, id DESC);

CREATE INDEX IF NOT EXISTS idx_system_event_log_severity_source_created_at
    ON system_event_log(severity, source, created_at DESC, id DESC);

CREATE INDEX IF NOT EXISTS idx_system_event_log_actor_user_id
    ON system_event_log(actor_user_id, created_at DESC, id DESC);

CREATE INDEX IF NOT EXISTS idx_system_event_log_workflow_uid
    ON system_event_log(workflow_uid, created_at DESC, id DESC);

CREATE INDEX IF NOT EXISTS idx_system_event_log_rotation_plan_id
    ON system_event_log(rotation_plan_id, created_at DESC, id DESC);

CREATE INDEX IF NOT EXISTS idx_system_event_log_task_ref
    ON system_event_log(task_ref, created_at DESC, id DESC);
