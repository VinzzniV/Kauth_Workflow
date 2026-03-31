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
        string frontendBaseUrl,
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
    @frontendBaseUrl,
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
        command.Parameters.AddWithValue("frontendBaseUrl", frontendBaseUrl);
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
            SenderEmail = reader.IsDBNull(1) ? null : reader.GetString(1),
            FrontendBaseUrl = reader.GetString(2),
            TestRecipientEmail = reader.IsDBNull(3) ? null : reader.GetString(3),
            SandboxRedirectEmail = reader.IsDBNull(4) ? null : reader.GetString(4),
            NotifyOnWorkflowCreated = reader.GetBoolean(5),
            NotifyOnTaskReady = reader.GetBoolean(6),
            NotifyOnWorkflowCompleted = reader.GetBoolean(7),
            LastTestStatus = reader.GetString(8),
            LastTestAt = reader.IsDBNull(9) ? null : reader.GetDateTime(9),
            LastError = reader.IsDBNull(10) ? null : reader.GetString(10),
            UpdatedAt = reader.GetDateTime(11)
        };
    }

    private static void AddNullableText(NpgsqlCommand command, string parameterName, string? value)
    {
        var parameter = command.Parameters.Add(parameterName, NpgsqlDbType.Text);
        parameter.Value = (object?)value ?? DBNull.Value;
    }

    private static string GetConnectionString()
    {
        return LifecycleRuntimeSettingsResolver.GetRequiredConnectionString();
    }
}
