-- Migrationspfad-Etappe 9a Schritt 5 Sub-C: Action-Definition fuer den ersten echten Linux-side
-- Action-Handler `SendWelcomeMailGraph` (Welcome-Mail-Versand via Microsoft Graph App-only).
--
-- Reine Datenmigration. Idempotent via ON CONFLICT (action_key) DO NOTHING. **Ohne feste ID**.
--
-- Parallel zur Simulation 'SendWelcomeMail' (ID 5, simulated_notification, target_runtime=NULL).
-- Bewusst KEIN notification_templates-Row-INSERT — der Catalog-Default (NotificationTemplateCatalog
-- 'welcome_mail') ist die kanonische Default-Quelle. Admins koennen einen Override ueber die UI
-- anlegen, wenn gewuenscht.
--
-- Anwendung:  psql "$DATABASE_URL" -f db/manual/2026-05-12_seed_send_welcome_mail_graph_action.sql

INSERT INTO public.action_definitions
    (action_key, name, description, handler_type, parameter_schema_json,
     is_active, requires_approval, is_idempotent, created_at, updated_at, target_runtime)
VALUES
    (
        'SendWelcomeMailGraph',
        'Send Welcome Mail (Graph)',
        'Versendet eine Willkommens-Mail an einen frisch angelegten User via Microsoft Graph ' ||
        '(App-only Auth). Linux-side; baut auf NotificationTemplateCatalog (welcome_mail) und ' ||
        'NotificationEmailTemplateBuilder auf. Pflicht-Payload-Felder: toAddress, recipientName, ' ||
        'firstName, lastName, userPrincipalName. Output liefert messageId (kann NULL sein) + ' ||
        'sentTo + sentAtUtc. Idempotenz wird via is_idempotent=true erlaubt; Permanent-Failures ' ||
        '(invalide Adresse, Auth-Fehler) gehen direkt auf FinalFail, transient (Graph 429/5xx) ' ||
        'wird retried. Bewusst KEIN temporaryPassword-Placeholder im Template (Vault-Slice in ' ||
        'Schritt 6 ergaenzt das spaeter).',
        'graph_welcome_mail',
        '{"type":"object","required":["toAddress","recipientName","firstName","lastName","userPrincipalName"],"additionalProperties":true}'::jsonb,
        true,
        false,
        true,
        NOW(),
        NOW(),
        NULL
    )
ON CONFLICT (action_key) DO NOTHING;
