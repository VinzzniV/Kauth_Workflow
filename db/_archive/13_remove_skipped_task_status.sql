UPDATE workflow_task_dependencies
SET required_status = 'done'
WHERE required_status IN ('skipped', 'cancelled');

UPDATE task_template_dependencies
SET required_status = 'done'
WHERE required_status IN ('skipped', 'cancelled');

DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'workflow_tasks' AND column_name = 'cancelled_at'
    ) THEN
        UPDATE workflow_tasks
        SET
            status = 'done',
            completed_at = COALESCE(completed_at, cancelled_at, NOW()),
            cancelled_at = NULL
        WHERE status IN ('skipped', 'cancelled');

        UPDATE workflow_tasks wt
        SET
            status = 'done',
            completed_at = COALESCE(wt.completed_at, wt.cancelled_at, w.cancelled_at, NOW()),
            cancelled_at = NULL
        FROM workflows w
        WHERE wt.workflow_id = w.id
          AND w.status = 'cancelled'
          AND wt.status <> 'done';
    ELSE
        UPDATE workflow_tasks
        SET status = 'done', completed_at = COALESCE(completed_at, NOW())
        WHERE status IN ('skipped', 'cancelled');
    END IF;
END $$;

DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'workflows' AND column_name = 'cancelled_at'
    ) THEN
        UPDATE workflows
        SET
            status = 'completed',
            completed_at = COALESCE(completed_at, cancelled_at, NOW()),
            cancelled_at = NULL
        WHERE status = 'cancelled';
    ELSE
        UPDATE workflows
        SET status = 'completed', completed_at = COALESCE(completed_at, NOW())
        WHERE status = 'cancelled';
    END IF;
END $$;

ALTER TABLE workflows
    DROP CONSTRAINT IF EXISTS workflows_status_check;

ALTER TABLE workflows
    ADD CONSTRAINT workflows_status_check
        CHECK (status IN ('draft', 'in_progress', 'waiting_for_supervisor', 'waiting_for_department', 'completed'));

ALTER TABLE workflow_tasks
    DROP CONSTRAINT IF EXISTS workflow_tasks_status_check;

ALTER TABLE workflow_tasks
    ADD CONSTRAINT workflow_tasks_status_check
        CHECK (status IN ('open', 'ready', 'in_progress', 'blocked', 'done'));

ALTER TABLE workflow_task_dependencies
    DROP CONSTRAINT IF EXISTS workflow_task_dependencies_required_status_check;

ALTER TABLE workflow_task_dependencies
    ADD CONSTRAINT workflow_task_dependencies_required_status_check
        CHECK (required_status IN ('open', 'ready', 'in_progress', 'blocked', 'done'));

ALTER TABLE task_template_dependencies
    DROP CONSTRAINT IF EXISTS task_template_dependencies_required_status_check;

ALTER TABLE task_template_dependencies
    ADD CONSTRAINT task_template_dependencies_required_status_check
        CHECK (required_status IN ('open', 'ready', 'in_progress', 'blocked', 'done'));
