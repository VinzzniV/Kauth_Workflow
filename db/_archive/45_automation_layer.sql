ALTER TABLE workflow_nodes
    DROP CONSTRAINT IF EXISTS workflow_nodes_node_type_check;

ALTER TABLE workflow_nodes
    ADD CONSTRAINT workflow_nodes_node_type_check
    CHECK (node_type IN ('start', 'form', 'approval', 'task', 'decision', 'parallel_split', 'parallel_join', 'automation', 'measure_provision', 'measure_deprovision', 'measure_change', 'measure_rename', 'setup', 'end'));

CREATE TABLE IF NOT EXISTS action_definitions (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    action_key VARCHAR(120) NOT NULL UNIQUE,
    name VARCHAR(220) NOT NULL,
    description TEXT,
    handler_type VARCHAR(120) NOT NULL,
    parameter_schema_json JSONB,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    requires_approval BOOLEAN NOT NULL DEFAULT FALSE,
    is_idempotent BOOLEAN NOT NULL DEFAULT FALSE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS workflow_node_actions (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    workflow_node_id BIGINT NOT NULL REFERENCES workflow_nodes(id) ON DELETE CASCADE,
    action_definition_id BIGINT NOT NULL REFERENCES action_definitions(id) ON DELETE RESTRICT,
    input_mapping_json JSONB,
    execution_order INTEGER NOT NULL DEFAULT 0,
    on_error_behavior VARCHAR(32) NOT NULL DEFAULT 'fail_workflow'
        CHECK (on_error_behavior IN ('fail_workflow')),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE (workflow_node_id, execution_order),
    UNIQUE (workflow_node_id, action_definition_id, execution_order)
);

CREATE TABLE IF NOT EXISTS automation_jobs (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    workflow_id BIGINT NOT NULL REFERENCES workflows(id) ON DELETE CASCADE,
    workflow_node_instance_id BIGINT NOT NULL REFERENCES workflow_node_instances(id) ON DELETE CASCADE,
    workflow_node_action_id BIGINT NOT NULL REFERENCES workflow_node_actions(id) ON DELETE RESTRICT,
    action_definition_id BIGINT NOT NULL REFERENCES action_definitions(id) ON DELETE RESTRICT,
    status VARCHAR(32) NOT NULL
        CHECK (status IN ('pending', 'running', 'succeeded', 'failed', 'cancelled')),
    payload_json JSONB,
    available_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    started_at TIMESTAMPTZ,
    completed_at TIMESTAMPTZ
);

CREATE TABLE IF NOT EXISTS automation_job_attempts (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    automation_job_id BIGINT NOT NULL REFERENCES automation_jobs(id) ON DELETE CASCADE,
    attempt_number INTEGER NOT NULL,
    status VARCHAR(32) NOT NULL
        CHECK (status IN ('running', 'succeeded', 'failed')),
    error_message TEXT,
    started_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    completed_at TIMESTAMPTZ,
    UNIQUE (automation_job_id, attempt_number)
);

CREATE TABLE IF NOT EXISTS automation_job_logs (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    automation_job_id BIGINT NOT NULL REFERENCES automation_jobs(id) ON DELETE CASCADE,
    level VARCHAR(16) NOT NULL
        CHECK (level IN ('debug', 'info', 'warning', 'error')),
    message TEXT NOT NULL,
    details_json JSONB,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_workflow_node_actions_workflow_node_id
    ON workflow_node_actions(workflow_node_id, execution_order, id);
CREATE INDEX IF NOT EXISTS idx_automation_jobs_claim
    ON automation_jobs(status, available_at, created_at, id)
    WHERE status = 'pending';
CREATE INDEX IF NOT EXISTS idx_automation_jobs_workflow_id
    ON automation_jobs(workflow_id, created_at, id);
CREATE INDEX IF NOT EXISTS idx_automation_jobs_node_instance_id
    ON automation_jobs(workflow_node_instance_id, created_at, id);
CREATE INDEX IF NOT EXISTS idx_automation_job_attempts_job_id
    ON automation_job_attempts(automation_job_id, attempt_number);
CREATE INDEX IF NOT EXISTS idx_automation_job_logs_job_id
    ON automation_job_logs(automation_job_id, created_at, id);

INSERT INTO action_definitions (
    action_key,
    name,
    description,
    handler_type,
    parameter_schema_json,
    is_active,
    requires_approval,
    is_idempotent,
    updated_at
)
VALUES
    (
        'CreateAdUser',
        'Create AD User',
        'Simulated creation of an Active Directory user.',
        'simulated_directory',
        '{"type":"object","additionalProperties":true}'::jsonb,
        TRUE,
        FALSE,
        TRUE,
        NOW()
    ),
    (
        'CreateMailbox',
        'Create Mailbox',
        'Simulated provisioning of a mailbox.',
        'simulated_mailbox',
        '{"type":"object","additionalProperties":true}'::jsonb,
        TRUE,
        FALSE,
        TRUE,
        NOW()
    ),
    (
        'AssignGroups',
        'Assign Groups',
        'Simulated assignment of directory groups.',
        'simulated_directory_groups',
        '{"type":"object","additionalProperties":true}'::jsonb,
        TRUE,
        FALSE,
        TRUE,
        NOW()
    ),
    (
        'CreateErpEmployee',
        'Create ERP Employee',
        'Simulated creation of an ERP employee.',
        'simulated_erp',
        '{"type":"object","additionalProperties":true}'::jsonb,
        TRUE,
        FALSE,
        FALSE,
        NOW()
    ),
    (
        'SendWelcomeMail',
        'Send Welcome Mail',
        'Simulated sending of a welcome email.',
        'simulated_notification',
        '{"type":"object","additionalProperties":true}'::jsonb,
        TRUE,
        FALSE,
        TRUE,
        NOW()
    )
ON CONFLICT (action_key) DO UPDATE
SET
    name = EXCLUDED.name,
    description = EXCLUDED.description,
    handler_type = EXCLUDED.handler_type,
    parameter_schema_json = EXCLUDED.parameter_schema_json,
    is_active = EXCLUDED.is_active,
    requires_approval = EXCLUDED.requires_approval,
    is_idempotent = EXCLUDED.is_idempotent,
    updated_at = NOW();
