-- Migrationspfad-Etappe 9a Schritt 7 Sub-C: Action-Definition fuer den ersten echten Linux-side
-- Mailbox-Provisioning-Handler `CreateMailboxGraph`.
--
-- Reine Datenmigration. Idempotent via ON CONFLICT (action_key) DO NOTHING. **Ohne feste ID**
-- (IDENTITY-Sequenz; vermeidet Konflikt mit Bestands-DBs).
--
-- Parallel zur Simulation 'CreateMailbox' (ID 2, simulated_mailbox, target_runtime=NULL).
--
-- Pflicht-Voraussetzungen (operativ):
--   * Graph App-Registrierung mit den least-privileged Application-Permissions
--       - User.Read.All                         (fuer GET /users/{upn})
--       - LicenseAssignment.ReadWrite.All       (fuer POST /users/{id}/assignLicense)
--     Hoeher privilegierte Alternativen (User.ReadWrite.All, Organization.ReadWrite.All,
--     Directory.ReadWrite.All) decken den Pfad ebenfalls ab, sind aber nicht der Primaervertrag.
--     Admin-Consent erforderlich.
--   * Exchange-Online-SKU im Tenant (siehe `Get-MgSubscribedSku` fuer verfuegbare SKU-IDs).
--   * Lizenz-Pool muss freie Lizenzen haben. CountViolation -> Permanent-Failure mit Pool-Hinweis.
--
-- Retry-Override (Schritt 7 Sub-A): max_attempts_override=10 + subsequent_retry_delay_seconds=300
-- = 60s + 8x300s = ~41 min Wartezeit-Budget bis zum 10. Versuch. Damit ist ein Standard-Entra-
-- Connect-Sync-Zyklus (~30 min) sicher abgedeckt.
--
-- Anwendung:  psql "$DATABASE_URL" -f db/manual/2026-05-13_seed_create_mailbox_graph_action.sql

INSERT INTO public.action_definitions
    (action_key, name, description, handler_type, parameter_schema_json,
     is_active, requires_approval, is_idempotent, created_at, updated_at, target_runtime,
     max_attempts_override, subsequent_retry_delay_seconds_override)
VALUES
    (
        'CreateMailboxGraph',
        'Create Exchange Mailbox (Graph)',
        'Weist einem in Entra existierenden User via Microsoft Graph eine Exchange-Online-Lizenz zu. ' ||
        'Die Mailbox wird durch Exchange Online automatisch provisioniert. Pflicht-Payload: ' ||
        'userPrincipalName, skuId (UUID). Output: primarySmtpAddress (echte beobachtete SMTP, kein ' ||
        'UPN-Fallback), licenseSkuId, assignedAtUtc. User-404 (Entra-Sync-Lag) -> Transient-Retry; ' ||
        'SMTP noch nicht publiziert -> Transient-Retry; Misconfig (SKU/Pool/Permission) -> Permanent. ' ||
        'Retry-Override: 10 Attempts mit 60s + 8x300s = ~41min Wartezeit-Budget fuer den Entra-Connect-' ||
        'Sync-Zyklus. Pflicht-Permissions: User.Read.All + LicenseAssignment.ReadWrite.All.',
        'graph_mailbox_provision',
        '{"type":"object","required":["userPrincipalName","skuId"],"additionalProperties":true}'::jsonb,
        true,
        false,
        true,
        NOW(),
        NOW(),
        NULL,
        10,
        300
    )
ON CONFLICT (action_key) DO NOTHING;
