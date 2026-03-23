using Npgsql;
using NpgsqlTypes;

namespace API;

internal sealed class PostgresNotificationEmailConfigurationRepository : INotificationEmailConfigurationRepository
{
    public async Task<StoredNotificationEmailSettings?> GetSettings(CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
SELECT
    enabled,
    tenant_id,
    client_id,
    client_secret,
    sender_email,
    frontend_base_url,
    test_recipient_email,
    sandbox_redirect_email,
    notify_on_workflow_created,
    notify_on_task_ready,
    notify_on_workflow_completed,
    last_test_status,
    last_test_at,
    last_error,
    updated_at
FROM notification_email_settings
WHERE id = 1
LIMIT 1;";

        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return Map(reader);
    }

    public async Task<StoredNotificationEmailSettings> UpsertSettings(
        NotificationEmailSettingsUpsertModel settings,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
INSERT INTO notification_email_settings (
    id,
    enabled,
    tenant_id,
    client_id,
    client_secret,
    sender_email,
    frontend_base_url,
    test_recipient_email,
    sandbox_redirect_email,
    notify_on_workflow_created,
    notify_on_task_ready,
    notify_on_workflow_completed,
    last_test_status,
    last_test_at,
    last_error,
    updated_at
)
VALUES (
    1,
    @enabled,
    @tenantId,
    @clientId,
    @clientSecret,
    @senderEmail,
    @frontendBaseUrl,
    @testRecipientEmail,
    @sandboxRedirectEmail,
    @notifyOnWorkflowCreated,
    @notifyOnTaskReady,
    @notifyOnWorkflowCompleted,
    'never',
    NULL,
    NULL,
    NOW()
)
ON CONFLICT (id) DO UPDATE
SET
    enabled = EXCLUDED.enabled,
    tenant_id = EXCLUDED.tenant_id,
    client_id = EXCLUDED.client_id,
    client_secret = EXCLUDED.client_secret,
    sender_email = EXCLUDED.sender_email,
    frontend_base_url = EXCLUDED.frontend_base_url,
    test_recipient_email = EXCLUDED.test_recipient_email,
    sandbox_redirect_email = EXCLUDED.sandbox_redirect_email,
    notify_on_workflow_created = EXCLUDED.notify_on_workflow_created,
    notify_on_task_ready = EXCLUDED.notify_on_task_ready,
    notify_on_workflow_completed = EXCLUDED.notify_on_workflow_completed,
    last_test_status = 'never',
    last_test_at = NULL,
    last_error = NULL,
    updated_at = NOW()
RETURNING
    enabled,
    tenant_id,
    client_id,
    client_secret,
    sender_email,
    frontend_base_url,
    test_recipient_email,
    sandbox_redirect_email,
    notify_on_workflow_created,
    notify_on_task_ready,
    notify_on_workflow_completed,
    last_test_status,
    last_test_at,
    last_error,
    updated_at;";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("enabled", settings.Enabled);
        AddNullableText(command, "tenantId", settings.TenantId);
        AddNullableText(command, "clientId", settings.ClientId);
        AddNullableText(command, "clientSecret", settings.ClientSecret);
        AddNullableText(command, "senderEmail", settings.SenderEmail);
        command.Parameters.AddWithValue("frontendBaseUrl", settings.FrontendBaseUrl);
        AddNullableText(command, "testRecipientEmail", settings.TestRecipientEmail);
        AddNullableText(command, "sandboxRedirectEmail", settings.SandboxRedirectEmail);
        command.Parameters.AddWithValue("notifyOnWorkflowCreated", settings.NotifyOnWorkflowCreated);
        command.Parameters.AddWithValue("notifyOnTaskReady", settings.NotifyOnTaskReady);
        command.Parameters.AddWithValue("notifyOnWorkflowCompleted", settings.NotifyOnWorkflowCompleted);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        return Map(reader);
    }

    public async Task<StoredNotificationEmailSettings> UpdateTestStatus(
        string lastTestStatus,
        string? lastError,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
INSERT INTO notification_email_settings (
    id,
    enabled,
    frontend_base_url,
    last_test_status,
    last_test_at,
    last_error,
    updated_at
)
VALUES (
    1,
    FALSE,
    'http://localhost:5173',
    @lastTestStatus,
    NOW(),
    @lastError,
    NOW()
)
ON CONFLICT (id) DO UPDATE
SET
    last_test_status = @lastTestStatus,
    last_test_at = NOW(),
    last_error = @lastError,
    updated_at = NOW()
RETURNING
    enabled,
    tenant_id,
    client_id,
    client_secret,
    sender_email,
    frontend_base_url,
    test_recipient_email,
    sandbox_redirect_email,
    notify_on_workflow_created,
    notify_on_task_ready,
    notify_on_workflow_completed,
    last_test_status,
    last_test_at,
    last_error,
    updated_at;";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("lastTestStatus", lastTestStatus);
        AddNullableText(command, "lastError", lastError);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        return Map(reader);
    }

    private static StoredNotificationEmailSettings Map(NpgsqlDataReader reader)
    {
        return new StoredNotificationEmailSettings
        {
            Enabled = reader.GetBoolean(0),
            TenantId = reader.IsDBNull(1) ? null : reader.GetString(1),
            ClientId = reader.IsDBNull(2) ? null : reader.GetString(2),
            ClientSecret = reader.IsDBNull(3) ? null : reader.GetString(3),
            SenderEmail = reader.IsDBNull(4) ? null : reader.GetString(4),
            FrontendBaseUrl = reader.GetString(5),
            TestRecipientEmail = reader.IsDBNull(6) ? null : reader.GetString(6),
            SandboxRedirectEmail = reader.IsDBNull(7) ? null : reader.GetString(7),
            NotifyOnWorkflowCreated = reader.GetBoolean(8),
            NotifyOnTaskReady = reader.GetBoolean(9),
            NotifyOnWorkflowCompleted = reader.GetBoolean(10),
            LastTestStatus = reader.GetString(11),
            LastTestAt = reader.IsDBNull(12) ? null : reader.GetDateTime(12),
            LastError = reader.IsDBNull(13) ? null : reader.GetString(13),
            UpdatedAt = reader.GetDateTime(14)
        };
    }

    private static void AddNullableText(NpgsqlCommand command, string parameterName, string? value)
    {
        var parameter = command.Parameters.Add(parameterName, NpgsqlDbType.Text);
        parameter.Value = (object?)value ?? DBNull.Value;
    }

    private static string GetConnectionString()
    {
        var connectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("CONNECTION_STRING is not configured.");
        }

        return connectionString;
    }
}
