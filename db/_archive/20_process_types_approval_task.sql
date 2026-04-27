ALTER TABLE process_types
    ADD COLUMN IF NOT EXISTS approval_task_template_key VARCHAR(120);

UPDATE process_types
SET approval_task_template_key = 'supervisor_fills_document'
WHERE requires_supervisor_step = TRUE
  AND approval_task_template_key IS NULL;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'process_types_supervisor_step_requires_approval_task'
    ) THEN
        ALTER TABLE process_types
            ADD CONSTRAINT process_types_supervisor_step_requires_approval_task
                CHECK (NOT requires_supervisor_step OR approval_task_template_key IS NOT NULL);
    END IF;
END $$;
