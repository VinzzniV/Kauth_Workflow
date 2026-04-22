using Npgsql;

namespace API;

internal sealed class PostgresNotificationTemplateRepository : INotificationTemplateRepository
{
    public async Task<List<StoredNotificationTemplate>> GetTemplates(CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync(cancellationToken);

        const string sql = """
SELECT
    template_key,
    display_name,
    trigger_description,
    subject_template,
    body_template,
    is_system_locked,
    updated_at
FROM notification_templates
ORDER BY template_key;
""";

        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var templates = new List<StoredNotificationTemplate>();
        while (await reader.ReadAsync(cancellationToken))
        {
            templates.Add(Map(reader));
        }

        return templates;
    }

    public async Task<StoredNotificationTemplate?> GetTemplate(string templateKey, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync(cancellationToken);

        const string sql = """
SELECT
    template_key,
    display_name,
    trigger_description,
    subject_template,
    body_template,
    is_system_locked,
    updated_at
FROM notification_templates
WHERE template_key = @templateKey
LIMIT 1;
""";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("templateKey", templateKey.Trim().ToLowerInvariant());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? Map(reader)
            : null;
    }

    public async Task<StoredNotificationTemplate> UpsertTemplate(
        NotificationTemplateUpsertModel model,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync(cancellationToken);

        const string sql = """
INSERT INTO notification_templates (
    template_key,
    display_name,
    trigger_description,
    subject_template,
    body_template,
    is_system_locked,
    updated_at
)
VALUES (
    @templateKey,
    @displayName,
    @triggerDescription,
    @subjectTemplate,
    @bodyTemplate,
    @isSystemLocked,
    NOW()
)
ON CONFLICT (template_key) DO UPDATE
SET
    display_name = EXCLUDED.display_name,
    trigger_description = EXCLUDED.trigger_description,
    subject_template = EXCLUDED.subject_template,
    body_template = EXCLUDED.body_template,
    is_system_locked = EXCLUDED.is_system_locked,
    updated_at = NOW()
RETURNING
    template_key,
    display_name,
    trigger_description,
    subject_template,
    body_template,
    is_system_locked,
    updated_at;
""";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("templateKey", model.TemplateKey.Trim().ToLowerInvariant());
        command.Parameters.AddWithValue("displayName", model.DisplayName.Trim());
        command.Parameters.AddWithValue("triggerDescription", model.TriggerDescription.Trim());
        command.Parameters.AddWithValue("subjectTemplate", model.SubjectTemplate);
        command.Parameters.AddWithValue("bodyTemplate", model.BodyTemplate);
        command.Parameters.AddWithValue("isSystemLocked", model.IsSystemLocked);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        return Map(reader);
    }

    private static StoredNotificationTemplate Map(NpgsqlDataReader reader)
    {
        return new StoredNotificationTemplate
        {
            TemplateKey = reader.GetString(0),
            DisplayName = reader.GetString(1),
            TriggerDescription = reader.GetString(2),
            SubjectTemplate = reader.GetString(3),
            BodyTemplate = reader.GetString(4),
            IsSystemLocked = reader.GetBoolean(5),
            UpdatedAt = reader.IsDBNull(6) ? null : reader.GetDateTime(6)
        };
    }

    private static string GetConnectionString()
    {
        return LifecycleRuntimeSettingsResolver.GetRequiredConnectionString();
    }
}
