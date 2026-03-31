CREATE TABLE IF NOT EXISTS graph_application_settings (
    id INT PRIMARY KEY,
    tenant_id TEXT NULL,
    client_id TEXT NULL,
    client_secret TEXT NULL,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

INSERT INTO graph_application_settings (
    id,
    tenant_id,
    client_id,
    client_secret,
    updated_at
)
SELECT
    1,
    tenant_id,
    client_id,
    client_secret,
    COALESCE(updated_at, NOW())
FROM notification_email_settings
WHERE id = 1
  AND (
      tenant_id IS NOT NULL
      OR client_id IS NOT NULL
      OR client_secret IS NOT NULL
  )
ON CONFLICT (id) DO NOTHING;
