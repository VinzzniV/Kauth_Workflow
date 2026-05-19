-- Offboarding-Slice: Action-Definition fuer DisableAdUserLdaps.
--
-- Deaktiviert einen bestehenden AD-User via LDAPS gegen den on-prem-DC. Laeuft im Windows-Worker
-- unter gMSA. Setzt das ACCOUNTDISABLE-Bit (userAccountControl |= 0x2). Idempotent: User bereits
-- deaktiviert -> Erfolg mit alreadyDisabled=true.
--
-- Pflicht-Payload:
--   userDistinguishedName: string (DN des Users, typischerweise aus CreateAdUserLdaps-Output)
--
-- Output:
--   distinguishedName: DN des deaktivierten Users
--   alreadyDisabled: bool (true wenn vorher schon deaktiviert)
--
-- FailureKind-Vertrag:
--   Payload-Validierung, User-Not-Found -> permanent
--   LDAP-Codes 49/50/21/19              -> permanent
--   Alles andere                         -> transient
--
-- Anwendung: psql "$DATABASE_URL" -f db/manual/2026-05-19_seed_disable_ad_user_ldaps_action.sql

INSERT INTO public.action_definitions
    (action_key, name, description, handler_type, parameter_schema_json,
     is_active, requires_approval, is_idempotent, created_at, updated_at, target_runtime)
VALUES
    (
        'DisableAdUserLdaps',
        'Disable AD User (LDAPS)',
        'Deaktiviert einen AD-User via LDAPS gegen den on-prem-DC. Laeuft im Windows-Worker unter gMSA. ' ||
        'Setzt das ACCOUNTDISABLE-Bit (userAccountControl |= 0x2). Idempotent: bereits deaktivierter User ' ||
        'liefert Success mit alreadyDisabled=true. Pflicht-Payload: userDistinguishedName (DN des Users). ' ||
        'Output: distinguishedName, alreadyDisabled.',
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
