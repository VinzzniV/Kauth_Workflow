ALTER TABLE task_templates
    ADD COLUMN IF NOT EXISTS due_in_days INTEGER;

ALTER TABLE workflow_tasks
    ADD COLUMN IF NOT EXISTS due_in_days INTEGER;

ALTER TABLE workflow_tasks
    ADD COLUMN IF NOT EXISTS due_at TIMESTAMPTZ;

UPDATE task_templates
SET due_in_days = CASE template_key
    WHEN 'supervisor_fills_document' THEN 2
    WHEN 'hardware_procure' THEN 5
    WHEN 'hardware_handover' THEN 1
    ELSE 3
END
WHERE due_in_days IS NULL;

UPDATE workflow_tasks wt
SET due_in_days = tt.due_in_days
FROM task_templates tt
WHERE wt.task_template_id = tt.id
  AND wt.due_in_days IS NULL;

UPDATE workflow_tasks wt
SET due_in_days = tt.due_in_days
FROM task_templates tt
WHERE wt.task_template_id IS NULL
  AND tt.template_key = wt.task_key
  AND wt.due_in_days IS NULL;

UPDATE workflow_tasks
SET due_at = COALESCE(ready_at, created_at) + (due_in_days * INTERVAL '1 day')
WHERE due_in_days IS NOT NULL
  AND due_at IS NULL
  AND status IN ('ready', 'in_progress', 'blocked', 'done');
