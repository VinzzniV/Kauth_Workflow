using System.Text.Json;
using Npgsql;
using NpgsqlTypes;

namespace API;

internal sealed partial class PostgresWorkflowRepository
{
    private sealed class RotationNotificationCandidateRow
    {
        public required string NotificationType { get; init; }
        public required long RotationPlanId { get; init; }
        public long? RotationStationId { get; init; }
        public required long GeneratedTaskId { get; init; }
        public required long PersonId { get; init; }
        public required Guid SourceWorkflowUid { get; init; }
        public required string PlanTitle { get; init; }
        public required string PersonDisplayName { get; init; }
        public required int DepartmentId { get; init; }
        public string? DepartmentName { get; init; }
        public string? TriggerType { get; init; }
        public DateOnly? ChangeDate { get; init; }
        public string? CurrentDepartmentName { get; init; }
        public string? NextDepartmentName { get; init; }
        public required string TaskTitle { get; init; }
        public required string TaskStatus { get; init; }
        public DateOnly? DueDate { get; init; }
        public long? AssigneeUserId { get; init; }
        public int? AssigneeResponsibilityId { get; init; }
    }

    private sealed class RotationNotificationCandidate
    {
        public required string NotificationType { get; init; }
        public required long RotationPlanId { get; init; }
        public long? RotationStationId { get; init; }
        public required long RecipientUserId { get; init; }
        public required string RecipientName { get; init; }
        public required string RecipientEmail { get; init; }
        public required string Subject { get; init; }
        public required RotationNotificationPayload Payload { get; init; }
        public required long GeneratedTaskId { get; init; }
    }

    public async Task<int> CreateDueRotationNotifications(DateOnly asOfDate)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        var candidateRows = new List<RotationNotificationCandidateRow>();
        candidateRows.AddRange(await LoadUpcomingChangeNotificationCandidateRows(connection, transaction, asOfDate));
        candidateRows.AddRange(await LoadReminderNotificationCandidateRows(connection, transaction, asOfDate));
        candidateRows.AddRange(await LoadOverdueNotificationCandidateRows(connection, transaction, asOfDate));

        var candidates = await BuildRotationNotificationCandidates(connection, transaction, candidateRows, asOfDate);
        var created = 0;

        foreach (var candidate in candidates)
        {
            if (await RotationNotificationExists(connection, transaction, candidate))
            {
                continue;
            }

            await InsertRotationNotification(connection, transaction, candidate);
            created++;
        }

        await transaction.CommitAsync();
        return created;
    }

    public async Task<List<RotationNotificationDispatchTarget>> GetDispatchableRotationNotifications()
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
    rn.recipient_user_id,
    rn.subject,
    rn.payload_json::text
FROM rotation_notifications rn
JOIN rotation_plans rp ON rp.id = rn.rotation_plan_id
WHERE rn.status IN ('pending', 'failed')
  AND rp.status IN ('draft', 'active')
