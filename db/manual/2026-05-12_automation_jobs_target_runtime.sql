-- Manual migration for existing databases created before Migrationspfad-Etappe 9a Schritt 2.
-- Fresh databases already get the new columns, view and index from db/01_schema.sql.
--
-- Adds Windows-Worker support to the Automation Layer:
--   * automation_jobs:
--       - target_runtime varchar(40)         NULL = Linux-API, 'windows_worker' = Windows-Worker
--       - claimed_at timestamptz             Lease-Spalte (Worker-Claim-Zeitpunkt)
--       - claimed_by varchar(120)            Lease-Spalte ("<hostname>:<pid>:<startup-uuid>")
--       - heartbeat_at timestamptz           Lease-Spalte (Worker-Heartbeat)
--       - completion_processed_at timestamptz  Sweeper-Idempotenz-Marker (Linux-API hat die Lifecycle-Folgewirkung gezogen)
--       - completion_claimed_at timestamptz    Sweeper-Claim-Lock (vermeidet Mehrfach-Trigger durch parallele Sweep-Zyklen)
--   * automation_job_attempts:
--       - output_json jsonb                  Persistente Ablage des Handler-Outputs (Success-Pfad)
--   * action_definitions:
--       - target_runtime varchar(40)         pro Action steuerbar, Linux-API liest dies beim INSERT INTO automation_jobs
--   * neue Indizes fuer Worker-Polling und Sweeper-Polling
--   * View automation_jobs_windows_worker  filtert auf target_runtime='windows_worker' fuer den Worker-Polling-User
--
-- Usage:
--   psql "$DATABASE_URL" -f db/manual/2026-05-12_automation_jobs_target_runtime.sql

DO $$
BEGIN
    -- automation_jobs
    ALTER TABLE public.automation_jobs
        ADD COLUMN IF NOT EXISTS target_runtime varchar(40);
    ALTER TABLE public.automation_jobs
        ADD COLUMN IF NOT EXISTS claimed_at timestamptz;
    ALTER TABLE public.automation_jobs
        ADD COLUMN IF NOT EXISTS claimed_by varchar(120);
    ALTER TABLE public.automation_jobs
        ADD COLUMN IF NOT EXISTS heartbeat_at timestamptz;
    ALTER TABLE public.automation_jobs
        ADD COLUMN IF NOT EXISTS completion_processed_at timestamptz;
    ALTER TABLE public.automation_jobs
        ADD COLUMN IF NOT EXISTS completion_claimed_at timestamptz;

    -- automation_job_attempts
    ALTER TABLE public.automation_job_attempts
        ADD COLUMN IF NOT EXISTS output_json jsonb;

    -- action_definitions
    ALTER TABLE public.action_definitions
        ADD COLUMN IF NOT EXISTS target_runtime varchar(40);
END
$$;

-- Indizes (CREATE INDEX IF NOT EXISTS ist out-of-DO-Block sauberer)
CREATE INDEX IF NOT EXISTS idx_automation_jobs_windows_worker_pending
    ON public.automation_jobs (target_runtime, status, available_at);

CREATE INDEX IF NOT EXISTS idx_automation_jobs_external_completion_pending
    ON public.automation_jobs (status, target_runtime, completion_processed_at);

-- View fuer den Worker-Polling-User
CREATE OR REPLACE VIEW public.automation_jobs_windows_worker AS
SELECT
    id,
    workflow_id,
    workflow_node_instance_id,
    workflow_node_action_id,
    action_definition_id,
    status,
    payload_json,
    available_at,
    created_at,
    started_at,
    completed_at,
    target_runtime,
    claimed_at,
    claimed_by,
    heartbeat_at,
    completion_processed_at,
    completion_claimed_at
FROM public.automation_jobs
WHERE target_runtime = 'windows_worker';
