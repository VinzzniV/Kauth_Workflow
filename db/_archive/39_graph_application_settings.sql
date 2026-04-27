CREATE TABLE IF NOT EXISTS graph_application_settings (
    id INT PRIMARY KEY,
    tenant_id TEXT NULL,
    client_id TEXT NULL,
    client_secret TEXT NULL,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'notification_email_settings'
          AND column_name = 'tenant_id'
    ) THEN
        EXECUTE $sql$
            INSERT INTO graph_application_settings (
                id,
                tenant_id,
                client_id,
                client_secret,
                updated_at
            )
            SELECT
                1,
                nes.tenant_id,
                nes.client_id,
                nes.client_secret,
                COALESCE(nes.updated_at, NOW())
            FROM notification_email_settings nes
            WHERE nes.id = 1
              AND (
                  nes.tenant_id IS NOT NULL
                  OR nes.client_id IS NOT NULL
                  OR nes.client_secret IS NOT NULL
              )
            ON CONFLICT (id) DO NOTHING
        $sql$;
    END IF;
END $$;
