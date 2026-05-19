-- Change-Workflow-Slice: Action-Definition fuer UpdateAdUserAttributesLdaps.
--
-- Aktualisiert eine feste Whitelist von AD-User-Attributen via LDAPS ModifyRequest (Replace).
-- Laeuft im Windows-Worker unter gMSA. Kein UPN/mail/displayName/cn/sAMAccountName (Identity-Slice).
--
-- Whitelist-Payload-Felder (alle optional, mindestens eins Pflicht):
--   managerDistinguishedName: string | null  (LDAP: manager)
--   department:               string | null  (LDAP: department)
--   title:                    string | null  (LDAP: title)
--   description:              string | null  (LDAP: description)
--
-- Pflicht-Payload:
--   userDistinguishedName: string (DN des Users)
--   + mindestens ein Whitelist-Feld
--
-- Clear-Semantik: JSON null -> Attribut wird in AD geloescht (Replace auf leere Liste).
-- Fehlendes Key -> Attribut wird nicht angefasst.
--
-- Idempotenz: alle angeforderten Werte schon gesetzt -> NoChangesNeeded -> Success.
--
-- Output:
--   distinguishedName: DN des Users
--   changedAttributes: string[] (LDAP-Attributnamen die tatsaechlich geaendert wurden)
--   noChangesNeeded: bool
--
-- FailureKind-Vertrag:
--   Payload-Validierung, User-Not-Found -> permanent
--   LDAP-Codes 49/50/32/21/19              -> permanent
--   Alles andere                           -> transient
--
-- Anwendung: psql "$DATABASE_URL" -f db/manual/2026-05-19_seed_update_ad_user_attributes_ldaps_action.sql

INSERT INTO public.action_definitions
    (action_key, name, description, handler_type, parameter_schema_json,
     is_active, requires_approval, is_idempotent, created_at, updated_at, target_runtime)
VALUES
    (
        'UpdateAdUserAttributesLdaps',
        'AD-User-Attribute aktualisieren (LDAPS)',
        'Aktualisiert eine Whitelist von AD-User-Attributen via LDAPS. Whitelist: ' ||
        'managerDistinguishedName (->manager), department, title, description. ' ||
        'Kein UPN/mail/displayName/cn/sAMAccountName. ' ||
        'JSON null = Attribut loeschen. Fehlendes Key = nicht anfassen. ' ||
        'Idempotent: keine Aenderung noetig -> Success mit noChangesNeeded=true. ' ||
        'Pflicht-Payload: userDistinguishedName + mindestens ein Whitelist-Feld. ' ||
        'Output: distinguishedName, changedAttributes[], noChangesNeeded.',
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
