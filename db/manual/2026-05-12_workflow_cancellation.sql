-- Manual migration for existing databases created before 2026-05-12.
-- Fresh databases already get the new columns and status values from db/01_schema.sql.
--
-- Adds Workflow-Storno (cancel) capability:
--   * 4 new columns on public.workflows: cancelled_at, cancelled_by_person_id,
--     cancellation_reason_code, cancellation_reason_detail
--   * extends workflows.status check constraint with 'cancelled' (and 'archived',
--     which is set today via archived_at without being in the constraint)
--   * extends workflow_tasks.status check constraint with 'cancelled' so open
--     tasks of a cancelled workflow can be transitioned to a terminal state
--
-- Usage:
--   psql "$DATABASE_URL" -f db/manual/2026-05-12_workflow_cancellation.sql

DO $$
BEGIN
    ALTER TABLE public.workflows
        ADD COLUMN IF NOT EXISTS cancelled_at timestamptz;
    ALTER TABLE public.workflows
        ADD COLUMN IF NOT EXISTS cancelled_by_person_id bigint;
    ALTER TABLE public.workflows
        ADD COLUMN IF NOT EXISTS cancellation_reason_code varchar(40);
    ALTER TABLE public.workflows
        ADD COLUMN IF NOT EXISTS cancellation_reason_detail text;

    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'workflows_cancelled_by_person_fkey'
    ) THEN
        ALTER TABLE public.workflows
            ADD CONSTRAINT workflows_cancelled_by_person_fkey
            FOREIGN KEY (cancelled_by_person_id) REFERENCES public.people(id);
    END IF;

    IF EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'workflows_status_check'
          AND conrelid = 'public.workflows'::regclass
    ) THEN
        ALTER TABLE public.workflows DROP CONSTRAINT workflows_status_check;
    END IF;

    ALTER TABLE public.workflows
        ADD CONSTRAINT workflows_status_check
        CHECK (status::text = ANY (ARRAY[
            'draft'::character varying,
            'in_progress'::character varying,
            'waiting_for_supervisor'::character varying,
            'waiting_for_department'::character varying,
            'completed'::character varying,
            'cancelled'::character varying
        ]::text[]));

    IF EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'workflow_tasks_status_check'
          AND conrelid = 'public.workflow_tasks'::regclass
    ) THEN
        ALTER TABLE public.workflow_tasks DROP CONSTRAINT workflow_tasks_status_check;
    END IF;

    ALTER TABLE public.workflow_tasks
        ADD CONSTRAINT workflow_tasks_status_check
        CHECK (status::text = ANY (ARRAY[
            'open'::character varying,
            'ready'::character varying,
            'in_progress'::character varying,
            'blocked'::character varying,
            'done'::character varying,
            'cancelled'::character varying
        ]::text[]));
END $$;
