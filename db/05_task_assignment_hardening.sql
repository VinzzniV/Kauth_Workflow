-- Härtet bestehende Datenbestaende fuer task_assignments nach.
-- Auf frischen Datenbanken ist der Effekt weitgehend ein No-Op, weil 01_schema.sql die Regeln bereits mitbringt.

-- Zuerst offensichtliche Inkonsistenzen auf das Zielmodell normalisieren.
UPDATE task_assignments
SET assignment_type = 'user'
WHERE assignee_user_id IS NOT NULL
  AND assignee_responsibility_id IS NULL
  AND assignment_type <> 'user';

UPDATE task_assignments
SET assignment_type = 'responsibility'
WHERE assignee_responsibility_id IS NOT NULL
  AND assignee_user_id IS NULL
  AND assignment_type <> 'responsibility';

UPDATE task_assignments
SET assignee_responsibility_id = NULL
WHERE assignment_type = 'user'
  AND assignee_responsibility_id IS NOT NULL;

UPDATE task_assignments
SET assignee_user_id = NULL
WHERE assignment_type = 'responsibility'
  AND assignee_user_id IS NOT NULL;

DELETE FROM task_assignments
WHERE assignee_user_id IS NULL
  AND assignee_responsibility_id IS NULL;

-- Falls mehrere primaere Zuweisungen pro Task existieren, bleibt die zuletzt gesetzte erhalten.
WITH ranked_assignments AS (
    SELECT
        id,
        ROW_NUMBER() OVER (
            PARTITION BY workflow_task_id
            ORDER BY assigned_at DESC, id DESC
        ) AS row_number
    FROM task_assignments
    WHERE is_primary = TRUE
)
UPDATE task_assignments ta
SET is_primary = FALSE
FROM ranked_assignments ra
WHERE ta.id = ra.id
  AND ra.row_number > 1;

DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'task_assignments_assignee_user_id_fkey'
    ) THEN
        ALTER TABLE task_assignments
        DROP CONSTRAINT task_assignments_assignee_user_id_fkey;
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'fk_task_assignments_assignee_user'
    ) THEN
        ALTER TABLE task_assignments
        ADD CONSTRAINT fk_task_assignments_assignee_user
            FOREIGN KEY (assignee_user_id) REFERENCES app_users(id) ON DELETE RESTRICT;
    END IF;
END $$;

DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'task_assignments_assignee_responsibility_id_fkey'
    ) THEN
        ALTER TABLE task_assignments
        DROP CONSTRAINT task_assignments_assignee_responsibility_id_fkey;
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'fk_task_assignments_assignee_responsibility'
    ) THEN
        ALTER TABLE task_assignments
        ADD CONSTRAINT fk_task_assignments_assignee_responsibility
            FOREIGN KEY (assignee_responsibility_id) REFERENCES app_responsibilities(id) ON DELETE RESTRICT;
    END IF;
END $$;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'chk_task_assignments_target_matches_type'
    ) THEN
        ALTER TABLE task_assignments
        ADD CONSTRAINT chk_task_assignments_target_matches_type
            CHECK (
                (assignment_type = 'user' AND assignee_user_id IS NOT NULL AND assignee_responsibility_id IS NULL)
                OR
                (assignment_type = 'responsibility' AND assignee_responsibility_id IS NOT NULL AND assignee_user_id IS NULL)
            );
    END IF;
END $$;

CREATE UNIQUE INDEX IF NOT EXISTS uq_task_assignments_primary_per_task
    ON task_assignments(workflow_task_id)
    WHERE is_primary = TRUE;