ORDER BY rn.created_at, rn.id;";

        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();

        var targets = new List<RotationNotificationDispatchTarget>();
        while (await reader.ReadAsync())
        {
            var payloadText = reader.IsDBNull(8) ? null : reader.GetString(8);
            if (string.IsNullOrWhiteSpace(payloadText))
            {
                continue;
            }

            var payload = JsonSerializer.Deserialize<RotationNotificationPayload>(payloadText);
            if (payload is null)
            {
                continue;
            }

            targets.Add(new RotationNotificationDispatchTarget
            {
                NotificationId = reader.GetInt64(0),
                RotationPlanId = reader.GetInt64(1),
                RotationStationId = reader.IsDBNull(2) ? null : reader.GetInt64(2),
                GeneratedTaskId = reader.IsDBNull(3) ? null : reader.GetInt64(3),
                NotificationType = reader.GetString(4),
                TargetEmail = reader.GetString(5),
                RecipientUserId = reader.IsDBNull(6) ? null : reader.GetInt64(6),
                TargetName = payload.RecipientName,
                Subject = reader.GetString(7),
                Payload = payload
            });
        }

        return targets;
    }

    public async Task ApplyRotationNotificationDispatchResults(IReadOnlyList<NotificationDispatchResult> results)
    {
        if (results.Count == 0)
        {
            return;
        }

        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        const string selectSql = @"
SELECT
    rotation_plan_id,
    rotation_station_id,
    generated_task_id,
    notification_type,
    recipient_email,
    status
FROM rotation_notifications
WHERE id = @notificationId
LIMIT 1;";

        const string updateSql = @"
UPDATE rotation_notifications
SET
    status = @status,
    attempts = attempts + CASE WHEN @attempted THEN 1 ELSE 0 END,
    sent_at = CASE WHEN @status = 'sent' THEN NOW() ELSE sent_at END,
    last_error = @lastError
WHERE id = @notificationId;";

        foreach (var result in results)
        {
            long? rotationPlanId = null;
            long? rotationStationId = null;
            long? generatedTaskId = null;
            string? notificationType = null;
            string? recipientEmail = null;
            string? oldStatus = null;

            await using (var selectCommand = new NpgsqlCommand(selectSql, connection, transaction))
            {
                selectCommand.Parameters.AddWithValue("notificationId", result.NotificationId);
                await using var reader = await selectCommand.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    rotationPlanId = reader.GetInt64(0);
                    rotationStationId = reader.IsDBNull(1) ? null : reader.GetInt64(1);
                    generatedTaskId = reader.IsDBNull(2) ? null : reader.GetInt64(2);
                    notificationType = reader.IsDBNull(3) ? null : reader.GetString(3);
                    recipientEmail = reader.IsDBNull(4) ? null : reader.GetString(4);
                    oldStatus = reader.IsDBNull(5) ? null : reader.GetString(5);
                }
            }

            await using var command = new NpgsqlCommand(updateSql, connection, transaction);
            command.Parameters.AddWithValue("status", result.Status);
            command.Parameters.AddWithValue("attempted", result.Attempted);
            command.Parameters.Add("lastError", NpgsqlDbType.Text).Value = (object?)result.ErrorMessage ?? DBNull.Value;
            command.Parameters.AddWithValue("notificationId", result.NotificationId);
            await command.ExecuteNonQueryAsync();

            if (!rotationPlanId.HasValue)
            {
                continue;
            }

            var eventType = result.Status switch
            {
                "sent" => "rotation_notification_sent",
                "failed" => "rotation_notification_failed",
                "disabled" => "rotation_notification_disabled",
                _ => null
            };
            if (eventType is null)
            {
                continue;
            }

            await InsertRotationAuditEntry(
                connection,
                transaction,
                rotationPlanId.Value,
                rotationStationId,
                generatedTaskId,
                null,
                eventType,
                oldStatus is null ? null : new { status = oldStatus },
                new
                {
                    notificationId = result.NotificationId,
                    notificationType,
                    recipientEmail,
                    status = result.Status
                },
                result.ErrorMessage);
        }

        await transaction.CommitAsync();
    }

    private static async Task<List<RotationNotificationCandidate>> BuildRotationNotificationCandidates(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        IReadOnlyList<RotationNotificationCandidateRow> rows,
        DateOnly asOfDate)
    {
        var groupedRows = new Dictionary<string, List<(RotationNotificationCandidateRow Row, long RecipientUserId, string RecipientName, string RecipientEmail)>>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            var recipient = await ResolveRotationNotificationRecipient(connection, transaction, row);
            if (recipient is null)
            {
                continue;
            }

            var groupKey = BuildRotationNotificationGroupKey(row, recipient.Value.Email, asOfDate);
            if (!groupedRows.TryGetValue(groupKey, out var group))
            {
                group = [];
                groupedRows[groupKey] = group;
            }

            group.Add((row, recipient.Value.UserId, recipient.Value.DisplayName, recipient.Value.Email));
        }

        return groupedRows
            .OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase)
            .Select(entry => BuildRotationNotificationCandidate(entry.Key, entry.Value))
            .ToList();
    }

    private static RotationNotificationCandidate BuildRotationNotificationCandidate(
        string dedupeKey,
        IReadOnlyList<(RotationNotificationCandidateRow Row, long RecipientUserId, string RecipientName, string RecipientEmail)> rows)
    {
        var first = rows[0];
        var tasks = rows
            .GroupBy(item => item.Row.GeneratedTaskId)
            .Select(group => group.First())
            .OrderBy(item => item.Row.DueDate)
            .ThenBy(item => item.Row.TaskTitle, StringComparer.CurrentCultureIgnoreCase)
            .Select(item => new RotationNotificationTaskMailItem
            {
                GeneratedTaskId = item.Row.GeneratedTaskId,
                TaskRef = RotationTaskRef.Build(item.Row.GeneratedTaskId),
                Title = item.Row.TaskTitle,
                Status = item.Row.TaskStatus,
                DueDate = item.Row.DueDate,
                DepartmentName = item.Row.DepartmentName
            })
            .ToList();

        var payload = new RotationNotificationPayload
        {
            DedupeKey = dedupeKey,
            RecipientName = first.RecipientName,
            PlanTitle = first.Row.PlanTitle,
            SourceWorkflowUid = first.Row.SourceWorkflowUid,
            PersonId = first.Row.PersonId,
            PersonDisplayName = first.Row.PersonDisplayName,
            CurrentDepartmentName = first.Row.CurrentDepartmentName,
            NextDepartmentName = first.Row.NextDepartmentName,
            ChangeDate = first.Row.ChangeDate,
            LinkPath = $"/tasks/my?taskRef={Uri.EscapeDataString(tasks[0].TaskRef)}",
            Tasks = tasks
        };

        return new RotationNotificationCandidate
        {
            NotificationType = first.Row.NotificationType,
            RotationPlanId = first.Row.RotationPlanId,
            RotationStationId = first.Row.RotationStationId,
            RecipientUserId = first.RecipientUserId,
            RecipientName = first.RecipientName,
            RecipientEmail = first.RecipientEmail,
            Subject = BuildRotationNotificationSubject(first.Row.NotificationType, payload),
            Payload = payload,
            GeneratedTaskId = tasks[0].GeneratedTaskId
        };
    }

    private static async Task<(long UserId, string DisplayName, string Email)?> ResolveRotationNotificationRecipient(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        RotationNotificationCandidateRow row)
    {
        if (row.AssigneeUserId.HasValue)
        {
            var directRecipient = await LoadActiveUserNotificationRecipient(connection, transaction, row.AssigneeUserId.Value);
            if (directRecipient.HasValue)
            {
                return (directRecipient.Value.UserId, directRecipient.Value.DisplayName, directRecipient.Value.Email);
            }
        }

        if (!row.AssigneeResponsibilityId.HasValue)
        {
            return null;
        }

        var responsibilityUserId = await ResolvePrimaryAssigneeUserId(
            connection,
            transaction,
            row.AssigneeResponsibilityId.Value,
            row.DepartmentId);
        if (!responsibilityUserId.HasValue)
        {
            return null;
        }

        var resolvedRecipient = await LoadActiveUserNotificationRecipient(connection, transaction, responsibilityUserId.Value);
        return resolvedRecipient.HasValue
            ? (resolvedRecipient.Value.UserId, resolvedRecipient.Value.DisplayName, resolvedRecipient.Value.Email)
            : null;
    }

    private static string BuildRotationNotificationGroupKey(
        RotationNotificationCandidateRow row,
        string recipientEmail,
        DateOnly asOfDate)
    {
        var stationKey = row.RotationStationId?.ToString() ?? $"task-{row.GeneratedTaskId}";
        var normalizedEmail = recipientEmail.Trim().ToLowerInvariant();
        return row.NotificationType switch
        {
            "upcoming_change" => $"{row.NotificationType}|{row.RotationPlanId}|{stationKey}|{normalizedEmail}|{row.TriggerType}|{row.ChangeDate:yyyy-MM-dd}",
            "reminder" => $"{row.NotificationType}|{row.RotationPlanId}|{stationKey}|{normalizedEmail}|{row.DueDate:yyyy-MM-dd}",
            _ => $"{row.NotificationType}|{row.RotationPlanId}|{stationKey}|{normalizedEmail}|{asOfDate:yyyy-MM-dd}"
        };
    }

    private static string BuildRotationNotificationSubject(string notificationType, RotationNotificationPayload payload)
    {
        return notificationType switch
        {
            "overdue" => $"Ueberfaellige Rotationsaufgaben fuer {payload.PersonDisplayName}",
            "reminder" => $"Faellige Rotationsaufgaben fuer {payload.PersonDisplayName}",
            _ when !string.IsNullOrWhiteSpace(payload.NextDepartmentName)
                => $"Bevorstehender Wechsel: {payload.PersonDisplayName} -> {payload.NextDepartmentName}",
            _ => $"Bevorstehender Wechsel: {payload.PersonDisplayName}"
        };
    }

    private static async Task<bool> RotationNotificationExists(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        RotationNotificationCandidate candidate)
    {
        const string sql = @"
SELECT 1
FROM rotation_notifications
WHERE rotation_plan_id = @rotationPlanId
  AND ((@rotationStationId IS NULL AND rotation_station_id IS NULL) OR rotation_station_id = @rotationStationId)
  AND recipient_email = @recipientEmail
  AND notification_type = @notificationType
  AND status IN ('pending', 'sent')
  AND payload_json ->> 'DedupeKey' = @dedupeKey
LIMIT 1;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("rotationPlanId", candidate.RotationPlanId);
        command.Parameters.Add("rotationStationId", NpgsqlDbType.Bigint).Value = (object?)candidate.RotationStationId ?? DBNull.Value;
        command.Parameters.AddWithValue("recipientEmail", candidate.RecipientEmail);
        command.Parameters.AddWithValue("notificationType", candidate.NotificationType);
        command.Parameters.AddWithValue("dedupeKey", candidate.Payload.DedupeKey);
        return await command.ExecuteScalarAsync() is not null;
    }

    private static async Task InsertRotationNotification(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        RotationNotificationCandidate candidate)
    {
        const string sql = @"
INSERT INTO rotation_notifications (
    rotation_plan_id,
    rotation_station_id,
    generated_task_id,
    notification_type,
    recipient_email,
    recipient_user_id,
    subject,
    payload_json,
    status
)
VALUES (
    @rotationPlanId,
    @rotationStationId,
    @generatedTaskId,
    @notificationType,
    @recipientEmail,
    @recipientUserId,
    @subject,
    @payloadJson,
    'pending'
);";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("rotationPlanId", candidate.RotationPlanId);
        command.Parameters.Add("rotationStationId", NpgsqlDbType.Bigint).Value = (object?)candidate.RotationStationId ?? DBNull.Value;
        command.Parameters.AddWithValue("generatedTaskId", candidate.GeneratedTaskId);
        command.Parameters.AddWithValue("notificationType", candidate.NotificationType);
        command.Parameters.AddWithValue("recipientEmail", candidate.RecipientEmail);
        command.Parameters.AddWithValue("recipientUserId", candidate.RecipientUserId);
        command.Parameters.AddWithValue("subject", candidate.Subject);
        command.Parameters.Add("payloadJson", NpgsqlDbType.Jsonb).Value = JsonSerializer.Serialize(candidate.Payload);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<List<RotationNotificationCandidateRow>> LoadUpcomingChangeNotificationCandidateRows(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        DateOnly asOfDate)
    {
        const string sql = @"
SELECT
    rgt.rotation_plan_id,
    rgt.rotation_station_id,
    rgt.id,
    rp.person_id,
    w.uid,
    rp.title,
    COALESCE(
        NULLIF(BTRIM(CONCAT_WS(' ', COALESCE(p.first_name, w.first_name), COALESCE(p.last_name, w.last_name))), ''),
        u.display_name,
        'Person #' || rp.person_id::text
    ) AS display_name,
    rgt.department_id,
    station_d.name,
    rgt.trigger_type,
    rgt.anchor_date,
    CASE
        WHEN rgt.trigger_type = 'exit' THEN station_d.name
        ELSE COALESCE(prev_d.name, plan_d.name, station_d.name)
    END AS current_department_name,
    CASE
        WHEN rgt.trigger_type = 'exit' THEN COALESCE(next_d.name, plan_d.name, station_d.name)
        ELSE station_d.name
    END AS next_department_name,
    rgt.title,
    rgt.status,
    rgt.due_date,
    ta.assignee_user_id,
    ta.assignee_responsibility_id
FROM rotation_generated_tasks rgt
JOIN rotation_plans rp ON rp.id = rgt.rotation_plan_id
JOIN workflows w ON w.id = rp.source_workflow_id
LEFT JOIN people p ON p.id = rp.person_id
LEFT JOIN app_users u ON u.id = p.app_user_id
LEFT JOIN departments plan_d ON plan_d.id = COALESCE(w.department_id, p.department_id, u.department_id)
JOIN rotation_stations rs ON rs.id = rgt.rotation_station_id
JOIN departments station_d ON station_d.id = rs.department_id
LEFT JOIN rotation_stations prev_rs
    ON prev_rs.rotation_plan_id = rs.rotation_plan_id
   AND prev_rs.order_index = rs.order_index - 1
LEFT JOIN departments prev_d ON prev_d.id = prev_rs.department_id
LEFT JOIN rotation_stations next_rs
    ON next_rs.rotation_plan_id = rs.rotation_plan_id
   AND next_rs.order_index = rs.order_index + 1
LEFT JOIN departments next_d ON next_d.id = next_rs.department_id
LEFT JOIN department_action_templates dat ON dat.id = rgt.template_id
LEFT JOIN rotation_task_assignments ta
    ON ta.rotation_generated_task_id = rgt.id
   AND ta.is_primary = TRUE
WHERE rp.status IN ('draft', 'active')
  AND rgt.status IN ('open', 'in_progress')
  AND dat.reminder_offset_days IS NOT NULL
  AND (rgt.anchor_date - dat.reminder_offset_days) = @asOfDate
ORDER BY rgt.rotation_plan_id, rgt.rotation_station_id, rgt.id;";

        return await LoadRotationNotificationCandidateRows(connection, transaction, sql, asOfDate, "upcoming_change");
    }

    private static async Task<List<RotationNotificationCandidateRow>> LoadReminderNotificationCandidateRows(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        DateOnly asOfDate)
    {
        const string sql = @"
SELECT
    rgt.rotation_plan_id,
    rgt.rotation_station_id,
    rgt.id,
    rp.person_id,
    w.uid,
    rp.title,
    COALESCE(
        NULLIF(BTRIM(CONCAT_WS(' ', COALESCE(p.first_name, w.first_name), COALESCE(p.last_name, w.last_name))), ''),
        u.display_name,
        'Person #' || rp.person_id::text
    ) AS display_name,
    rgt.department_id,
    station_d.name,
    rgt.trigger_type,
    rgt.anchor_date,
    CASE
        WHEN rgt.trigger_type = 'exit' THEN station_d.name
        ELSE COALESCE(prev_d.name, plan_d.name, station_d.name)
    END AS current_department_name,
    CASE
        WHEN rgt.trigger_type = 'exit' THEN COALESCE(next_d.name, plan_d.name, station_d.name)
        ELSE station_d.name
    END AS next_department_name,
    rgt.title,
    rgt.status,
    rgt.due_date,
    ta.assignee_user_id,
    ta.assignee_responsibility_id
FROM rotation_generated_tasks rgt
JOIN rotation_plans rp ON rp.id = rgt.rotation_plan_id
JOIN workflows w ON w.id = rp.source_workflow_id
LEFT JOIN people p ON p.id = rp.person_id
LEFT JOIN app_users u ON u.id = p.app_user_id
LEFT JOIN departments plan_d ON plan_d.id = COALESCE(w.department_id, p.department_id, u.department_id)
LEFT JOIN rotation_stations rs ON rs.id = rgt.rotation_station_id
LEFT JOIN departments station_d ON station_d.id = rs.department_id
LEFT JOIN rotation_stations prev_rs
    ON prev_rs.rotation_plan_id = rs.rotation_plan_id
   AND prev_rs.order_index = rs.order_index - 1
LEFT JOIN departments prev_d ON prev_d.id = prev_rs.department_id
LEFT JOIN rotation_stations next_rs
    ON next_rs.rotation_plan_id = rs.rotation_plan_id
   AND next_rs.order_index = rs.order_index + 1
LEFT JOIN departments next_d ON next_d.id = next_rs.department_id
LEFT JOIN rotation_task_assignments ta
    ON ta.rotation_generated_task_id = rgt.id
   AND ta.is_primary = TRUE
WHERE rp.status IN ('draft', 'active')
  AND rgt.status IN ('open', 'in_progress')
  AND rgt.due_date = @asOfDate
ORDER BY rgt.rotation_plan_id, rgt.rotation_station_id, rgt.id;";

        return await LoadRotationNotificationCandidateRows(connection, transaction, sql, asOfDate, "reminder");
    }

    private static async Task<List<RotationNotificationCandidateRow>> LoadOverdueNotificationCandidateRows(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        DateOnly asOfDate)
    {
        const string sql = @"
SELECT
    rgt.rotation_plan_id,
    rgt.rotation_station_id,
    rgt.id,
    rp.person_id,
    w.uid,
    rp.title,
    COALESCE(
        NULLIF(BTRIM(CONCAT_WS(' ', COALESCE(p.first_name, w.first_name), COALESCE(p.last_name, w.last_name))), ''),
        u.display_name,
        'Person #' || rp.person_id::text
    ) AS display_name,
    rgt.department_id,
    station_d.name,
    rgt.trigger_type,
    rgt.anchor_date,
    CASE
        WHEN rgt.trigger_type = 'exit' THEN station_d.name
        ELSE COALESCE(prev_d.name, plan_d.name, station_d.name)
    END AS current_department_name,
    CASE
        WHEN rgt.trigger_type = 'exit' THEN COALESCE(next_d.name, plan_d.name, station_d.name)
        ELSE station_d.name
    END AS next_department_name,
    rgt.title,
    rgt.status,
    rgt.due_date,
    ta.assignee_user_id,
    ta.assignee_responsibility_id
FROM rotation_generated_tasks rgt
JOIN rotation_plans rp ON rp.id = rgt.rotation_plan_id
JOIN workflows w ON w.id = rp.source_workflow_id
LEFT JOIN people p ON p.id = rp.person_id
LEFT JOIN app_users u ON u.id = p.app_user_id
LEFT JOIN departments plan_d ON plan_d.id = COALESCE(w.department_id, p.department_id, u.department_id)
LEFT JOIN rotation_stations rs ON rs.id = rgt.rotation_station_id
LEFT JOIN departments station_d ON station_d.id = rs.department_id
LEFT JOIN rotation_stations prev_rs
    ON prev_rs.rotation_plan_id = rs.rotation_plan_id
   AND prev_rs.order_index = rs.order_index - 1
LEFT JOIN departments prev_d ON prev_d.id = prev_rs.department_id
LEFT JOIN rotation_stations next_rs
    ON next_rs.rotation_plan_id = rs.rotation_plan_id
   AND next_rs.order_index = rs.order_index + 1
LEFT JOIN departments next_d ON next_d.id = next_rs.department_id
LEFT JOIN rotation_task_assignments ta
    ON ta.rotation_generated_task_id = rgt.id
   AND ta.is_primary = TRUE
WHERE rp.status IN ('draft', 'active')
  AND rgt.status IN ('open', 'in_progress')
  AND rgt.due_date IS NOT NULL
  AND rgt.due_date < @asOfDate
ORDER BY rgt.rotation_plan_id, rgt.rotation_station_id, rgt.id;";

        return await LoadRotationNotificationCandidateRows(connection, transaction, sql, asOfDate, "overdue");
    }

    private static async Task<List<RotationNotificationCandidateRow>> LoadRotationNotificationCandidateRows(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string sql,
        DateOnly asOfDate,
        string notificationType)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("asOfDate", asOfDate);
        await using var reader = await command.ExecuteReaderAsync();

        var rows = new List<RotationNotificationCandidateRow>();
        while (await reader.ReadAsync())
        {
            rows.Add(new RotationNotificationCandidateRow
            {
                NotificationType = notificationType,
                RotationPlanId = reader.GetInt64(0),
                RotationStationId = reader.IsDBNull(1) ? null : reader.GetInt64(1),
                GeneratedTaskId = reader.GetInt64(2),
                PersonId = reader.GetInt64(3),
                SourceWorkflowUid = reader.GetGuid(4),
                PlanTitle = reader.GetString(5),
                PersonDisplayName = reader.GetString(6),
                DepartmentId = reader.GetInt32(7),
                DepartmentName = reader.IsDBNull(8) ? null : reader.GetString(8),
                TriggerType = reader.IsDBNull(9) ? null : reader.GetString(9),
                ChangeDate = reader.IsDBNull(10) ? null : reader.GetFieldValue<DateOnly>(10),
                CurrentDepartmentName = reader.IsDBNull(11) ? null : reader.GetString(11),
                NextDepartmentName = reader.IsDBNull(12) ? null : reader.GetString(12),
                TaskTitle = reader.GetString(13),
                TaskStatus = reader.GetString(14),
                DueDate = reader.IsDBNull(15) ? null : reader.GetFieldValue<DateOnly>(15),
                AssigneeUserId = reader.IsDBNull(16) ? null : reader.GetInt64(16),
                AssigneeResponsibilityId = reader.IsDBNull(17) ? null : reader.GetInt32(17)
            });
        }

        return rows;
    }
}
