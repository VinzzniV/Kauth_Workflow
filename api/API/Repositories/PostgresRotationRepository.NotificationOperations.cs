using System.Text.Json;
using Npgsql;
using NpgsqlTypes;

namespace API;

internal sealed partial class PostgresRotationRepository
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

    public async Task<List<RotationNotificationDispatchTarget>> GetRotationNotificationPreviewTargets(
        long rotationPlanId,
        string notificationType,
        DateOnly asOfDate)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        var candidateRows = notificationType.Trim().ToLowerInvariant() switch
        {
            NotificationTemplateKeys.UpcomingChange => await LoadUpcomingChangeNotificationCandidateRows(connection, transaction, asOfDate),
            NotificationTemplateKeys.Reminder => await LoadReminderNotificationCandidateRows(connection, transaction, asOfDate),
            NotificationTemplateKeys.Overdue => await LoadOverdueNotificationCandidateRows(connection, transaction, asOfDate),
            _ => throw new InvalidOperationException($"Unsupported rotation notification type '{notificationType}'.")
        };

        var candidates = await BuildRotationNotificationCandidates(
            connection,
            transaction,
            candidateRows.Where(row => row.RotationPlanId == rotationPlanId).ToList(),
            asOfDate);

        var previewTargets = new List<RotationNotificationDispatchTarget>();
        foreach (var candidate in candidates)
        {
            if (await RotationNotificationExists(connection, transaction, candidate))
            {
                continue;
            }

            previewTargets.Add(new RotationNotificationDispatchTarget
            {
                NotificationId = 0,
                NotificationType = candidate.NotificationType,
                RotationPlanId = candidate.RotationPlanId,
                RotationStationId = candidate.RotationStationId,
                GeneratedTaskId = candidate.GeneratedTaskId,
                RecipientUserId = candidate.RecipientUserId,
                TargetName = candidate.RecipientName,
                TargetEmail = candidate.RecipientEmail,
                Subject = candidate.Subject,
                Payload = candidate.Payload
            });
        }

        await transaction.CommitAsync();
        return previewTargets;
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

    public async Task<List<RotationNotificationDispatchTarget>> GetDispatchableRotationNotifications(
        int? limit = null,
        IReadOnlyCollection<long>? excludeNotificationIds = null)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();

        var hasExclude = excludeNotificationIds is { Count: > 0 };
        var sql = @"
SELECT
    rn.id AS notification_id,
    rn.rotation_plan_id,
    rn.rotation_station_id,
    rn.generated_task_id,
    rn.notification_type,
    rn.recipient_email,
    rn.recipient_user_id,
    rn.subject,
    rn.payload_json::text AS payload_text
