-- Migrationspfad-Etappe 9a Schritt 6 Sub-C: erweitert das Payload-Schema der Action
-- 'SendWelcomeMailGraph' um das Pflicht-Feld `credentialVaultId` (UUID-Pointer ins
-- temporary_credentials-Vault, kommt ueber `created_ad_user.credentialVaultId`-Mapping aus
-- dem CreateAdUserLdaps-Output).
--
-- Reine Daten-Migration. Idempotent: re-run setzt einfach denselben JSON erneut.
--
-- Anwendung:  psql "$DATABASE_URL" -f db/manual/2026-05-13_update_send_welcome_mail_graph_payload_schema.sql

UPDATE public.action_definitions
SET
    parameter_schema_json = '{"type":"object","required":["toAddress","recipientName","firstName","lastName","userPrincipalName","credentialVaultId"],"additionalProperties":true}'::jsonb,
    description = 'Versendet eine Willkommens-Mail an einen frisch angelegten User via Microsoft Graph ' ||
                  '(App-only Auth). Linux-side; baut auf NotificationTemplateCatalog (welcome_mail) und ' ||
                  'NotificationEmailTemplateBuilder auf. Pflicht-Payload-Felder: toAddress, recipientName, ' ||
                  'firstName, lastName, userPrincipalName, credentialVaultId (UUID). credentialVaultId kommt ' ||
                  'ueber created_ad_user.credentialVaultId-Mapping; der Handler entschluesselt das Initial-' ||
                  'Passwort zur Run-time. Output liefert messageId (kann NULL sein) + sentTo + sentAtUtc. ' ||
                  'AlreadyExists ohne Vault-Eintrag fuehrt zu sauberem Permanent-Failure.',
    updated_at = NOW()
WHERE action_key = 'SendWelcomeMailGraph';
