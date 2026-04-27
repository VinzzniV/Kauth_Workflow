CREATE TABLE IF NOT EXISTS process_types (
    id INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    key VARCHAR(120) NOT NULL UNIQUE,
    name VARCHAR(180) NOT NULL,
    description TEXT,
    requires_supervisor_step BOOLEAN NOT NULL DEFAULT FALSE,
    approval_task_template_key VARCHAR(120),
    requires_target_person BOOLEAN NOT NULL DEFAULT FALSE,
    icon_key VARCHAR(80),
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    sort_order INTEGER NOT NULL DEFAULT 0,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT process_types_supervisor_step_requires_approval_task
        CHECK (NOT requires_supervisor_step OR approval_task_template_key IS NOT NULL)
);

INSERT INTO process_types (
    key,
    name,
    description,
    requires_supervisor_step,
    approval_task_template_key,
    requires_target_person,
    icon_key,
    is_active,
    sort_order
)
VALUES (
    'onboarding',
    'Onboarding',
    'Start eines neuen Mitarbeiters mit Aufgaben fuer HR, Fuehrungskraft und Fachbereiche.',
    TRUE,
    'supervisor_fills_document',
    FALSE,
    'identitat',
    TRUE,
    10
)
ON CONFLICT (key) DO UPDATE
SET
    name = EXCLUDED.name,
    description = EXCLUDED.description,
    requires_supervisor_step = EXCLUDED.requires_supervisor_step,
    approval_task_template_key = EXCLUDED.approval_task_template_key,
    requires_target_person = EXCLUDED.requires_target_person,
    icon_key = EXCLUDED.icon_key,
    is_active = EXCLUDED.is_active,
    sort_order = EXCLUDED.sort_order;