FROM rotation_notifications rn
JOIN rotation_plans rp ON rp.id = rn.rotation_plan_id
WHERE rn.status IN ('pending', 'failed')
  AND rp.status IN ('draft', 'active')";
        if (hasExclude)
        {
            sql += "\n  AND rn.id <> ALL(@excludeIds)";
        }
        sql += "\nORDER BY rn.created_at, rn.id";
        if (limit is int limitValue && limitValue > 0)
        {
            sql += $"\nLIMIT {limitValue}";
        }
        sql += ";";

        await using var command = new NpgsqlCommand(sql, connection);
        if (hasExclude)
        {
            command.Parameters.Add("excludeIds", NpgsqlDbType.Array | NpgsqlDbType.Bigint).Value = excludeNotificationIds!.ToArray();
        }
        await using var reader = await command.ExecuteReaderAsync();

        var targets = new List<RotationNotificationDispatchTarget>();
        while (await reader.ReadAsync())
        {
            var notificationId = reader.GetOrdinal("notification_id");
            var rotationPlanId = reader.GetOrdinal("rotation_plan_id");
            var rotationStationId = reader.GetOrdinal("rotation_station_id");
            var generatedTaskId = reader.GetOrdinal("generated_task_id");
            var notificationType = reader.GetOrdinal("notification_type");
            var recipientEmail = reader.GetOrdinal("recipient_email");
            var recipientUserId = reader.GetOrdinal("recipient_user_id");
            var subject = reader.GetOrdinal("subject");
            var payloadTextOrdinal = reader.GetOrdinal("payload_text");

            var payloadText = reader.IsDBNull(payloadTextOrdinal) ? null : reader.GetString(payloadTextOrdinal);
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
                NotificationId = reader.GetInt64(notificationId),
                RotationPlanId = reader.GetInt64(rotationPlanId),
                RotationStationId = reader.IsDBNull(rotationStationId) ? null : reader.GetInt64(rotationStationId),
                GeneratedTaskId = reader.IsDBNull(generatedTaskId) ? null : reader.GetInt64(generatedTaskId),
                NotificationType = reader.GetString(notificationType),
                TargetEmail = reader.GetString(recipientEmail),
                RecipientUserId = reader.IsDBNull(recipientUserId) ? null : reader.GetInt64(recipientUserId),
                TargetName = payload.RecipientName,
                Subject = reader.GetString(subject),
                Payload = payload
            });
        }

        return targets;
    }

    private sealed class RotationNotificationApplyMetadata
    {
        public required long RotationPlanId { get; init; }
        public long? RotationStationId { get; init; }
        public long? GeneratedTaskId { get; init; }
        public string? NotificationType { get; init; }
        public string? RecipientEmail { get; init; }
        public string? OldStatus { get; init; }
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

        var ids = results.Select(r => r.NotificationId).ToArray();

        const string selectSql = @"
SELECT
    id,
    rotation_plan_id,
    rotation_station_id,
    generated_task_id,
    notification_type,
    recipient_email,
    status AS old_status
FROM rotation_notifications
WHERE id = ANY(@ids);";

        var metadata = new Dictionary<long, RotationNotificationApplyMetadata>(ids.Length);
        await using (var selectCommand = new NpgsqlCommand(selectSql, connection, transaction))
        {
            selectCommand.Parameters.Add("ids", NpgsqlDbType.Array | NpgsqlDbType.Bigint).Value = ids;
            await using var reader = await selectCommand.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var idOrdinal = reader.GetOrdinal("id");
                var planOrdinal = reader.GetOrdinal("rotation_plan_id");
                var stationOrdinal = reader.GetOrdinal("rotation_station_id");
                var taskOrdinal = reader.GetOrdinal("generated_task_id");
                var typeOrdinal = reader.GetOrdinal("notification_type");
                var emailOrdinal = reader.GetOrdinal("recipient_email");
                var oldStatusOrdinal = reader.GetOrdinal("old_status");

                metadata[reader.GetInt64(idOrdinal)] = new RotationNotificationApplyMetadata
                {
                    RotationPlanId = reader.GetInt64(planOrdinal),
                    RotationStationId = reader.IsDBNull(stationOrdinal) ? null : reader.GetInt64(stationOrdinal),
                    GeneratedTaskId = reader.IsDBNull(taskOrdinal) ? null : reader.GetInt64(taskOrdinal),
                    NotificationType = reader.IsDBNull(typeOrdinal) ? null : reader.GetString(typeOrdinal),
                    RecipientEmail = reader.IsDBNull(emailOrdinal) ? null : reader.GetString(emailOrdinal),
                    OldStatus = reader.IsDBNull(oldStatusOrdinal) ? null : reader.GetString(oldStatusOrdinal)
                };
            }
        }

        const string updateSql = @"
UPDATE rotation_notifications rn
SET
    status = data.status,
    attempts = rn.attempts + CASE WHEN data.attempted THEN 1 ELSE 0 END,
    sent_at = CASE WHEN data.status = 'sent' THEN NOW() ELSE rn.sent_at END,
    last_error = data.last_error
FROM unnest(@ids::bigint[], @statuses::text[], @attempted::bool[], @errors::text[])
    AS data(id, status, attempted, last_error)
