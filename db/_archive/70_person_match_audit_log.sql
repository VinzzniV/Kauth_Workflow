-- 2026-05-02
-- HQ3: Audit-Trail fuer Personen-Matching beim Directory-Sync.
--
-- Bisher haben sowohl die Bulk-Verlinkung (link_people_by_employee_number)
-- als auch der Per-User-Pfad (EnsureDirectoryManagedPersonRecordAsync) eine
-- Match-Entscheidung getroffen, ohne sie zu protokollieren. Wenn das Matching
-- die falsche Person trifft (z.B. weil employee_number-Werte mehrdeutig sind
-- oder eine fallback-Strategie greift), bleibt das stumm.
--
-- Diese Tabelle protokolliert pro Match-Entscheidung:
--   * matched_person_id: die getroffene Person (NULL falls neu angelegt)
--   * app_user_id / directory_identity_id / employee_number: die Eingangs-IDs
--   * match_strategy: nach welcher Logik gematcht wurde
--   * match_score: 1.00 = stabiler ID-Match, niedriger = fuzziger
--   * fallback_used: wurde ein Fallback gegenueber dem Primaer-Pfad genutzt
--   * source: aufrufender Pfad (bulk vs. per-user)
--   * detail: optionale JSON-Zusatzinfo
--
-- Idempotent: CREATE TABLE IF NOT EXISTS und CREATE INDEX IF NOT EXISTS.

CREATE TABLE IF NOT EXISTS person_match_audit_log (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    matched_person_id bigint NULL,
    app_user_id bigint NULL,
    directory_identity_id bigint NULL,
    employee_number int NULL,
    match_strategy varchar(40) NOT NULL,
    match_score numeric(5, 2) NULL,
    fallback_used boolean NOT NULL DEFAULT false,
    source varchar(40) NOT NULL,
    detail jsonb NULL,
    created_at timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS person_match_audit_log_matched_person_idx
    ON person_match_audit_log (matched_person_id)
    WHERE matched_person_id IS NOT NULL;

CREATE INDEX IF NOT EXISTS person_match_audit_log_created_idx
    ON person_match_audit_log (created_at DESC);

CREATE INDEX IF NOT EXISTS person_match_audit_log_strategy_idx
    ON person_match_audit_log (match_strategy, created_at DESC);
