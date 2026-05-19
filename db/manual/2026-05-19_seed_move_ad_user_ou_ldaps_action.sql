-- Change-Workflow-Slice: Action-Definition fuer MoveAdUserOuLdaps.
--
-- Verschiebt einen AD-User per LDAP ModifyDN in eine andere OU. RDN (CN) bleibt erhalten.
-- Laeuft im Windows-Worker unter gMSA via LDAPS gegen den on-prem-DC. Idempotent: User bereits
-- in Ziel-OU -> Success mit alreadyInTargetOu=true.
--
-- Pflicht-Payload:
--   userDistinguishedName: string (aktueller DN des Users)
--   targetOu: string (Ziel-OU-DN, z. B. "OU=Archiv,OU=Mitarbeiter,DC=example,DC=local")
--
-- Output:
--   distinguishedName: neuer DN (RDN,targetOu) oder unveraenderter DN bei AlreadyInTargetOu
--   fromOu: bisherige OU
--   toOu: Ziel-OU
--   alreadyInTargetOu: bool
--
-- FailureKind-Vertrag:
--   Payload-Validierung, User-Not-Found -> permanent
--   LDAP-Codes 49/50/32/21/19              -> permanent
--   Alles andere                           -> transient
--
-- Anwendung: psql "$DATABASE_URL" -f db/manual/2026-05-19_seed_move_ad_user_ou_ldaps_action.sql

INSERT INTO public.action_definitions
    (action_key, name, description, handler_type, parameter_schema_json,
     is_active, requires_approval, is_idempotent, created_at, updated_at, target_runtime)
VALUES
    (
        'MoveAdUserOuLdaps',
        'AD-User in OU verschieben (LDAPS)',
        'Verschiebt einen AD-User per LDAP ModifyDN in eine andere Organisationseinheit. ' ||
        'Der RDN (CN) bleibt unveraendert. Laeuft im Windows-Worker unter gMSA. ' ||
        'Idempotent: User bereits in Ziel-OU -> Success mit alreadyInTargetOu=true. ' ||
        'Pflicht-Payload: userDistinguishedName (aktueller DN), targetOu (Ziel-OU-DN). ' ||
        'Output: distinguishedName, fromOu, toOu, alreadyInTargetOu.',
        'windows_worker_ldaps',
        '{"type":"object","required":["userDistinguishedName","targetOu"],"additionalProperties":true}'::jsonb,
        true,
        false,
        true,
        NOW(),
        NOW(),
        'windows_worker'
    )
ON CONFLICT (action_key) DO NOTHING;
