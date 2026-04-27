ALTER TABLE workflow_definition_versions
    ADD COLUMN IF NOT EXISTS primary_legacy_process_type_id INTEGER REFERENCES process_types(id) ON DELETE RESTRICT,
    ADD COLUMN IF NOT EXISTS published_at TIMESTAMPTZ;

CREATE UNIQUE INDEX IF NOT EXISTS uq_workflow_definition_versions_published_per_definition
    ON workflow_definition_versions(workflow_definition_id)
    WHERE status = 'published';

ALTER TABLE workflows
    ADD COLUMN IF NOT EXISTS workflow_definition_version_id BIGINT REFERENCES workflow_definition_versions(id) ON DELETE SET NULL,
    ADD COLUMN IF NOT EXISTS current_runtime_status VARCHAR(32);

CREATE TABLE IF NOT EXISTS workflow_node_instances (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    workflow_id BIGINT NOT NULL REFERENCES workflows(id) ON DELETE CASCADE,
    workflow_node_id BIGINT NOT NULL REFERENCES workflow_nodes(id) ON DELETE CASCADE,
    status VARCHAR(32) NOT NULL
        CHECK (status IN ('pending', 'active', 'done', 'failed', 'cancelled')),
    started_at TIMESTAMPTZ,
    completed_at TIMESTAMPTZ,
    result_json JSONB,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE (workflow_id, workflow_node_id)
);

CREATE TABLE IF NOT EXISTS workflow_runtime_events (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    workflow_id BIGINT NOT NULL REFERENCES workflows(id) ON DELETE CASCADE,
    workflow_node_instance_id BIGINT REFERENCES workflow_node_instances(id) ON DELETE SET NULL,
    event_type VARCHAR(80) NOT NULL,
    payload_json JSONB,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_workflows_definition_version_id
    ON workflows(workflow_definition_version_id)
    WHERE workflow_definition_version_id IS NOT NULL;
CREATE INDEX IF NOT EXISTS idx_workflows_current_runtime_status
    ON workflows(current_runtime_status)
    WHERE current_runtime_status IS NOT NULL;
CREATE INDEX IF NOT EXISTS idx_workflow_node_instances_workflow_id
    ON workflow_node_instances(workflow_id, status, id);
CREATE INDEX IF NOT EXISTS idx_workflow_runtime_events_workflow_id
    ON workflow_runtime_events(workflow_id, created_at, id);
