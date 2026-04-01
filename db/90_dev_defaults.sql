-- Lokale Entwicklungs-Defaults ohne Demo-Benutzer oder kuenstliche Rollen-/Gruppenwelten.

INSERT INTO notification_email_settings (
    id,
    enabled,
    frontend_base_url,
    last_test_status
)
VALUES
    (1, FALSE, 'http://localhost:5173', 'never')
ON CONFLICT (id) DO NOTHING;
