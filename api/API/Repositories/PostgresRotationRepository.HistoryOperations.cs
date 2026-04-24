using System.Text.Json;
using Npgsql;

namespace API;

internal sealed partial class PostgresRotationRepository
{
    public async Task<List<RotationAuditEntryDto>> GetRotationAuditLog(long planId, int limit = 200, int offset = 0)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();

        const string sql = @"
SELECT
    ral.id AS audit_entry_id,
    ral.rotation_plan_id,
    ral.rotation_station_id,
    ral.generated_task_id,
    ral.actor_user_id,
    u.display_name AS actor_user_name,
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
            var auditEntryId = reader.GetOrdinal("audit_entry_id");
            var rotationPlanId = reader.GetOrdinal("rotation_plan_id");
            var rotationStationId = reader.GetOrdinal("rotation_station_id");
            var generatedTaskId = reader.GetOrdinal("generated_task_id");
            var actorUserId = reader.GetOrdinal("actor_user_id");
            var actorUserName = reader.GetOrdinal("actor_user_name");
            var eventType = reader.GetOrdinal("event_type");
            var oldValue = reader.GetOrdinal("old_value");
            var newValue = reader.GetOrdinal("new_value");
            var detail = reader.GetOrdinal("detail");
            var createdAt = reader.GetOrdinal("created_at");

            entries.Add(new RotationAuditEntryDto
            {
                Id = reader.GetInt64(auditEntryId),
                RotationPlanId = reader.GetInt64(rotationPlanId),
                RotationStationId = reader.IsDBNull(rotationStationId) ? null : reader.GetInt64(rotationStationId),
                GeneratedTaskId = reader.IsDBNull(generatedTaskId) ? null : reader.GetInt64(generatedTaskId),
                ActorUserId = reader.IsDBNull(actorUserId) ? null : reader.GetInt64(actorUserId),
                ActorUserName = reader.IsDBNull(actorUserName) ? null : reader.GetString(actorUserName),
                EventType = reader.GetString(eventType),
                OldValue = ReadJsonElement(reader, oldValue),
                NewValue = ReadJsonElement(reader, newValue),
                Detail = reader.IsDBNull(detail) ? null : reader.GetString(detail),
                CreatedAt = reader.GetDateTime(createdAt)
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
    rn.id AS notification_id,
    rn.rotation_plan_id,
    rn.rotation_station_id,
    rn.generated_task_id,
    rn.notification_type,
    rn.recipient_email,
    COALESCE(rn.payload_json ->> 'RecipientName', u.display_name) AS recipient_name,
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
            var notificationId = reader.GetOrdinal("notification_id");
            var rotationPlanId = reader.GetOrdinal("rotation_plan_id");
            var rotationStationId = reader.GetOrdinal("rotation_station_id");
            var generatedTaskId = reader.GetOrdinal("generated_task_id");
            var notificationType = reader.GetOrdinal("notification_type");
            var recipientEmail = reader.GetOrdinal("recipient_email");
            var recipientName = reader.GetOrdinal("recipient_name");
            var recipientUserId = reader.GetOrdinal("recipient_user_id");
            var subject = reader.GetOrdinal("subject");
            var payloadJson = reader.GetOrdinal("payload_json");
            var status = reader.GetOrdinal("status");
            var attempts = reader.GetOrdinal("attempts");
            var lastError = reader.GetOrdinal("last_error");
            var sentAt = reader.GetOrdinal("sent_at");
            var createdAt = reader.GetOrdinal("created_at");

            notifications.Add(new RotationNotificationDto
            {
                Id = reader.GetInt64(notificationId),
                RotationPlanId = reader.GetInt64(rotationPlanId),
                RotationStationId = reader.IsDBNull(rotationStationId) ? null : reader.GetInt64(rotationStationId),
                GeneratedTaskId = reader.IsDBNull(generatedTaskId) ? null : reader.GetInt64(generatedTaskId),
                NotificationType = reader.GetString(notificationType),
                RecipientEmail = reader.GetString(recipientEmail),
                RecipientName = reader.IsDBNull(recipientName) ? null : reader.GetString(recipientName),
                RecipientUserId = reader.IsDBNull(recipientUserId) ? null : reader.GetInt64(recipientUserId),
                Subject = reader.GetString(subject),
                Payload = ReadJsonElement(reader, payloadJson),
                Status = reader.GetString(status),
                Attempts = reader.GetInt32(attempts),
                LastError = reader.IsDBNull(lastError) ? null : reader.GetString(lastError),
                SentAt = reader.IsDBNull(sentAt) ? null : reader.GetDateTime(sentAt),
                CreatedAt = reader.GetDateTime(createdAt)
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
