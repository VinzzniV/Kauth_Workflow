using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;

namespace API;

internal sealed class SystemEventLogService(
    LifecycleRuntimeSettings runtimeSettings,
    ILogger<SystemEventLogService> logger) : ISystemEventLogService
{
    private static readonly HashSet<string> AllowedSources =
    [
        "frontend",
        "api",
        "system",
        "mail",
        "entra",
        "directory",
        "automation",
        "workflow",
        "task",
        "rotation",
        "admin"
    ];

    private static readonly HashSet<string> AllowedSeverities = ["info", "warning", "error"];

    private static readonly string[] RedactedKeyFragments =
    [
        "token",
        "secret",
        "authorization",
        "clientsecret",
        "accesskey",
        "apikey",
        "refresh",
        "password"
    ];

    private static readonly string[] BodyKeyFragments =
    [
        "htmlbody",
        "textbody",
        "mailbody",
        "messagebody"
    ];

    public async Task WriteAsync(SystemEventLogWriteModel model, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(runtimeSettings.ConnectionString))
        {
            return;
        }

        try
        {
            await using var connection = new NpgsqlConnection(runtimeSettings.ConnectionString);
            await connection.OpenAsync(cancellationToken);

            const string sql = """
INSERT INTO system_event_log (
    severity,
    source,
    category,
    event_key,
    message,
    user_message,
    actor_user_id,
    client_route,
    client_function,
    http_method,
    http_path,
    http_status,
    trace_identifier,
    workflow_uid,
    rotation_plan_id,
    task_ref,
    entity_type,
    entity_id,
    details_json
)
VALUES (
    @severity,
    @source,
    @category,
    @eventKey,
    @message,
    @userMessage,
    @actorUserId,
    @clientRoute,
    @clientFunction,
    @httpMethod,
    @httpPath,
    @httpStatus,
    @traceIdentifier,
    @workflowUid,
    @rotationPlanId,
    @taskRef,
    @entityType,
    @entityId,
    @detailsJson
);
""";

            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("severity", NormalizeSeverity(model.Severity));
            command.Parameters.AddWithValue("source", NormalizeSource(model.Source));
            command.Parameters.AddWithValue("category", NormalizeValue(model.Category, "general"));
            command.Parameters.AddWithValue("eventKey", NormalizeValue(model.EventKey, "event"));
            command.Parameters.AddWithValue("message", NormalizeMessage(model.Message));
            command.Parameters.AddWithValue("userMessage", (object?)NormalizeOptionalText(model.UserMessage) ?? DBNull.Value);
            command.Parameters.AddWithValue("actorUserId", (object?)model.ActorUserId ?? DBNull.Value);
            command.Parameters.AddWithValue("clientRoute", (object?)NormalizeOptionalText(model.ClientRoute) ?? DBNull.Value);
            command.Parameters.AddWithValue("clientFunction", (object?)NormalizeOptionalText(model.ClientFunction) ?? DBNull.Value);
            command.Parameters.AddWithValue("httpMethod", (object?)NormalizeOptionalText(model.HttpMethod) ?? DBNull.Value);
            command.Parameters.AddWithValue("httpPath", (object?)NormalizeOptionalText(model.HttpPath) ?? DBNull.Value);
            command.Parameters.AddWithValue("httpStatus", (object?)model.HttpStatus ?? DBNull.Value);
            command.Parameters.AddWithValue("traceIdentifier", (object?)NormalizeOptionalText(model.TraceIdentifier) ?? DBNull.Value);
            command.Parameters.AddWithValue("workflowUid", (object?)model.WorkflowUid ?? DBNull.Value);
            command.Parameters.AddWithValue("rotationPlanId", (object?)model.RotationPlanId ?? DBNull.Value);
            command.Parameters.AddWithValue("taskRef", (object?)NormalizeOptionalText(model.TaskRef) ?? DBNull.Value);
            command.Parameters.AddWithValue("entityType", (object?)NormalizeOptionalText(model.EntityType) ?? DBNull.Value);
            command.Parameters.AddWithValue("entityId", (object?)NormalizeOptionalText(model.EntityId) ?? DBNull.Value);
            command.Parameters.Add(
                new NpgsqlParameter("detailsJson", NpgsqlDbType.Jsonb)
                {
                    Value = (object?)BuildRedactedDetailsJson(model.Details) ?? DBNull.Value
                });
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedTable)
        {
            logger.LogError(ex,
                "system_event_log table does not exist. DB schema drift detected — apply the pending migration before system events can be recorded.");
        }
    }

    public async Task<CursorPageDto<AdminSystemLogEntryDto>> GetAdminLogsAsync(
        SystemEventLogQuery query,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(runtimeSettings.ConnectionString))
        {
            return new CursorPageDto<AdminSystemLogEntryDto> { Items = [], HasMore = false, NextCursor = null };
        }

        var normalizedQuery = NormalizeQuery(query);
        var cursor = DecodeCursor(normalizedQuery.Cursor);

        await using var connection = new NpgsqlConnection(runtimeSettings.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = """
SELECT
    log.id,
    log.created_at,
    log.severity,
    log.source,
    log.category,
    log.event_key,
    log.message,
    log.user_message,
    log.actor_user_id,
    actor.display_name,
    log.client_route,
    log.client_function,
    log.http_method,
    log.http_path,
    log.http_status,
    log.trace_identifier,
    log.workflow_uid,
    log.rotation_plan_id,
    log.task_ref,
    log.entity_type,
    log.entity_id,
    log.details_json::text
FROM system_event_log log
LEFT JOIN app_users actor ON actor.id = log.actor_user_id
WHERE (@severities IS NULL OR log.severity = ANY(@severities))
  AND (@source IS NULL OR log.source = @source)
  AND (@since IS NULL OR log.created_at >= @since)
  AND (@until IS NULL OR log.created_at <= @until)
  AND (@search IS NULL OR (
        log.message ILIKE @search
        OR COALESCE(log.user_message, '') ILIKE @search
        OR COALESCE(log.client_route, '') ILIKE @search
        OR COALESCE(log.client_function, '') ILIKE @search
        OR COALESCE(log.http_path, '') ILIKE @search
        OR COALESCE(log.event_key, '') ILIKE @search
        OR COALESCE(log.category, '') ILIKE @search
        OR COALESCE(log.task_ref, '') ILIKE @search
        OR COALESCE(actor.display_name, '') ILIKE @search
      ))
  AND (@actorUserId IS NULL OR log.actor_user_id = @actorUserId)
  AND (@workflowUid IS NULL OR log.workflow_uid = @workflowUid)
  AND (@rotationPlanId IS NULL OR log.rotation_plan_id = @rotationPlanId)
  AND (@taskRef IS NULL OR log.task_ref = @taskRef)
  AND (@cursorAt IS NULL OR log.created_at < @cursorAt
       OR (log.created_at = @cursorAt AND log.id < @cursorId))
ORDER BY log.created_at DESC, log.id DESC
LIMIT @fetchLimit;
""";

        await using var command = BuildFilterCommand(sql, normalizedQuery, connection);
        command.Parameters.Add(new NpgsqlParameter("cursorAt", NpgsqlDbType.TimestampTz)
        {
            Value = (object?)cursor?.CreatedAt ?? DBNull.Value
        });
        command.Parameters.Add(new NpgsqlParameter("cursorId", NpgsqlDbType.Bigint)
        {
            Value = (object?)cursor?.Id ?? DBNull.Value
        });
        command.Parameters.Add(new NpgsqlParameter("fetchLimit", NpgsqlDbType.Integer)
        {
            Value = normalizedQuery.Limit + 1
        });

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var entries = new List<AdminSystemLogEntryDto>();
        while (await reader.ReadAsync(cancellationToken))
        {
            entries.Add(new AdminSystemLogEntryDto
            {
                Id = reader.GetInt64(0),
                CreatedAt = reader.GetDateTime(1),
                Severity = reader.GetString(2),
                Source = reader.GetString(3),
                Category = reader.GetString(4),
                EventKey = reader.GetString(5),
                Message = reader.GetString(6),
                UserMessage = reader.IsDBNull(7) ? null : reader.GetString(7),
                ActorUserId = reader.IsDBNull(8) ? null : reader.GetInt64(8),
                ActorDisplayName = reader.IsDBNull(9) ? null : reader.GetString(9),
                ClientRoute = reader.IsDBNull(10) ? null : reader.GetString(10),
                ClientFunction = reader.IsDBNull(11) ? null : reader.GetString(11),
                HttpMethod = reader.IsDBNull(12) ? null : reader.GetString(12),
                HttpPath = reader.IsDBNull(13) ? null : reader.GetString(13),
                HttpStatus = reader.IsDBNull(14) ? null : reader.GetInt32(14),
                TraceIdentifier = reader.IsDBNull(15) ? null : reader.GetString(15),
                WorkflowUid = reader.IsDBNull(16) ? null : reader.GetGuid(16),
                RotationPlanId = reader.IsDBNull(17) ? null : reader.GetInt64(17),
                TaskRef = reader.IsDBNull(18) ? null : reader.GetString(18),
                EntityType = reader.IsDBNull(19) ? null : reader.GetString(19),
                EntityId = reader.IsDBNull(20) ? null : reader.GetString(20),
                Details = reader.IsDBNull(21) ? null : JsonSerializer.Deserialize<JsonElement>(reader.GetString(21))
            });
        }

        var hasMore = entries.Count > normalizedQuery.Limit;
        if (hasMore)
        {
            entries.RemoveAt(entries.Count - 1);
        }

        var nextCursor = hasMore && entries.Count > 0
            ? CursorPageQuery.EncodeCursor(entries[^1].CreatedAt, entries[^1].Id)
            : null;

        return new CursorPageDto<AdminSystemLogEntryDto>
        {
            Items = entries,
            HasMore = hasMore,
            NextCursor = nextCursor
        };
    }

    public async Task<AdminSystemLogSummaryDto> GetAdminLogSummaryAsync(
        SystemEventLogQuery query,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(runtimeSettings.ConnectionString))
        {
            return EmptySummary();
        }

        var normalizedQuery = NormalizeQuery(query);
        await using var connection = new NpgsqlConnection(runtimeSettings.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        const string countSql = """
SELECT
    COUNT(*)::int,
    COUNT(*) FILTER (WHERE log.severity = 'info')::int,
    COUNT(*) FILTER (WHERE log.severity = 'warning')::int,
    COUNT(*) FILTER (WHERE log.severity = 'error')::int
FROM system_event_log log
LEFT JOIN app_users actor ON actor.id = log.actor_user_id
WHERE (@severities IS NULL OR log.severity = ANY(@severities))
  AND (@source IS NULL OR log.source = @source)
  AND (@since IS NULL OR log.created_at >= @since)
  AND (@until IS NULL OR log.created_at <= @until)
  AND (@search IS NULL OR (
        log.message ILIKE @search
        OR COALESCE(log.user_message, '') ILIKE @search
        OR COALESCE(log.client_route, '') ILIKE @search
        OR COALESCE(log.client_function, '') ILIKE @search
        OR COALESCE(log.http_path, '') ILIKE @search
        OR COALESCE(log.event_key, '') ILIKE @search
        OR COALESCE(log.category, '') ILIKE @search
        OR COALESCE(log.task_ref, '') ILIKE @search
        OR COALESCE(actor.display_name, '') ILIKE @search
      ))
  AND (@actorUserId IS NULL OR log.actor_user_id = @actorUserId)
  AND (@workflowUid IS NULL OR log.workflow_uid = @workflowUid)
  AND (@rotationPlanId IS NULL OR log.rotation_plan_id = @rotationPlanId)
  AND (@taskRef IS NULL OR log.task_ref = @taskRef);
""";

        const string sourceSql = """
SELECT
    log.source,
    COUNT(*)::int
FROM system_event_log log
LEFT JOIN app_users actor ON actor.id = log.actor_user_id
WHERE (@severities IS NULL OR log.severity = ANY(@severities))
  AND (@source IS NULL OR log.source = @source)
  AND (@since IS NULL OR log.created_at >= @since)
  AND (@until IS NULL OR log.created_at <= @until)
  AND (@search IS NULL OR (
        log.message ILIKE @search
        OR COALESCE(log.user_message, '') ILIKE @search
        OR COALESCE(log.client_route, '') ILIKE @search
        OR COALESCE(log.client_function, '') ILIKE @search
        OR COALESCE(log.http_path, '') ILIKE @search
        OR COALESCE(log.event_key, '') ILIKE @search
        OR COALESCE(log.category, '') ILIKE @search
        OR COALESCE(log.task_ref, '') ILIKE @search
        OR COALESCE(actor.display_name, '') ILIKE @search
      ))
  AND (@actorUserId IS NULL OR log.actor_user_id = @actorUserId)
  AND (@workflowUid IS NULL OR log.workflow_uid = @workflowUid)
  AND (@rotationPlanId IS NULL OR log.rotation_plan_id = @rotationPlanId)
  AND (@taskRef IS NULL OR log.task_ref = @taskRef)
GROUP BY log.source
ORDER BY COUNT(*) DESC, log.source ASC;
""";

        var summary = EmptySummary();
        await using (var command = BuildFilterCommand(countSql, normalizedQuery, connection))
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            if (await reader.ReadAsync(cancellationToken))
            {
                summary = new AdminSystemLogSummaryDto
                {
                    TotalCount = reader.GetInt32(0),
                    InfoCount = reader.GetInt32(1),
                    WarningCount = reader.GetInt32(2),
                    ErrorCount = reader.GetInt32(3),
                    Sources = []
                };
            }
        }

        await using (var command = BuildFilterCommand(sourceSql, normalizedQuery, connection))
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                summary.Sources.Add(new AdminSystemLogSourceCountDto
                {
                    Source = reader.GetString(0),
                    Count = reader.GetInt32(1)
                });
            }
        }

        return summary;
    }

    private static NpgsqlCommand BuildFilterCommand(string sql, SystemEventLogQuery query, NpgsqlConnection connection)
    {
        var command = new NpgsqlCommand(sql, connection);
        command.Parameters.Add("severities", NpgsqlDbType.Array | NpgsqlDbType.Varchar).Value =
            query.Severities.Count > 0 ? query.Severities.ToArray() : DBNull.Value;
        command.Parameters.Add(
            new NpgsqlParameter("source", NpgsqlDbType.Varchar)
            {
                Value = (object?)NormalizeOptionalText(query.Source) ?? DBNull.Value
            });
        command.Parameters.Add(
            new NpgsqlParameter("since", NpgsqlDbType.TimestampTz)
            {
                Value = (object?)query.Since ?? DBNull.Value
            });
        command.Parameters.Add(
            new NpgsqlParameter("until", NpgsqlDbType.TimestampTz)
            {
                Value = (object?)query.Until ?? DBNull.Value
            });
        command.Parameters.Add(
            new NpgsqlParameter("search", NpgsqlDbType.Text)
            {
                Value = (object?)ToSearchLike(query.Search) ?? DBNull.Value
            });
        command.Parameters.Add(
            new NpgsqlParameter("actorUserId", NpgsqlDbType.Bigint)
            {
                Value = (object?)query.ActorUserId ?? DBNull.Value
            });
        command.Parameters.Add(
            new NpgsqlParameter("workflowUid", NpgsqlDbType.Uuid)
            {
                Value = (object?)query.WorkflowUid ?? DBNull.Value
            });
        command.Parameters.Add(
            new NpgsqlParameter("rotationPlanId", NpgsqlDbType.Bigint)
            {
                Value = (object?)query.RotationPlanId ?? DBNull.Value
            });
        command.Parameters.Add(
            new NpgsqlParameter("taskRef", NpgsqlDbType.Varchar)
            {
                Value = (object?)NormalizeOptionalText(query.TaskRef) ?? DBNull.Value
            });
        return command;
    }

    private static AdminSystemLogSummaryDto EmptySummary()
    {
        return new AdminSystemLogSummaryDto
        {
            TotalCount = 0,
            InfoCount = 0,
            WarningCount = 0,
            ErrorCount = 0,
            Sources = []
        };
    }

    private static SystemEventLogQuery NormalizeQuery(SystemEventLogQuery query)
    {
        return new SystemEventLogQuery
        {
            Severities = query.Severities
                .Select(NormalizeSeverity)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            Source = NormalizeOptionalText(query.Source) is { Length: > 0 } source ? NormalizeSource(source) : null,
            Since = query.Since,
            Until = query.Until,
            Search = NormalizeOptionalText(query.Search),
            ActorUserId = query.ActorUserId is > 0 ? query.ActorUserId : null,
            WorkflowUid = query.WorkflowUid,
            RotationPlanId = query.RotationPlanId is > 0 ? query.RotationPlanId : null,
            TaskRef = NormalizeOptionalText(query.TaskRef),
            Limit = Math.Clamp(query.Limit, 1, 200),
            Cursor = query.Cursor
        };
    }

    internal static (DateTime CreatedAt, long Id)? DecodeCursor(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor))
        {
            return null;
        }

        var query = new CursorPageQuery { Cursor = cursor };
        return query.DecodedCursor;
    }

    internal static string NormalizeSeverity(string? value)
    {
        var normalized = NormalizeValue(value, "info");
        return AllowedSeverities.Contains(normalized) ? normalized : "info";
    }

    internal static string NormalizeSource(string? value)
    {
        var normalized = NormalizeValue(value, "system");
        return AllowedSources.Contains(normalized) ? normalized : "system";
    }

    internal static string NormalizeValue(string? value, string fallback)
    {
        var normalized = NormalizeOptionalText(value)?.ToLowerInvariant();
        return string.IsNullOrWhiteSpace(normalized) ? fallback : normalized;
    }

    internal static string NormalizeMessage(string? value)
    {
        var normalized = NormalizeOptionalText(value);
        return string.IsNullOrWhiteSpace(normalized) ? "Systemereignis protokolliert." : normalized;
    }

    internal static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string? ToSearchLike(string? value)
    {
        var normalized = NormalizeOptionalText(value);
        return normalized is null ? null : $"%{normalized}%";
    }

    internal static string? BuildRedactedDetailsJson(object? details)
    {
        if (details is null)
        {
            return null;
        }

        JsonNode? node;
        if (details is JsonElement jsonElement)
        {
            node = JsonNode.Parse(jsonElement.GetRawText());
        }
        else
        {
            node = JsonSerializer.SerializeToNode(details);
        }

        if (node is null)
        {
            return null;
        }

        RedactNode(node, parentKey: null);
        return node.ToJsonString();
    }

    private static void RedactNode(JsonNode? node, string? parentKey)
    {
        if (node is JsonObject obj)
        {
            foreach (var property in obj.ToList())
            {
                if (property.Value is null)
                {
                    continue;
                }

                if (ShouldRedact(property.Key))
                {
                    obj[property.Key] = "[REDACTED]";
                    continue;
                }

                if (ShouldRedactBody(property.Key))
                {
                    obj[property.Key] = "[OMITTED]";
                    continue;
                }

                RedactNode(property.Value, property.Key);
            }

            return;
        }

        if (node is JsonArray array)
        {
            foreach (var item in array)
            {
                RedactNode(item, parentKey);
            }

            return;
        }

        if (node is JsonValue value && parentKey is not null)
        {
            if (ShouldRedact(parentKey))
            {
                ReplaceValue(value, "[REDACTED]");
                return;
            }

            if (ShouldRedactBody(parentKey))
            {
                ReplaceValue(value, "[OMITTED]");
            }
        }
    }

    private static void ReplaceValue(JsonValue value, string replacement)
    {
        if (value.GetValueKind() is JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False)
        {
            value.ReplaceWith(JsonValue.Create(replacement));
        }
    }

    internal static bool ShouldRedact(string key)
    {
        var normalized = key.Trim().ToLowerInvariant();
        return RedactedKeyFragments.Any(fragment => normalized.Contains(fragment, StringComparison.Ordinal));
    }

    internal static bool ShouldRedactBody(string key)
    {
        var normalized = key.Trim().ToLowerInvariant();
        return BodyKeyFragments.Any(fragment => normalized.Contains(fragment, StringComparison.Ordinal));
    }
}
