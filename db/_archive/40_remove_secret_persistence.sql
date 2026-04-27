DROP TABLE IF EXISTS graph_application_settings;

ALTER TABLE notification_email_settings
    DROP COLUMN IF EXISTS tenant_id,
    DROP COLUMN IF EXISTS client_id,
    DROP COLUMN IF EXISTS client_secret;
