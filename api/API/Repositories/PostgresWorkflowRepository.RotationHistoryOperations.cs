using System.Text.Json;
using Npgsql;

namespace API;

internal sealed partial class PostgresWorkflowRepository
{
    public async Task<List<RotationAuditEntryDto>> GetRotationAuditLog(long planId, int limit = 200, int offset = 0)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();

        const string sql = @"
SELECT
    ral.id,
    ral.rotation_plan_id,
    ral.rotation_station_id,
    ral.generated_task_id,
    ral.actor_user_id,
    u.display_name,
    ral.event_type,
    ral.old_value::text,
    ral.new_value::text,
    ral.detail,
    ral.created_at
FROM rotation_audit_log ral
LEFT JOIN app_users u ON u.id = ral.actor_user_id
WHERE ral.rotation_plan_id = @planId
ORDER BY ral.created_at DESC, ral.id DESC
LIMIT @limit
OFFSET @offset;";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("planId", planId);
        command.Parameters.AddWithValue("limit", limit);
        command.Parameters.AddWithValue("offset", offset);

        await using var reader = await command.ExecuteReaderAsync();
        var entries = new List<RotationAuditEntryDto>();
        while (await reader.ReadAsync())
        {
            entries.Add(new RotationAuditEntryDto
            {
                Id = reader.GetInt64(0),
                RotationPlanId = reader.GetInt64(1),
                RotationStationId = reader.IsDBNull(2) ? null : reader.GetInt64(2),
                GeneratedTaskId = reader.IsDBNull(3) ? null : reader.GetInt64(3),
                ActorUserId = reader.IsDBNull(4) ? null : reader.GetInt64(4),
                ActorUserName = reader.IsDBNull(5) ? null : reader.GetString(5),
                EventType = reader.GetString(6),
                OldValue = ReadJsonElement(reader, 7),
                NewValue = ReadJsonElement(reader, 8),
                Detail = reader.IsDBNull(9) ? null : reader.GetString(9),
                CreatedAt = reader.GetDateTime(10)
            });
        }

        return entries;
    }

    public async Task<List<RotationNotificationDto>> GetRotationNotifications(long planId, int limit = 200, int offset = 0)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();

        const string sql = @"
SELECT
    rn.id,
    rn.rotation_plan_id,
    rn.rotation_station_id,
    rn.generated_task_id,
    rn.notification_type,
    rn.recipient_email,
    COALESCE(rn.payload_json ->> 'RecipientName', u.display_name),
    rn.recipient_user_id,
    rn.subject,
    rn.payload_json::text,
    rn.status,
    rn.attempts,
    rn.last_error,
    rn.sent_at,
    rn.created_at
FROM rotation_notifications rn
LEFT JOIN app_users u ON u.id = rn.recipient_user_id
WHERE rn.rotation_plan_id = @planId
ORDER BY rn.created_at DESC, rn.id DESC
LIMIT @limit
OFFSET @offset;";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("planId", planId);
        command.Parameters.AddWithValue("limit", limit);
        command.Parameters.AddWithValue("offset", offset);

        await using var reader = await command.ExecuteReaderAsync();
        var notifications = new List<RotationNotificationDto>();
        while (await reader.ReadAsync())
        {
            notifications.Add(new RotationNotificationDto
            {
                Id = reader.GetInt64(0),
                RotationPlanId = reader.GetInt64(1),
                RotationStationId = reader.IsDBNull(2) ? null : reader.GetInt64(2),
                GeneratedTaskId = reader.IsDBNull(3) ? null : reader.GetInt64(3),
                NotificationType = reader.GetString(4),
                RecipientEmail = reader.GetString(5),
                RecipientName = reader.IsDBNull(6) ? null : reader.GetString(6),
                RecipientUserId = reader.IsDBNull(7) ? null : reader.GetInt64(7),
                Subject = reader.GetString(8),
                Payload = ReadJsonElement(reader, 9),
                Status = reader.GetString(10),
                Attempts = reader.GetInt32(11),
                LastError = reader.IsDBNull(12) ? null : reader.GetString(12),
                SentAt = reader.IsDBNull(13) ? null : reader.GetDateTime(13),
                CreatedAt = reader.GetDateTime(14)
            });
        }

        return notifications;
    }

    private static JsonElement? ReadJsonElement(NpgsqlDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal))
        {
            return null;
        }

        using var document = JsonDocument.Parse(reader.GetString(ordinal));
        return document.RootElement.Clone();
    }
}
