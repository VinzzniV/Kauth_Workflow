CREATE EXTENSION IF NOT EXISTS pgcrypto;

DROP TABLE IF EXISTS workflow_notifications CASCADE;
DROP TABLE IF EXISTS workflow_task_comments CASCADE;
DROP TABLE IF EXISTS workflow_audit_log CASCADE;
DROP TABLE IF EXISTS notification_email_settings CASCADE;
DROP TABLE IF EXISTS task_assignments CASCADE;
DROP TABLE IF EXISTS workflow_task_dependencies CASCADE;
DROP TABLE IF EXISTS workflow_tasks CASCADE;
DROP TABLE IF EXISTS task_template_dependencies CASCADE;
DROP TABLE IF EXISTS task_template_conditions CASCADE;
DROP TABLE IF EXISTS task_templates CASCADE;
DROP TABLE IF EXISTS workflow_answer_selected_options CASCADE;
DROP TABLE IF EXISTS workflow_answers CASCADE;
DROP TABLE IF EXISTS workflows CASCADE;
DROP TABLE IF EXISTS app_role_answer_default_options CASCADE;
DROP TABLE IF EXISTS app_role_answer_defaults CASCADE;
DROP TABLE IF EXISTS workflow_answer_single_select_keep_values CASCADE;
DROP TABLE IF EXISTS workflow_answer_reset_rules CASCADE;
DROP TABLE IF EXISTS workflow_answer_validation_rules CASCADE;
DROP TABLE IF EXISTS workflow_answer_visibility_rules CASCADE;
DROP TABLE IF EXISTS workflow_answer_options CASCADE;
DROP TABLE IF EXISTS workflow_answer_definitions CASCADE;
DROP TABLE IF EXISTS system_responsibilities CASCADE;
DROP TABLE IF EXISTS department_settings CASCADE;
DROP TABLE IF EXISTS people CASCADE;
DROP TABLE IF EXISTS app_user_groups CASCADE;
DROP TABLE IF EXISTS app_group_responsibilities CASCADE;
DROP TABLE IF EXISTS app_group_roles CASCADE;
DROP TABLE IF EXISTS app_groups CASCADE;
DROP TABLE IF EXISTS app_user_responsibilities CASCADE;
DROP TABLE IF EXISTS app_user_roles CASCADE;
DROP TABLE IF EXISTS app_responsibilities CASCADE;
DROP TABLE IF EXISTS app_users CASCADE;
DROP TABLE IF EXISTS app_roles CASCADE;
DROP TABLE IF EXISTS departments CASCADE;

CREATE TABLE departments (
    id INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    name VARCHAR(120) NOT NULL UNIQUE
);

