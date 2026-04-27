ALTER TABLE rotation_generated_tasks
    DROP CONSTRAINT IF EXISTS rotation_generated_tasks_rotation_station_id_fkey;

ALTER TABLE rotation_generated_tasks
    ADD CONSTRAINT rotation_generated_tasks_rotation_station_id_fkey
        FOREIGN KEY (rotation_station_id) REFERENCES rotation_stations(id) ON DELETE SET NULL;

ALTER TABLE rotation_generated_tasks
    ADD COLUMN IF NOT EXISTS trigger_type VARCHAR(16);

UPDATE rotation_generated_tasks
SET trigger_type = 'enter'
WHERE trigger_type IS NULL;

ALTER TABLE rotation_generated_tasks
    ALTER COLUMN trigger_type SET NOT NULL;

ALTER TABLE rotation_generated_tasks
    DROP CONSTRAINT IF EXISTS rotation_generated_tasks_trigger_type_check;

ALTER TABLE rotation_generated_tasks
    ADD CONSTRAINT rotation_generated_tasks_trigger_type_check
        CHECK (trigger_type IN ('enter', 'exit'));

ALTER TABLE rotation_generated_tasks
    ADD COLUMN IF NOT EXISTS anchor_date DATE;

UPDATE rotation_generated_tasks
SET anchor_date = COALESCE(due_date, CURRENT_DATE)
WHERE anchor_date IS NULL;

ALTER TABLE rotation_generated_tasks
    ALTER COLUMN anchor_date SET NOT NULL;

ALTER TABLE rotation_generated_tasks
    ADD COLUMN IF NOT EXISTS started_at TIMESTAMPTZ;

CREATE TABLE IF NOT EXISTS rotation_task_assignments (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    rotation_generated_task_id BIGINT NOT NULL REFERENCES rotation_generated_tasks(id) ON DELETE CASCADE,
    assignee_user_id BIGINT REFERENCES app_users(id) ON DELETE SET NULL,
    assignee_responsibility_id INTEGER REFERENCES app_responsibilities(id) ON DELETE SET NULL,
    assignment_type VARCHAR(32) NOT NULL CHECK (assignment_type IN ('user', 'responsibility')),
    is_primary BOOLEAN NOT NULL DEFAULT FALSE,
    assigned_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    completed_at TIMESTAMPTZ,
    CHECK (
        (assignment_type = 'user' AND assignee_user_id IS NOT NULL AND assignee_responsibility_id IS NULL)
        OR (assignment_type = 'responsibility' AND assignee_responsibility_id IS NOT NULL)
    )
);

CREATE TABLE IF NOT EXISTS rotation_task_comments (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    rotation_generated_task_id BIGINT NOT NULL REFERENCES rotation_generated_tasks(id) ON DELETE CASCADE,
    author_user_id BIGINT REFERENCES app_users(id) ON DELETE SET NULL,
    comment_text TEXT NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE UNIQUE INDEX IF NOT EXISTS uq_rotation_generated_tasks_station_template
    ON rotation_generated_tasks(rotation_station_id, template_id)
    WHERE rotation_station_id IS NOT NULL
      AND template_id IS NOT NULL;

CREATE INDEX IF NOT EXISTS idx_rotation_task_assignments_task_id
    ON rotation_task_assignments(rotation_generated_task_id);

CREATE UNIQUE INDEX IF NOT EXISTS uq_rotation_task_assignments_primary
    ON rotation_task_assignments(rotation_generated_task_id)
    WHERE is_primary = TRUE;

CREATE INDEX IF NOT EXISTS idx_rotation_task_comments_task_id
    ON rotation_task_comments(rotation_generated_task_id);
