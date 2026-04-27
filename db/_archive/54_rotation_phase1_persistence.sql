CREATE TABLE IF NOT EXISTS rotation_plans (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    person_id BIGINT NOT NULL REFERENCES people(id) ON DELETE RESTRICT,
    source_workflow_id BIGINT NOT NULL REFERENCES workflows(id) ON DELETE RESTRICT,
    title VARCHAR(220) NOT NULL,
    status VARCHAR(32) NOT NULL DEFAULT 'draft'
        CHECK (status IN ('draft', 'active', 'completed', 'archived')),
    created_by_user_id BIGINT REFERENCES app_users(id) ON DELETE SET NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE OR REPLACE FUNCTION ensure_rotation_plan_source_workflow_completed()
RETURNS TRIGGER AS $$
DECLARE
    workflow_status VARCHAR(40);
BEGIN
    SELECT status
    INTO workflow_status
    FROM workflows
    WHERE id = NEW.source_workflow_id;

    IF workflow_status IS NULL THEN
        RETURN NEW;
    END IF;

    IF workflow_status <> 'completed' THEN
        RAISE EXCEPTION 'rotation_plans.source_workflow_id % must reference a completed workflow', NEW.source_workflow_id;
    END IF;

    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_rotation_plans_validate_source_workflow ON rotation_plans;
CREATE TRIGGER trg_rotation_plans_validate_source_workflow
    BEFORE INSERT OR UPDATE OF source_workflow_id
    ON rotation_plans
    FOR EACH ROW
    EXECUTE FUNCTION ensure_rotation_plan_source_workflow_completed();

CREATE TABLE IF NOT EXISTS rotation_stations (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    rotation_plan_id BIGINT NOT NULL REFERENCES rotation_plans(id) ON DELETE CASCADE,
    department_id INTEGER NOT NULL REFERENCES departments(id) ON DELETE RESTRICT,
    start_date DATE NOT NULL,
    end_date DATE NOT NULL,
    order_index INTEGER NOT NULL DEFAULT 0 CHECK (order_index >= 0),
    location VARCHAR(160),
    notes TEXT,
    status VARCHAR(32) NOT NULL DEFAULT 'planned'
        CHECK (status IN ('planned', 'active', 'completed', 'cancelled')),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT chk_rotation_stations_date_range CHECK (end_date >= start_date),
    UNIQUE (rotation_plan_id, order_index)
);

CREATE TABLE IF NOT EXISTS department_action_templates (
    id INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    department_id INTEGER NOT NULL REFERENCES departments(id) ON DELETE RESTRICT,
    trigger_type VARCHAR(16) NOT NULL
        CHECK (trigger_type IN ('enter', 'exit')),
    title VARCHAR(220) NOT NULL,
    description TEXT,
    task_type VARCHAR(32) NOT NULL
        CHECK (task_type IN ('manual', 'technical', 'approval', 'information')),
    default_responsibility_id INTEGER REFERENCES app_responsibilities(id) ON DELETE SET NULL,
    due_offset_days INTEGER NOT NULL,
    reminder_offset_days INTEGER,
    is_automatable BOOLEAN NOT NULL DEFAULT FALSE,
    automation_key VARCHAR(120),
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT chk_department_action_templates_automation_key
        CHECK (is_automatable OR automation_key IS NULL),
    CONSTRAINT chk_department_action_templates_reminder_offset
        CHECK (reminder_offset_days IS NULL OR reminder_offset_days >= 0)
);

CREATE TABLE IF NOT EXISTS rotation_generated_tasks (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    rotation_plan_id BIGINT NOT NULL REFERENCES rotation_plans(id) ON DELETE CASCADE,
    rotation_station_id BIGINT NOT NULL REFERENCES rotation_stations(id) ON DELETE CASCADE,
    person_id BIGINT NOT NULL REFERENCES people(id) ON DELETE RESTRICT,
    department_id INTEGER NOT NULL REFERENCES departments(id) ON DELETE RESTRICT,
    template_id INTEGER REFERENCES department_action_templates(id) ON DELETE SET NULL,
    title VARCHAR(220) NOT NULL,
    description TEXT,
    task_type VARCHAR(32) NOT NULL
        CHECK (task_type IN ('manual', 'technical', 'approval', 'information')),
    responsibility_id INTEGER REFERENCES app_responsibilities(id) ON DELETE SET NULL,
    due_date DATE,
    status VARCHAR(32) NOT NULL DEFAULT 'open'
        CHECK (status IN ('open', 'in_progress', 'completed', 'failed', 'cancelled')),
    completion_note TEXT,
    completed_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS rotation_notifications (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    rotation_plan_id BIGINT NOT NULL REFERENCES rotation_plans(id) ON DELETE CASCADE,
    rotation_station_id BIGINT REFERENCES rotation_stations(id) ON DELETE SET NULL,
    generated_task_id BIGINT REFERENCES rotation_generated_tasks(id) ON DELETE SET NULL,
    notification_type VARCHAR(32) NOT NULL
        CHECK (notification_type IN ('upcoming_change', 'reminder', 'overdue', 'escalation')),
    recipient_email VARCHAR(320) NOT NULL,
    recipient_user_id BIGINT REFERENCES app_users(id) ON DELETE SET NULL,
    subject VARCHAR(220) NOT NULL,
    payload_json JSONB,
    status VARCHAR(32) NOT NULL DEFAULT 'pending'
        CHECK (status IN ('pending', 'sent', 'failed', 'disabled')),
    attempts INTEGER NOT NULL DEFAULT 0 CHECK (attempts >= 0),
    last_error TEXT,
    sent_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS rotation_audit_log (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    rotation_plan_id BIGINT NOT NULL REFERENCES rotation_plans(id) ON DELETE CASCADE,
    rotation_station_id BIGINT REFERENCES rotation_stations(id) ON DELETE SET NULL,
    generated_task_id BIGINT REFERENCES rotation_generated_tasks(id) ON DELETE SET NULL,
    actor_user_id BIGINT REFERENCES app_users(id) ON DELETE SET NULL,
    event_type VARCHAR(80) NOT NULL,
    old_value JSONB,
    new_value JSONB,
    detail TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_rotation_plans_person_id
    ON rotation_plans(person_id);
CREATE INDEX IF NOT EXISTS idx_rotation_plans_source_workflow_id
    ON rotation_plans(source_workflow_id);
CREATE UNIQUE INDEX IF NOT EXISTS uq_rotation_plans_active_per_person
    ON rotation_plans(person_id)
    WHERE status = 'active';
CREATE UNIQUE INDEX IF NOT EXISTS uq_rotation_plans_open_source_workflow
    ON rotation_plans(source_workflow_id)
    WHERE status IN ('draft', 'active');
CREATE INDEX IF NOT EXISTS idx_department_action_templates_department_trigger_active
    ON department_action_templates(department_id, trigger_type, is_active);
CREATE INDEX IF NOT EXISTS idx_rotation_generated_tasks_plan_id
    ON rotation_generated_tasks(rotation_plan_id);
CREATE INDEX IF NOT EXISTS idx_rotation_generated_tasks_station_id
    ON rotation_generated_tasks(rotation_station_id);
CREATE INDEX IF NOT EXISTS idx_rotation_generated_tasks_department_status
    ON rotation_generated_tasks(department_id, status);
CREATE INDEX IF NOT EXISTS idx_rotation_generated_tasks_due_date_status
    ON rotation_generated_tasks(due_date, status);
CREATE INDEX IF NOT EXISTS idx_rotation_notifications_plan_type_status
    ON rotation_notifications(rotation_plan_id, notification_type, status);
CREATE INDEX IF NOT EXISTS idx_rotation_notifications_station_type
    ON rotation_notifications(rotation_station_id, notification_type);
CREATE INDEX IF NOT EXISTS idx_rotation_audit_log_plan_id
    ON rotation_audit_log(rotation_plan_id, created_at DESC);
CREATE INDEX IF NOT EXISTS idx_rotation_audit_log_generated_task_id
    ON rotation_audit_log(generated_task_id);
