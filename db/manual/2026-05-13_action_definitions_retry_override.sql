-- Migrationspfad-Etappe 9a Schritt 7 Sub-A: per-Action Retry-Override.
--
-- Zwei optionale Spalten auf `action_definitions`:
--   * max_attempts_override                       int NULL  -> max. Versuche
--   * subsequent_retry_delay_seconds_override     int NULL  -> Backoff fuer Retries ab Versuch 2
--
-- NULL = "globalen Default aus WorkflowAutomationRetrySettings verwenden". Bestehende Actions
-- bleiben damit unveraendert (heutiges Verhalten = 3 Versuche x 60s/300s = ~6 min Budget).
--
-- Erste Nutzung: CreateMailboxGraph (Schritt 7 Sub-C) setzt 10 / 300, damit Entra-Connect-Sync-
-- Lag (~30 min) im Retry-Budget aufgefangen wird (60s + 8x300s = ~41 min Wartezeit).
--
-- Idempotent via IF NOT EXISTS.
--
-- Anwendung:  psql "$DATABASE_URL" -f db/manual/2026-05-13_action_definitions_retry_override.sql

ALTER TABLE public.action_definitions
    ADD COLUMN IF NOT EXISTS max_attempts_override integer NULL;

ALTER TABLE public.action_definitions
    ADD COLUMN IF NOT EXISTS subsequent_retry_delay_seconds_override integer NULL;
