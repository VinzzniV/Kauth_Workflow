-- Offboarding-Slice: Action-Definition fuer RemoveMailboxLicense.
--
-- Entfernt eine Exchange-Online-Lizenz von einem Entra-User via Microsoft Graph. Laeuft
-- Linux-seitig im API-Prozess (kein Worker). Idempotent: Lizenz nicht zugewiesen ->
-- Erfolg mit licenseNotAssigned=true.
--
-- Pflicht-Payload:
--   userPrincipalName: string (UPN-Format, z. B. aus CreateAdUserLdaps/CreateMailboxGraph-Output)
--   skuId: string (UUID, Exchange-Online-SKU)
--
-- Output:
--   userPrincipalName: UPN
--   licenseSkuId: entfernte SKU
--   licenseNotAssigned: bool (true wenn bereits nicht zugewiesen)
--   removedAtUtc: Zeitpunkt der Entfernung (null wenn licenseNotAssigned)
--
-- FailureKind-Vertrag:
--   Payload-Validierung, User-Not-Found, Permissions-Fehler (401/403) -> permanent
--   429, 5xx, Timeouts                                                 -> transient
--
-- Pflicht-Graph-Permissions (Application, Admin-Consent):
--   User.Read.All                       (GET /users/{upn})
--   LicenseAssignment.ReadWrite.All     (POST /users/{id}/assignLicense)
--
-- Anwendung: psql "$DATABASE_URL" -f db/manual/2026-05-19_seed_remove_mailbox_license_graph_action.sql

INSERT INTO public.action_definitions
    (action_key, name, description, handler_type, parameter_schema_json,
     is_active, requires_approval, is_idempotent, created_at, updated_at, target_runtime)
VALUES
    (
        'RemoveMailboxLicense',
        'Remove Exchange Mailbox License (Graph)',
        'Entfernt eine Exchange-Online-Lizenz von einem Entra-User via Microsoft Graph. Idempotent: Lizenz ' ||
        'nicht zugewiesen -> Erfolg mit licenseNotAssigned=true. Pflicht-Payload: userPrincipalName, skuId (UUID). ' ||
        'Output: userPrincipalName, licenseSkuId, licenseNotAssigned, removedAtUtc. ' ||
        'Pflicht-Permissions: User.Read.All + LicenseAssignment.ReadWrite.All (Admin-Consent).',
        'graph_mailbox_deprovision',
        '{"type":"object","required":["userPrincipalName","skuId"],"additionalProperties":true}'::jsonb,
        true,
        false,
        true,
        NOW(),
        NOW(),
        NULL
    )
ON CONFLICT (action_key) DO NOTHING;