CREATE TABLE app_roles (
    id INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    department_id INTEGER REFERENCES departments(id) ON DELETE SET NULL,
    role_key VARCHAR(120) NOT NULL UNIQUE,
    name VARCHAR(160) NOT NULL,
    role_kind VARCHAR(32) NOT NULL CHECK (role_kind IN ('position', 'system')),
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE app_users (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    external_key VARCHAR(120),
    department_id INTEGER REFERENCES departments(id) ON DELETE SET NULL,
    display_name VARCHAR(180) NOT NULL,
    email VARCHAR(320) NOT NULL UNIQUE,
    notification_email VARCHAR(320),
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE notification_email_settings (
    id SMALLINT PRIMARY KEY DEFAULT 1 CHECK (id = 1),
    enabled BOOLEAN NOT NULL DEFAULT FALSE,
    tenant_id VARCHAR(255),
    client_id VARCHAR(255),
    client_secret TEXT,
    sender_email VARCHAR(320),
    frontend_base_url VARCHAR(500) NOT NULL DEFAULT 'http://localhost:5173',
    test_recipient_email VARCHAR(320),
    sandbox_redirect_email VARCHAR(320),
    notify_on_workflow_created BOOLEAN NOT NULL DEFAULT TRUE,
    notify_on_task_ready BOOLEAN NOT NULL DEFAULT TRUE,
    notify_on_workflow_completed BOOLEAN NOT NULL DEFAULT TRUE,
    last_test_status VARCHAR(16) NOT NULL DEFAULT 'never'
        CHECK (last_test_status IN ('never', 'succeeded', 'failed', 'disabled')),
    last_test_at TIMESTAMPTZ,
    last_error TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE people (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    app_user_id BIGINT NOT NULL UNIQUE REFERENCES app_users(id) ON DELETE CASCADE,
    department_id INTEGER REFERENCES departments(id) ON DELETE SET NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE OR REPLACE FUNCTION sync_people_department_to_user()
RETURNS TRIGGER AS $$
BEGIN
    IF pg_trigger_depth() > 1 THEN
        RETURN NEW;
    END IF;

    UPDATE app_users
    SET department_id = NEW.department_id
    WHERE id = NEW.app_user_id
      AND department_id IS DISTINCT FROM NEW.department_id;

    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION sync_user_department_to_people()
RETURNS TRIGGER AS $$
BEGIN
    IF pg_trigger_depth() > 1 THEN
        RETURN NEW;
    END IF;

    UPDATE people
    SET
        department_id = NEW.department_id,
        updated_at = NOW()
    WHERE app_user_id = NEW.id
      AND department_id IS DISTINCT FROM NEW.department_id;

    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_people_sync_department_to_user
AFTER INSERT OR UPDATE OF department_id ON people
FOR EACH ROW
EXECUTE FUNCTION sync_people_department_to_user();

CREATE TRIGGER trg_app_users_sync_department_to_people
AFTER INSERT OR UPDATE OF department_id ON app_users
FOR EACH ROW
EXECUTE FUNCTION sync_user_department_to_people();

CREATE TABLE app_groups (
    id INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    group_key VARCHAR(120) NOT NULL UNIQUE,
    name VARCHAR(160) NOT NULL UNIQUE,
    description TEXT,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE app_responsibilities (
    id INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    department_id INTEGER REFERENCES departments(id) ON DELETE SET NULL,
    responsibility_key VARCHAR(120) NOT NULL UNIQUE,
    system_key VARCHAR(64) UNIQUE,
    name VARCHAR(160) NOT NULL,
    responsibility_type VARCHAR(32) NOT NULL CHECK (responsibility_type IN ('process', 'department_lead', 'application')),
    description TEXT,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT chk_app_responsibilities_application_system_key
        CHECK (responsibility_type <> 'application' OR system_key IS NOT NULL)
);

CREATE TABLE department_settings (
    department_id INTEGER PRIMARY KEY REFERENCES departments(id) ON DELETE CASCADE,
    department_lead_person_id BIGINT REFERENCES people(id) ON DELETE SET NULL,
    requirement_approver_person_id BIGINT REFERENCES people(id) ON DELETE SET NULL,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE system_responsibilities (
    id INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    system_key VARCHAR(64) NOT NULL UNIQUE,
    app_responsibility_id INTEGER NOT NULL UNIQUE REFERENCES app_responsibilities(id) ON DELETE CASCADE,
    responsible_person_id BIGINT REFERENCES people(id) ON DELETE SET NULL,
    responsible_department_id INTEGER REFERENCES departments(id) ON DELETE SET NULL,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CHECK (responsible_person_id IS NOT NULL OR responsible_department_id IS NOT NULL)
);

CREATE TABLE app_user_roles (
    app_user_id BIGINT NOT NULL REFERENCES app_users(id) ON DELETE CASCADE,
    app_role_id INTEGER NOT NULL REFERENCES app_roles(id) ON DELETE CASCADE,
    assigned_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    PRIMARY KEY (app_user_id, app_role_id)
);

CREATE TABLE app_user_groups (
    app_user_id BIGINT NOT NULL REFERENCES app_users(id) ON DELETE CASCADE,
    app_group_id INTEGER NOT NULL REFERENCES app_groups(id) ON DELETE CASCADE,
    assigned_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    PRIMARY KEY (app_user_id, app_group_id)
);

CREATE TABLE app_group_roles (
    app_group_id INTEGER NOT NULL REFERENCES app_groups(id) ON DELETE CASCADE,
    app_role_id INTEGER NOT NULL REFERENCES app_roles(id) ON DELETE CASCADE,
    assigned_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    PRIMARY KEY (app_group_id, app_role_id)
);

CREATE TABLE app_user_responsibilities (
    app_user_id BIGINT NOT NULL REFERENCES app_users(id) ON DELETE CASCADE,
    app_responsibility_id INTEGER NOT NULL REFERENCES app_responsibilities(id) ON DELETE CASCADE,
    assigned_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    PRIMARY KEY (app_user_id, app_responsibility_id)
);

CREATE TABLE app_group_responsibilities (
    app_group_id INTEGER NOT NULL REFERENCES app_groups(id) ON DELETE CASCADE,
    app_responsibility_id INTEGER NOT NULL REFERENCES app_responsibilities(id) ON DELETE CASCADE,
    assigned_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    PRIMARY KEY (app_group_id, app_responsibility_id)
);

CREATE TABLE workflow_answer_definitions (
    id INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    answer_key VARCHAR(120) NOT NULL UNIQUE,
    title VARCHAR(180) NOT NULL,
    category VARCHAR(80) NOT NULL DEFAULT 'general',
    description TEXT NOT NULL,
    icon_key VARCHAR(80) NOT NULL DEFAULT 'berechtigungen',
    input_type VARCHAR(32) NOT NULL CHECK (input_type IN ('boolean', 'text', 'select', 'multi_select')),
    is_required BOOLEAN NOT NULL DEFAULT FALSE,
    sort_order INTEGER NOT NULL DEFAULT 0,
    is_active BOOLEAN NOT NULL DEFAULT TRUE
);

CREATE TABLE workflow_answer_options (
    id INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    answer_definition_id INTEGER NOT NULL REFERENCES workflow_answer_definitions(id) ON DELETE CASCADE,
    option_key VARCHAR(120) NOT NULL,
    option_value VARCHAR(180) NOT NULL,
    option_label VARCHAR(180) NOT NULL,
    sort_order INTEGER NOT NULL DEFAULT 0,
    CONSTRAINT uq_workflow_answer_options_definition_option_pair UNIQUE (answer_definition_id, id),
    UNIQUE (answer_definition_id, option_key),
    UNIQUE (answer_definition_id, option_value)
);

CREATE TABLE workflow_answer_visibility_rules (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    answer_definition_id INTEGER NOT NULL REFERENCES workflow_answer_definitions(id) ON DELETE CASCADE,
    dependency_answer_definition_id INTEGER NOT NULL REFERENCES workflow_answer_definitions(id) ON DELETE CASCADE,
    dependency_kind VARCHAR(40) NOT NULL CHECK (dependency_kind IN ('boolean_true', 'selected_option_value')),
    expected_value_text TEXT,
    missing_result BOOLEAN NOT NULL DEFAULT TRUE,
    sort_order INTEGER NOT NULL DEFAULT 0,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CHECK (answer_definition_id <> dependency_answer_definition_id)
);

CREATE UNIQUE INDEX uq_workflow_answer_visibility_rules
    ON workflow_answer_visibility_rules (
        answer_definition_id,
        dependency_answer_definition_id,
        dependency_kind,
        (expected_value_text IS NULL),
        COALESCE(expected_value_text, '')
    );

CREATE TABLE workflow_answer_validation_rules (
    answer_definition_id INTEGER PRIMARY KEY REFERENCES workflow_answer_definitions(id) ON DELETE CASCADE,
    validation_kind VARCHAR(40) NOT NULL CHECK (validation_kind IN ('text_required', 'single_select_required', 'multi_select_required')),
    message TEXT NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE workflow_answer_reset_rules (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    answer_definition_id INTEGER NOT NULL REFERENCES workflow_answer_definitions(id) ON DELETE CASCADE,
    trigger_kind VARCHAR(40) NOT NULL CHECK (trigger_kind IN ('when_not_true', 'single_select_mismatch')),
    target_answer_definition_id INTEGER NOT NULL REFERENCES workflow_answer_definitions(id) ON DELETE CASCADE,
    clear_boolean BOOLEAN NOT NULL DEFAULT FALSE,
    clear_text BOOLEAN NOT NULL DEFAULT FALSE,
    clear_number BOOLEAN NOT NULL DEFAULT FALSE,
    clear_selected_option BOOLEAN NOT NULL DEFAULT FALSE,
    clear_selected_options BOOLEAN NOT NULL DEFAULT FALSE,
    sort_order INTEGER NOT NULL DEFAULT 0,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CHECK (answer_definition_id <> target_answer_definition_id),
    CHECK (clear_boolean OR clear_text OR clear_number OR clear_selected_option OR clear_selected_options)
);

CREATE UNIQUE INDEX uq_workflow_answer_reset_rules
    ON workflow_answer_reset_rules (
        answer_definition_id,
        trigger_kind,
        target_answer_definition_id
    );

CREATE TABLE workflow_answer_single_select_keep_values (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    answer_definition_id INTEGER NOT NULL REFERENCES workflow_answer_definitions(id) ON DELETE CASCADE,
    option_value VARCHAR(180) NOT NULL,
    sort_order INTEGER NOT NULL DEFAULT 0,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE (answer_definition_id, option_value)
);

CREATE TABLE app_role_answer_defaults (
    app_role_id INTEGER NOT NULL REFERENCES app_roles(id) ON DELETE CASCADE,
    answer_definition_id INTEGER NOT NULL REFERENCES workflow_answer_definitions(id) ON DELETE CASCADE,
    is_recommended BOOLEAN NOT NULL DEFAULT TRUE,
    is_default BOOLEAN NOT NULL DEFAULT FALSE,
    default_value_boolean BOOLEAN,
    default_value_text TEXT,
    default_value_number NUMERIC(12, 2),
    sort_order INTEGER NOT NULL DEFAULT 0,
    PRIMARY KEY (app_role_id, answer_definition_id)
);

CREATE TABLE app_role_answer_default_options (
    app_role_id INTEGER NOT NULL,
    answer_definition_id INTEGER NOT NULL,
    answer_option_id INTEGER NOT NULL REFERENCES workflow_answer_options(id) ON DELETE CASCADE,
    is_default BOOLEAN NOT NULL DEFAULT FALSE,
    PRIMARY KEY (app_role_id, answer_definition_id, answer_option_id),
    FOREIGN KEY (app_role_id, answer_definition_id)
        REFERENCES app_role_answer_defaults(app_role_id, answer_definition_id)
        ON DELETE CASCADE
);

CREATE TABLE workflows (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    uid UUID NOT NULL DEFAULT gen_random_uuid() UNIQUE,
    department_id INTEGER NOT NULL REFERENCES departments(id) ON DELETE RESTRICT,
    onboarding_role_id INTEGER NOT NULL REFERENCES app_roles(id) ON DELETE RESTRICT,
    created_by_user_id BIGINT REFERENCES app_users(id) ON DELETE SET NULL,
    first_name VARCHAR(120) NOT NULL,
    last_name VARCHAR(120) NOT NULL,
    employee_number INTEGER NOT NULL,
    badge_number INTEGER NOT NULL,
    deadline_date DATE,
    status VARCHAR(40) NOT NULL DEFAULT 'draft'
        CHECK (status IN ('draft', 'in_progress', 'waiting_for_supervisor', 'waiting_for_department', 'completed')),
    started_at TIMESTAMPTZ,
    completed_at TIMESTAMPTZ,
    cancelled_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE workflow_answers (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    workflow_id BIGINT NOT NULL REFERENCES workflows(id) ON DELETE CASCADE,
    answer_definition_id INTEGER NOT NULL REFERENCES workflow_answer_definitions(id) ON DELETE RESTRICT,
    answer_key VARCHAR(120) NOT NULL,
    input_type VARCHAR(32) NOT NULL CHECK (input_type IN ('boolean', 'text', 'select', 'multi_select')),
    value_boolean BOOLEAN,
    value_text TEXT,
    value_number NUMERIC(12, 2),
    selected_option_id INTEGER,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE (workflow_id, answer_definition_id),
    CONSTRAINT fk_workflow_answers_selected_option_matches_definition
        FOREIGN KEY (answer_definition_id, selected_option_id)
        REFERENCES workflow_answer_options(answer_definition_id, id)
        ON DELETE RESTRICT
);

CREATE TABLE workflow_answer_selected_options (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    workflow_answer_id BIGINT NOT NULL REFERENCES workflow_answers(id) ON DELETE CASCADE,
    answer_option_id INTEGER NOT NULL REFERENCES workflow_answer_options(id) ON DELETE CASCADE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE (workflow_answer_id, answer_option_id)
);

CREATE TABLE task_templates (
    id INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    template_key VARCHAR(120) NOT NULL UNIQUE,
    title VARCHAR(220) NOT NULL,
    category VARCHAR(80) NOT NULL DEFAULT 'general',
    description TEXT NOT NULL,
    icon_key VARCHAR(80) NOT NULL DEFAULT 'berechtigungen',
    owning_department_id INTEGER REFERENCES departments(id) ON DELETE SET NULL,
    default_responsibility_id INTEGER REFERENCES app_responsibilities(id) ON DELETE SET NULL,
    process_area_label VARCHAR(80),
    is_department_phase_task BOOLEAN NOT NULL DEFAULT TRUE,
    is_required BOOLEAN NOT NULL DEFAULT TRUE,
    due_in_days INTEGER,
    sort_order INTEGER NOT NULL DEFAULT 0,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE task_template_conditions (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    task_template_id INTEGER NOT NULL REFERENCES task_templates(id) ON DELETE CASCADE,
    condition_group INTEGER NOT NULL DEFAULT 1,
    answer_key VARCHAR(120) NOT NULL REFERENCES workflow_answer_definitions(answer_key) ON UPDATE CASCADE ON DELETE RESTRICT,
    operator VARCHAR(32) NOT NULL CHECK (operator IN ('eq', 'neq', 'is_true', 'is_false', 'is_null', 'is_not_null')),
    expected_value_text TEXT,
    expected_value_boolean BOOLEAN,
    expected_value_number NUMERIC(12, 2),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE UNIQUE INDEX uq_task_template_conditions_rule
    ON task_template_conditions (
        task_template_id,
        condition_group,
        answer_key,
        operator,
        (expected_value_text IS NULL),
        (COALESCE(expected_value_text, '')),
        (expected_value_boolean IS NULL),
        (COALESCE(expected_value_boolean::text, '')),
        (expected_value_number IS NULL),
        (COALESCE(expected_value_number::text, ''))
    );

CREATE TABLE task_template_dependencies (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    task_template_id INTEGER NOT NULL REFERENCES task_templates(id) ON DELETE CASCADE,
    depends_on_task_template_id INTEGER NOT NULL REFERENCES task_templates(id) ON DELETE CASCADE,
    required_status VARCHAR(32) NOT NULL DEFAULT 'done'
        CHECK (required_status IN ('open', 'ready', 'in_progress', 'blocked', 'done')),
    UNIQUE (task_template_id, depends_on_task_template_id)
);

CREATE TABLE workflow_tasks (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    workflow_id BIGINT NOT NULL REFERENCES workflows(id) ON DELETE CASCADE,
    task_template_id INTEGER REFERENCES task_templates(id) ON DELETE SET NULL,
    task_key VARCHAR(120) NOT NULL,
    title VARCHAR(220) NOT NULL,
    category VARCHAR(80) NOT NULL,
    description TEXT NOT NULL,
    icon_key VARCHAR(80) NOT NULL DEFAULT 'berechtigungen',
    process_area_label VARCHAR(80),
    is_department_phase_task BOOLEAN NOT NULL DEFAULT TRUE,
    status VARCHAR(32) NOT NULL DEFAULT 'open'
        CHECK (status IN ('open', 'ready', 'in_progress', 'blocked', 'done')),
    is_required BOOLEAN NOT NULL DEFAULT TRUE,
    due_in_days INTEGER,
    due_at TIMESTAMPTZ,
    sort_order INTEGER NOT NULL DEFAULT 0,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    ready_at TIMESTAMPTZ,
    started_at TIMESTAMPTZ,
    completed_at TIMESTAMPTZ,
    cancelled_at TIMESTAMPTZ,
    UNIQUE (workflow_id, task_key)
);

CREATE TABLE workflow_task_dependencies (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    workflow_task_id BIGINT NOT NULL REFERENCES workflow_tasks(id) ON DELETE CASCADE,
    depends_on_workflow_task_id BIGINT NOT NULL REFERENCES workflow_tasks(id) ON DELETE CASCADE,
    required_status VARCHAR(32) NOT NULL DEFAULT 'done'
        CHECK (required_status IN ('open', 'ready', 'in_progress', 'blocked', 'done')),
    UNIQUE (workflow_task_id, depends_on_workflow_task_id),
    CHECK (workflow_task_id <> depends_on_workflow_task_id)
);

CREATE TABLE task_assignments (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    workflow_task_id BIGINT NOT NULL REFERENCES workflow_tasks(id) ON DELETE CASCADE,
    assignee_user_id BIGINT,
    assignee_responsibility_id INTEGER,
    assignment_type VARCHAR(16) NOT NULL DEFAULT 'responsibility' CHECK (assignment_type IN ('responsibility', 'user')),
    is_primary BOOLEAN NOT NULL DEFAULT TRUE,
    assigned_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    completed_at TIMESTAMPTZ,
    CONSTRAINT fk_task_assignments_assignee_user
        FOREIGN KEY (assignee_user_id) REFERENCES app_users(id) ON DELETE RESTRICT,
    CONSTRAINT fk_task_assignments_assignee_responsibility
        FOREIGN KEY (assignee_responsibility_id) REFERENCES app_responsibilities(id) ON DELETE RESTRICT,
    CONSTRAINT chk_task_assignments_target_matches_type
        CHECK (
            (assignment_type = 'user' AND assignee_user_id IS NOT NULL AND assignee_responsibility_id IS NULL)
            OR
            (assignment_type = 'responsibility' AND assignee_responsibility_id IS NOT NULL AND assignee_user_id IS NULL)
        )
);

CREATE TABLE workflow_task_comments (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    workflow_task_id BIGINT NOT NULL REFERENCES workflow_tasks(id) ON DELETE CASCADE,
    author_user_id BIGINT REFERENCES app_users(id) ON DELETE SET NULL,
    comment_text TEXT NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE workflow_audit_log (
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

CREATE TABLE workflow_notifications (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    workflow_id BIGINT NOT NULL REFERENCES workflows(id) ON DELETE CASCADE,
    workflow_task_id BIGINT REFERENCES workflow_tasks(id) ON DELETE CASCADE,
    recipient_user_id BIGINT REFERENCES app_users(id) ON DELETE SET NULL,
    target_email VARCHAR(320) NOT NULL,
    target_name VARCHAR(180) NOT NULL,
    notification_type VARCHAR(80) NOT NULL DEFAULT 'workflow_created',
    status VARCHAR(32) NOT NULL DEFAULT 'pending' CHECK (status IN ('pending', 'sent', 'failed', 'disabled')),
    attempts INTEGER NOT NULL DEFAULT 0,
    last_error TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    sent_at TIMESTAMPTZ
);

CREATE INDEX idx_app_roles_department_kind ON app_roles(department_id, role_kind);
CREATE INDEX idx_app_responsibilities_department_type ON app_responsibilities(department_id, responsibility_type);
CREATE UNIQUE INDEX uq_people_app_user_id ON people(app_user_id);
CREATE INDEX idx_people_department_id ON people(department_id);
CREATE INDEX idx_department_settings_lead_person ON department_settings(department_lead_person_id);
CREATE INDEX idx_department_settings_requirement_approver ON department_settings(requirement_approver_person_id);
CREATE INDEX idx_system_responsibilities_person ON system_responsibilities(responsible_person_id);
CREATE INDEX idx_system_responsibilities_department ON system_responsibilities(responsible_department_id);
CREATE UNIQUE INDEX uq_app_users_external_key ON app_users(external_key) WHERE external_key IS NOT NULL;
CREATE INDEX idx_app_user_roles_role ON app_user_roles(app_role_id);
CREATE INDEX idx_app_user_groups_group ON app_user_groups(app_group_id);
CREATE INDEX idx_app_group_roles_role ON app_group_roles(app_role_id);
CREATE INDEX idx_app_user_responsibilities_responsibility ON app_user_responsibilities(app_responsibility_id);
CREATE INDEX idx_app_group_responsibilities_responsibility ON app_group_responsibilities(app_responsibility_id);
CREATE INDEX idx_workflows_uid ON workflows(uid);
CREATE INDEX idx_workflows_created_at ON workflows(created_at DESC);
CREATE INDEX idx_workflow_answers_workflow_id ON workflow_answers(workflow_id);
CREATE INDEX idx_workflow_answers_key ON workflow_answers(answer_key);
CREATE INDEX idx_workflow_answer_visibility_rules_definition
    ON workflow_answer_visibility_rules(answer_definition_id);
CREATE INDEX idx_workflow_answer_reset_rules_definition
    ON workflow_answer_reset_rules(answer_definition_id);
CREATE INDEX idx_task_template_conditions_template_id ON task_template_conditions(task_template_id);
CREATE INDEX idx_workflow_tasks_workflow_id ON workflow_tasks(workflow_id);
CREATE INDEX idx_workflow_tasks_workflow_task_key ON workflow_tasks(workflow_id, task_key);
CREATE INDEX idx_task_assignments_task_id ON task_assignments(workflow_task_id);
CREATE INDEX idx_workflow_task_comments_task_id ON workflow_task_comments(workflow_task_id, created_at DESC);
CREATE UNIQUE INDEX uq_task_assignments_primary_per_task
    ON task_assignments(workflow_task_id)
    WHERE is_primary = TRUE;
CREATE INDEX idx_workflow_audit_log_workflow_id ON workflow_audit_log(workflow_id, created_at DESC);
CREATE INDEX idx_workflow_notifications_workflow_id ON workflow_notifications(workflow_id);
