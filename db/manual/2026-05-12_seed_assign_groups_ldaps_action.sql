-- Migrationspfad-Etappe 9a Schritt 5 Sub-B: Action-Definition fuer den zweiten echten LDAPS-
-- Handler `AssignGroupsLdaps` (LDAPS-ModifyRequest auf Group-DNs, Member-Add pro Group).
--
-- Reine Datenmigration. Idempotent via ON CONFLICT (action_key) DO NOTHING. **Ohne feste ID** —
-- IDENTITY vergibt frei, vermeidet ID-Kollisionen mit Bestands-Dev-DBs.
--
-- Parallel zur Simulation 'AssignGroups' (ID 3, simulated_directory_groups, target_runtime=NULL).
-- Workflow-Designer waehlt aktiv die LDAPS-Variante, wenn ein echter Worker-Pfad gewuenscht ist.
--
-- Anwendung:  psql "$DATABASE_URL" -f db/manual/2026-05-12_seed_assign_groups_ldaps_action.sql

INSERT INTO public.action_definitions
    (action_key, name, description, handler_type, parameter_schema_json,
     is_active, requires_approval, is_idempotent, created_at, updated_at, target_runtime)
VALUES
    (
        'AssignGroupsLdaps',
        'Assign AD Groups (LDAPS)',
        'Fuegt den AD-User in N AD-Gruppen ein via LDAPS-ModifyRequest. Laeuft im Windows-Worker ' ||
        'unter gMSA. Pflicht-Payload-Felder: userDistinguishedName (typischerweise via ' ||
        'created_ad_user-Source aus vorgaengigem CreateAdUserLdaps), groupDistinguishedNames (string array). ' ||
        'Output liefert newlyAdded + alreadyMember + ggf. failed-Liste mit LDAP-ResultCode. Idempotent ' ||
        'durch AD-Code-20-Mapping (AttributeOrValueAlreadyExists -> alreadyMember).',
        'windows_worker_ldaps_groups',
        '{"type":"object","required":["userDistinguishedName","groupDistinguishedNames"],"additionalProperties":true}'::jsonb,
        true,
        false,
        true,
        NOW(),
        NOW(),
        'windows_worker'
    )
ON CONFLICT (action_key) DO NOTHING;
