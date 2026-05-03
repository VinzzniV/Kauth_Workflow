-- 2026-05-02
-- L5: Optionales Begruendungsfeld fuer Permission-Audit-Eintraege.
-- Admin-Endpoints koennen einen Freitext-Reason an das Audit weiterreichen,
-- damit nachvollziehbar bleibt, WARUM eine Berechtigung geaendert wurde.
-- Spalte ist NULLable und backwards-kompatibel: bestehende Inserts ohne reason funktionieren weiter.

ALTER TABLE auth_permission_audit_log ADD COLUMN IF NOT EXISTS reason text;