WHERE rn.id = data.id;";

        await using (var updateCommand = new NpgsqlCommand(updateSql, connection, transaction))
        {
            updateCommand.Parameters.Add("ids", NpgsqlDbType.Array | NpgsqlDbType.Bigint).Value = ids;
            updateCommand.Parameters.Add("statuses", NpgsqlDbType.Array | NpgsqlDbType.Text).Value = results.Select(r => r.Status).ToArray();
            updateCommand.Parameters.Add("attempted", NpgsqlDbType.Array | NpgsqlDbType.Boolean).Value = results.Select(r => r.Attempted).ToArray();
            updateCommand.Parameters.Add("errors", NpgsqlDbType.Array | NpgsqlDbType.Text).Value = results.Select(r => r.ErrorMessage).ToArray();
            await updateCommand.ExecuteNonQueryAsync();
        }

        foreach (var result in results)
        {
            if (!metadata.TryGetValue(result.NotificationId, out var meta))
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
                meta.RotationPlanId,
                meta.RotationStationId,
                meta.GeneratedTaskId,
                null,
                eventType,
                meta.OldStatus is null ? null : new { status = meta.OldStatus },
                new
                {
                    notificationId = result.NotificationId,
                    notificationType = meta.NotificationType,
                    recipientEmail = meta.RecipientEmail,
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
            var directRecipient = await PostgresRepositorySharedHelpers.LoadActiveUserNotificationRecipient(connection, transaction, row.AssigneeUserId.Value);
            if (directRecipient.HasValue)
            {
                return (directRecipient.Value.UserId, directRecipient.Value.DisplayName, directRecipient.Value.Email);
            }
        }

        if (!row.AssigneeResponsibilityId.HasValue)
        {
            return null;
        }

        var responsibilityUserId = await PostgresRepositorySharedHelpers.ResolvePrimaryAssigneeUserId(
            connection,
            transaction,
            row.AssigneeResponsibilityId.Value,
            row.DepartmentId);
        if (!responsibilityUserId.HasValue)
        {
            return null;
        }

        var resolvedRecipient = await PostgresRepositorySharedHelpers.LoadActiveUserNotificationRecipient(connection, transaction, responsibilityUserId.Value);
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
    rgt.id AS generated_task_id,
    rp.person_id,
    w.uid AS source_workflow_uid,
    rp.title AS plan_title,
    COALESCE(
        NULLIF(BTRIM(CONCAT_WS(' ', COALESCE(p.first_name, w.first_name), COALESCE(p.last_name, w.last_name))), ''),
        u.display_name,
        'Person #' || rp.person_id::text
    ) AS display_name,
    rgt.department_id,
    station_d.name AS department_name,
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
    rgt.title AS task_title,
    rgt.status AS task_status,
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
    rgt.id AS generated_task_id,
    rp.person_id,
    w.uid AS source_workflow_uid,
    rp.title AS plan_title,
    COALESCE(
        NULLIF(BTRIM(CONCAT_WS(' ', COALESCE(p.first_name, w.first_name), COALESCE(p.last_name, w.last_name))), ''),
        u.display_name,
        'Person #' || rp.person_id::text
    ) AS display_name,
    rgt.department_id,
    station_d.name AS department_name,
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
    rgt.title AS task_title,
    rgt.status AS task_status,
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
    rgt.id AS generated_task_id,
    rp.person_id,
    w.uid AS source_workflow_uid,
    rp.title AS plan_title,
    COALESCE(
        NULLIF(BTRIM(CONCAT_WS(' ', COALESCE(p.first_name, w.first_name), COALESCE(p.last_name, w.last_name))), ''),
        u.display_name,
        'Person #' || rp.person_id::text
    ) AS display_name,
    rgt.department_id,
    station_d.name AS department_name,
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
    rgt.title AS task_title,
    rgt.status AS task_status,
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
            var rotationPlanId = reader.GetOrdinal("rotation_plan_id");
            var rotationStationId = reader.GetOrdinal("rotation_station_id");
            var generatedTaskId = reader.GetOrdinal("generated_task_id");
            var personId = reader.GetOrdinal("person_id");
            var sourceWorkflowUid = reader.GetOrdinal("source_workflow_uid");
            var planTitle = reader.GetOrdinal("plan_title");
            var displayName = reader.GetOrdinal("display_name");
            var departmentId = reader.GetOrdinal("department_id");
            var departmentName = reader.GetOrdinal("department_name");
            var triggerType = reader.GetOrdinal("trigger_type");
            var anchorDate = reader.GetOrdinal("anchor_date");
            var currentDepartmentName = reader.GetOrdinal("current_department_name");
            var nextDepartmentName = reader.GetOrdinal("next_department_name");
            var taskTitle = reader.GetOrdinal("task_title");
            var taskStatus = reader.GetOrdinal("task_status");
            var dueDate = reader.GetOrdinal("due_date");
            var assigneeUserId = reader.GetOrdinal("assignee_user_id");
            var assigneeResponsibilityId = reader.GetOrdinal("assignee_responsibility_id");

            rows.Add(new RotationNotificationCandidateRow
            {
                NotificationType = notificationType,
                RotationPlanId = reader.GetInt64(rotationPlanId),
                RotationStationId = reader.IsDBNull(rotationStationId) ? null : reader.GetInt64(rotationStationId),
                GeneratedTaskId = reader.GetInt64(generatedTaskId),
                PersonId = reader.GetInt64(personId),
                SourceWorkflowUid = reader.GetGuid(sourceWorkflowUid),
                PlanTitle = reader.GetString(planTitle),
                PersonDisplayName = reader.GetString(displayName),
                DepartmentId = reader.GetInt32(departmentId),
                DepartmentName = reader.IsDBNull(departmentName) ? null : reader.GetString(departmentName),
                TriggerType = reader.IsDBNull(triggerType) ? null : reader.GetString(triggerType),
                ChangeDate = reader.IsDBNull(anchorDate) ? null : reader.GetFieldValue<DateOnly>(anchorDate),
                CurrentDepartmentName = reader.IsDBNull(currentDepartmentName) ? null : reader.GetString(currentDepartmentName),
                NextDepartmentName = reader.IsDBNull(nextDepartmentName) ? null : reader.GetString(nextDepartmentName),
                TaskTitle = reader.GetString(taskTitle),
                TaskStatus = reader.GetString(taskStatus),
                DueDate = reader.IsDBNull(dueDate) ? null : reader.GetFieldValue<DateOnly>(dueDate),
                AssigneeUserId = reader.IsDBNull(assigneeUserId) ? null : reader.GetInt64(assigneeUserId),
                AssigneeResponsibilityId = reader.IsDBNull(assigneeResponsibilityId) ? null : reader.GetInt32(assigneeResponsibilityId)
            });
        }

        return rows;
    }
}
