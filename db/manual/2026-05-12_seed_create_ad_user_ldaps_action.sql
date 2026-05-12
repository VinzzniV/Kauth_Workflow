-- Migrationspfad-Etappe 9a Schritt 3 (Sub-B): Action-Definition fuer den ersten echten
-- AD-Schreib-Handler `CreateAdUserLdaps` (LDAPS gegen den on-prem-DC; Worker laeuft unter gMSA).
--
-- Reine Datenmigration: kein Schema-Drift gegenueber db/01_schema.sql. Idempotent via
-- ON CONFLICT (action_key) DO NOTHING — bestehende DBs ziehen den Eintrag ohne Seed-Reload.
--
-- Parallele Action zur ID=1 `CreateAdUser` (simulated_directory, target_runtime=NULL). Die
-- alte Simulation bleibt fuer Bestands-Workflows; Migration der Workflow-Definitionen erfolgt
-- selektiv pro Workflow.

INSERT INTO public.action_definitions
    (id, action_key, name, description, handler_type, parameter_schema_json,
     is_active, requires_approval, is_idempotent, created_at, updated_at, target_runtime)
OVERRIDING SYSTEM VALUE VALUES
    (7,
     'CreateAdUserLdaps',
     'Create AD User (LDAPS)',
     'Echte AD-User-Anlage via LDAPS gegen on-prem-DC. Laeuft im Windows-Worker unter gMSA. ' ||
     'Pflicht-Payload-Felder: samAccountName, userPrincipalName, displayName, givenName, surname, mail, targetOu. ' ||
     'Optional: employeeNumber. Output liefert distinguishedName, samAccountName, userPrincipalName und ' ||
     'temporaryPassword (Force-Change-at-Next-Logon).',
     'windows_worker_ldaps',
     '{"type":"object","required":["samAccountName","userPrincipalName","displayName","givenName","surname","mail","targetOu"],"additionalProperties":true}'::jsonb,
     true,
     false,
     true,
     NOW(),
     NOW(),
     'windows_worker')
ON CONFLICT (action_key) DO NOTHING;

-- Sequence-Cursor nachfuehren, damit IDENTITY-Generated-Always-Spalten beim naechsten regulaeren
-- INSERT nicht in einen Konflikt laufen. setval-auf-MAX deckt den Fall ab, dass der Eintrag
-- bereits per Seed-Reload mit ID 7 existiert und die Sequenz unterhalb liegt.
SELECT setval(
    pg_get_serial_sequence('public.action_definitions', 'id'),
    GREATEST(
        (SELECT COALESCE(MAX(id), 1) FROM public.action_definitions),
        (SELECT last_value FROM public.action_definitions_id_seq)
    )
);
