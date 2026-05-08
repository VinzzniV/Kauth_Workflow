-- Manual migration for existing databases created before 2026-05-08.
-- Fresh databases already get the new column name from db/01_schema.sql.
--
-- Usage:
--   psql "$DATABASE_URL" -f db/manual/2026-05-08_rename_approval_task_template_key_to_approval_spec_key.sql

DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'workflow_definitions'
          AND column_name = 'approval_task_template_key'
    ) AND NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'workflow_definitions'
          AND column_name = 'approval_spec_key'
    ) THEN
        ALTER TABLE public.workflow_definitions
            RENAME COLUMN approval_task_template_key TO approval_spec_key;
        RAISE NOTICE 'Renamed column approval_task_template_key -> approval_spec_key.';
    ELSE
        RAISE NOTICE 'Column rename skipped (already migrated or unexpected schema state).';
    END IF;

    IF EXISTS (
        SELECT 1
        FROM pg_constraint c
        JOIN pg_class t ON t.oid = c.conrelid
        JOIN pg_namespace n ON n.oid = t.relnamespace
        WHERE n.nspname = 'public'
          AND t.relname = 'workflow_definitions'
          AND c.conname = 'workflow_definitions_supervisor_step_requires_approval_task'
    ) AND NOT EXISTS (
        SELECT 1
        FROM pg_constraint c
        JOIN pg_class t ON t.oid = c.conrelid
        JOIN pg_namespace n ON n.oid = t.relnamespace
        WHERE n.nspname = 'public'
          AND t.relname = 'workflow_definitions'
          AND c.conname = 'workflow_definitions_supervisor_step_requires_approval_spec'
    ) THEN
        ALTER TABLE public.workflow_definitions
            RENAME CONSTRAINT workflow_definitions_supervisor_step_requires_approval_task
            TO workflow_definitions_supervisor_step_requires_approval_spec;
        RAISE NOTICE 'Renamed supervisor-step constraint to approval_spec variant.';
    ELSE
        RAISE NOTICE 'Constraint rename skipped (already migrated or unexpected schema state).';
    END IF;
END $$;
