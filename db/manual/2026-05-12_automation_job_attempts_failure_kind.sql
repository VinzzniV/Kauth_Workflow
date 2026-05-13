-- Migrationspfad-Etappe 9a Schritt 4 Sub-A: failure_kind-Marker am Attempt.
--
-- Worker (oder spaeter Linux-Handler) klassifizieren einen Failure-Attempt als 'permanent' oder
-- 'transient'. WorkflowAutomationRetryPolicy unterscheidet: 'permanent' -> sofort FinalFail
-- (ueberschreibt is_idempotent + attemptNumber); 'transient' / NULL -> bestehende Logik.
--
-- Streng idempotent: Spalte und Named-Constraint werden getrennt geprueft, damit auch der
-- Teilzustand "Spalte existiert, Constraint fehlt" durch Re-Run repariert wird.
--
-- Anwendung:  psql "$DATABASE_URL" -f db/manual/2026-05-12_automation_job_attempts_failure_kind.sql

ALTER TABLE automation_job_attempts
    ADD COLUMN IF NOT EXISTS failure_kind varchar(20) NULL;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'chk_automation_job_attempts_failure_kind'
          AND conrelid = 'public.automation_job_attempts'::regclass
    ) THEN
        ALTER TABLE automation_job_attempts
            ADD CONSTRAINT chk_automation_job_attempts_failure_kind
            CHECK (failure_kind IS NULL OR failure_kind IN ('permanent', 'transient'));
    END IF;
END $$;
