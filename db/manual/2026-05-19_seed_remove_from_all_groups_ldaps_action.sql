-- Offboarding-Slice: Action-Definition fuer RemoveFromAllGroupsLdaps.
--
-- Entfernt einen AD-User aus allen Gruppen via LDAPS. Laeuft im Windows-Worker unter gMSA.
-- Sucht alle Gruppen mit LDAP-Filter `(&(objectClass=group)(member=<dn>))` und entfernt den
-- User pro Gruppe via ModifyRequest (Operation=Delete). Idempotent: Code 16 (NoSuchAttribute)
-- = bereits kein Mitglied -> AlreadyRemoved.
--
-- Pflicht-Payload:
--   userDistinguishedName: string (DN des Users)
--
-- Output:
--   userDistinguishedName: DN
--   removed: string[] (neu entfernte Gruppen-DNs)
--   alreadyRemoved: string[] (bereits nicht mehr Mitglied)
--   failed: object[] (fehlgeschlagene Gruppen mit Fehlerdetails)
--
-- FailureKind-Vertrag:
--   Payload-Validierung, LDAP-Codes 49/50/32/21/19 (permanent) -> permanent
--   Partielle Fehler mit mindestens einem permanent           -> permanent
--   Alles andere                                              -> transient
--
-- Anwendung: psql "$DATABASE_URL" -f db/manual/2026-05-19_seed_remove_from_all_groups_ldaps_action.sql

INSERT INTO public.action_definitions
    (action_key, name, description, handler_type, parameter_schema_json,
     is_active, requires_approval, is_idempotent, created_at, updated_at, target_runtime)
VALUES
    (
        'RemoveFromAllGroupsLdaps',
        'Remove from All Groups (LDAPS)',
        'Entfernt einen AD-User aus allen Gruppen via LDAPS gegen den on-prem-DC. Laeuft im Windows-Worker ' ||
        'unter gMSA. Sucht per LDAP-Filter alle Gruppen und entfernt den User pro Gruppe. Idempotent: Code 16 ' ||
        '(NoSuchAttribute = bereits kein Mitglied) wird als AlreadyRemoved behandelt. Pflicht-Payload: ' ||
        'userDistinguishedName. Output: removed, alreadyRemoved, failed (je string-Array bzw. Fehler-Array).',
        'windows_worker_ldaps',
        '{"type":"object","required":["userDistinguishedName"],"additionalProperties":true}'::jsonb,
        true,
        false,
        true,
        NOW(),
        NOW(),
        'windows_worker'
    )
ON CONFLICT (action_key) DO NOTHING;
