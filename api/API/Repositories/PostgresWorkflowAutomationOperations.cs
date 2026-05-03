using System.Text.Json;
using Npgsql;
using NpgsqlTypes;

namespace API;

internal sealed class PostgresWorkflowAutomationOperations : IWorkflowAutomationOperations
{
    internal const string AutomationJobStatusPending = "pending";
    internal const string AutomationJobStatusRunning = "running";
    internal const string AutomationJobStatusSucceeded = "succeeded";
    internal const string AutomationJobStatusFailed = "failed";
    internal const string AutomationJobStatusCancelled = "cancelled";

    public Task<bool> ActionDefinitionExists(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        string actionKey,
        bool requireActive)
        => ActionDefinitionExistsAsync(connection, transaction, actionKey, requireActive);

    public Task<long> ResolveActionDefinitionIdByKey(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string actionKey,
        bool requireActive)
        => ResolveActionDefinitionIdByKeyAsync(connection, transaction, actionKey, requireActive);

    internal static async Task<int> LoadNextAutomationAttemptNumberAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long jobId,
        CancellationToken cancellationToken)
    {
        const string sql = """
SELECT COALESCE(MAX(attempt_number), 0) + 1
FROM automation_job_attempts
WHERE automation_job_id = @jobId;
""";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("jobId", jobId);
        var scalar = await command.ExecuteScalarAsync(cancellationToken);
        return scalar is int attemptNumber ? attemptNumber : 1;
    }

    internal static async Task InsertAutomationJobAttemptAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long jobId,
        int attemptNumber,
        CancellationToken cancellationToken)
    {
        const string sql = """
INSERT INTO automation_job_attempts (
    automation_job_id,
    attempt_number,
    status,
    started_at
)
VALUES (
    @jobId,
    @attemptNumber,
    @status,
    NOW()
);
""";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("jobId", jobId);
        command.Parameters.AddWithValue("attemptNumber", attemptNumber);
        command.Parameters.AddWithValue("status", AutomationJobStatusRunning);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    internal static async Task CompleteAutomationAttemptAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long jobId,
        int attemptNumber,
        string status,
        string? errorMessage,
        CancellationToken cancellationToken)
    {
        const string sql = """
UPDATE automation_job_attempts
SET
    status = @status,
    error_message = @errorMessage,
    completed_at = NOW()
WHERE automation_job_id = @jobId
  AND attempt_number = @attemptNumber;
""";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("jobId", jobId);
        command.Parameters.AddWithValue("attemptNumber", attemptNumber);
        command.Parameters.AddWithValue("status", status);
        command.Parameters.AddWithValue("errorMessage", (object?)errorMessage ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    internal static async Task SetAutomationJobStatusAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long jobId,
        string status,
        DateTime? startedAtUtc,
        DateTime? completedAtUtc,
        DateTime? availableAtUtc,
        CancellationToken cancellationToken)
    {
        const string sql = """
UPDATE automation_jobs
SET
    status = @status,
    started_at = @startedAtUtc,
    completed_at = @completedAtUtc,
    available_at = COALESCE(@availableAtUtc, available_at)
WHERE id = @jobId;
""";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("jobId", jobId);
        command.Parameters.AddWithValue("status", status);
        command.Parameters.Add("startedAtUtc", NpgsqlDbType.TimestampTz).Value = (object?)startedAtUtc ?? DBNull.Value;
        command.Parameters.Add("completedAtUtc", NpgsqlDbType.TimestampTz).Value = (object?)completedAtUtc ?? DBNull.Value;
        command.Parameters.Add("availableAtUtc", NpgsqlDbType.TimestampTz).Value = (object?)availableAtUtc ?? DBNull.Value;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    internal static async Task InsertAutomationLogsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long jobId,
        IReadOnlyList<WorkflowAutomationLogEntry> logs,
        CancellationToken cancellationToken)
    {
        if (logs.Count == 0)
        {
            return;
        }

        const string sql = """
INSERT INTO automation_job_logs (
    automation_job_id,
    level,
    message,
    details_json
)
VALUES (
    @jobId,
    @level,
    @message,
    @detailsJson
);
""";

        foreach (var log in logs)
        {
            await using var command = new NpgsqlCommand(sql, connection, transaction);
            command.Parameters.AddWithValue("jobId", jobId);
            command.Parameters.AddWithValue("level", NormalizeAutomationLogLevel(log.Level));
            command.Parameters.AddWithValue("message", log.Message.Trim());
            command.Parameters.Add(
                new NpgsqlParameter("detailsJson", NpgsqlDbType.Jsonb)
                {
                    Value = log.Details.HasValue ? JsonSerializer.Serialize(log.Details.Value) : DBNull.Value
                });
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    internal static string NormalizeAutomationLogLevel(string level)
    {
        var normalized = level.Trim().ToLowerInvariant();
        return normalized is "debug" or "info" or "warning" or "error" ? normalized : "info";
    }

    internal static bool IsMissingWorkflowBuilderAutomationSchema(PostgresException ex)
    {
        if (ex.SqlState != PostgresErrorCodes.UndefinedTable)
        {
            return false;
        }

        return string.Equals(ex.TableName, "action_definitions", StringComparison.OrdinalIgnoreCase)
            || string.Equals(ex.TableName, "workflow_node_actions", StringComparison.OrdinalIgnoreCase)
            || string.Equals(ex.TableName, "automation_jobs", StringComparison.OrdinalIgnoreCase)
            || string.Equals(ex.TableName, "automation_job_attempts", StringComparison.OrdinalIgnoreCase)
            || string.Equals(ex.TableName, "automation_job_logs", StringComparison.OrdinalIgnoreCase);
    }

    internal static async Task<bool> ActionDefinitionExistsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        string actionKey,
        bool requireActive)
    {
        const string sql = """
SELECT 1
FROM action_definitions
WHERE LOWER(action_key) = LOWER(@actionKey)
  AND (@requireActive = FALSE OR is_active = TRUE)
LIMIT 1;
""";

        try
        {
            await using var command = new NpgsqlCommand(sql, connection, transaction);
            command.Parameters.AddWithValue("actionKey", actionKey.Trim());
            command.Parameters.AddWithValue("requireActive", requireActive);
            return await command.ExecuteScalarAsync() is not null;
        }
        catch (PostgresException ex) when (IsMissingWorkflowBuilderAutomationSchema(ex))
        {
            return false;
        }
    }

    internal static async Task<long> ResolveActionDefinitionIdByKeyAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string actionKey,
        bool requireActive)
    {
        const string sql = """
SELECT id
FROM action_definitions
WHERE LOWER(action_key) = LOWER(@actionKey)
  AND (@requireActive = FALSE OR is_active = TRUE)
LIMIT 1;
""";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("actionKey", actionKey.Trim());
        command.Parameters.AddWithValue("requireActive", requireActive);
        var scalar = await command.ExecuteScalarAsync();
        if (scalar is long id)
        {
            return id;
        }

        throw new InvalidOperationException(
            requireActive
                ? $"Active action definition '{actionKey}' was not found."
                : $"Action definition '{actionKey}' was not found.");
    }

    internal static async Task CancelPendingAutomationJobsForNodeInstanceAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long workflowNodeInstanceId,
        CancellationToken cancellationToken)
    {
        const string sql = """
UPDATE automation_jobs
SET
    status = @cancelledStatus,
    completed_at = COALESCE(completed_at, NOW())
WHERE workflow_node_instance_id = @workflowNodeInstanceId
  AND status = @pendingStatus;
""";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("workflowNodeInstanceId", workflowNodeInstanceId);
        command.Parameters.AddWithValue("cancelledStatus", AutomationJobStatusCancelled);
        command.Parameters.AddWithValue("pendingStatus", AutomationJobStatusPending);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    internal static async Task<WorkflowNodeActionRecord?> LoadNextAutomationNodeActionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long workflowNodeId,
        int currentExecutionOrder,
        CancellationToken cancellationToken)
    {
        const string sql = """
SELECT
    wna.id,
    wna.action_definition_id,
    wna.execution_order,
    wna.on_error_behavior,
    wna.input_mapping_json::text,
    ad.action_key,
    ad.name,
    ad.handler_type,
    ad.is_idempotent
FROM workflow_node_actions wna
INNER JOIN action_definitions ad ON ad.id = wna.action_definition_id
WHERE wna.workflow_node_id = @workflowNodeId
  AND wna.execution_order > @currentExecutionOrder
ORDER BY wna.execution_order, wna.id
LIMIT 1;
""";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("workflowNodeId", workflowNodeId);
        command.Parameters.AddWithValue("currentExecutionOrder", currentExecutionOrder);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new WorkflowNodeActionRecord
        {
            Id = reader.GetInt64(0),
            ActionDefinitionId = reader.GetInt64(1),
            ExecutionOrder = reader.GetInt32(2),
            OnErrorBehavior = reader.GetString(3),
            InputMapping = reader.IsDBNull(4) ? null : PostgresRepositorySharedHelpers.ParseJsonElement(reader.GetString(4)),
            ActionKey = reader.GetString(5),
            ActionName = reader.GetString(6),
            HandlerType = reader.GetString(7),
            IsIdempotent = reader.GetBoolean(8)
        };
    }

    internal static async Task<long> CreateAutomationJobAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long workflowId,
        long workflowNodeInstanceId,
        long workflowNodeActionId,
        long actionDefinitionId,
        JsonElement payload,
        CancellationToken cancellationToken)
    {
        const string sql = """
INSERT INTO automation_jobs (
    workflow_id,
    workflow_node_instance_id,
    workflow_node_action_id,
    action_definition_id,
    status,
    payload_json,
    available_at
)
VALUES (
    @workflowId,
    @workflowNodeInstanceId,
    @workflowNodeActionId,
    @actionDefinitionId,
    @status,
    @payloadJson,
    NOW()
)
RETURNING id;
""";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("workflowId", workflowId);
        command.Parameters.AddWithValue("workflowNodeInstanceId", workflowNodeInstanceId);
        command.Parameters.AddWithValue("workflowNodeActionId", workflowNodeActionId);
        command.Parameters.AddWithValue("actionDefinitionId", actionDefinitionId);
        command.Parameters.AddWithValue("status", AutomationJobStatusPending);
        command.Parameters.Add(
            new NpgsqlParameter("payloadJson", NpgsqlDbType.Jsonb)
            {
                Value = JsonSerializer.Serialize(payload)
            });

        var scalar = await command.ExecuteScalarAsync(cancellationToken);
        if (scalar is not long jobId)
        {
            throw new InvalidOperationException("Automation job could not be created.");
        }

        return jobId;
    }

    internal static async Task<JsonElement> BuildAutomationJobPayloadAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long workflowId,
        JsonElement? inputMapping,
        IReadOnlyDictionary<string, StoredWorkflowAnswerRecord> answersByKey,
        CancellationToken cancellationToken)
    {
        var context = await LoadAutomationPayloadContextAsync(connection, transaction, workflowId, cancellationToken);
        if (!HasJsonValue(inputMapping))
        {
            return JsonSerializer.SerializeToElement(new
            {
                workflowUid = context.WorkflowUid,
                definitionKey = context.WorkflowDefinitionKey
            });
        }

        var resolved = ResolveAutomationMappingValue(inputMapping!.Value, context, answersByKey);
        return JsonSerializer.SerializeToElement(resolved);
    }

    internal static async Task<AutomationPayloadContextRecord> LoadAutomationPayloadContextAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long workflowId,
        CancellationToken cancellationToken)
    {
        const string sql = """
SELECT
    w.id,
    w.uid,
    d.definition_key,
    w.department_id,
    w.position_role_id,
    w.first_name,
    w.last_name,
    w.employee_number,
    w.badge_number,
    w.deadline_date,
    w.target_person_id,
    p.department_id,
    p.current_position_role_id,
    p.app_user_id,
    p.directory_identity_id,
    p.first_name,
    p.last_name,
    p.employee_number,
    p.badge_number,
    p.employment_status,
    p.entry_date,
    p.exit_date,
    COALESCE(
        NULLIF(BTRIM(CONCAT_WS(' ', p.first_name, p.last_name)), ''),
        u.display_name,
        di.display_name,
        CASE WHEN p.id IS NULL THEN NULL ELSE 'Person #' || p.id::text END
    ),
    COALESCE(di.mail, u.email),
    di.user_principal_name,
    di.mail,
    di.display_name,
    di.department_name,
    di.employee_number,
    di.account_enabled
FROM workflows w
LEFT JOIN workflow_definition_versions v ON v.id = w.workflow_definition_version_id
LEFT JOIN workflow_definitions d ON d.id = v.workflow_definition_id
LEFT JOIN people p ON p.id = w.target_person_id
LEFT JOIN app_users u ON u.id = p.app_user_id
LEFT JOIN directory_identities di ON di.id = p.directory_identity_id
WHERE w.id = @workflowId
LIMIT 1;
""";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("workflowId", workflowId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException("Automation payload context could not be loaded.");
        }

        return new AutomationPayloadContextRecord
        {
            WorkflowId = reader.GetInt64(0),
            WorkflowUid = reader.GetGuid(1),
            WorkflowDefinitionKey = reader.IsDBNull(2) ? null : reader.GetString(2),
            DepartmentId = reader.GetInt32(3),
            RoleId = reader.GetInt32(4),
            FirstName = reader.IsDBNull(5) ? null : reader.GetString(5),
            LastName = reader.IsDBNull(6) ? null : reader.GetString(6),
            EmployeeNumber = reader.IsDBNull(7) ? null : reader.GetInt32(7),
            BadgeNumber = reader.IsDBNull(8) ? null : reader.GetInt32(8),
            DeadlineDate = reader.IsDBNull(9) ? null : reader.GetFieldValue<DateOnly>(9),
            TargetPersonId = reader.IsDBNull(10) ? null : reader.GetInt64(10),
            TargetDepartmentId = reader.IsDBNull(11) ? null : reader.GetInt32(11),
            TargetRoleId = reader.IsDBNull(12) ? null : reader.GetInt32(12),
            TargetAppUserId = reader.IsDBNull(13) ? null : reader.GetInt64(13),
            TargetDirectoryIdentityId = reader.IsDBNull(14) ? null : reader.GetInt64(14),
            TargetFirstName = reader.IsDBNull(15) ? null : reader.GetString(15),
            TargetLastName = reader.IsDBNull(16) ? null : reader.GetString(16),
            TargetEmployeeNumber = reader.IsDBNull(17) ? null : reader.GetInt32(17),
            TargetBadgeNumber = reader.IsDBNull(18) ? null : reader.GetInt32(18),
            TargetEmploymentStatus = reader.IsDBNull(19) ? null : reader.GetString(19),
            TargetEntryDate = reader.IsDBNull(20) ? null : reader.GetFieldValue<DateOnly>(20),
            TargetExitDate = reader.IsDBNull(21) ? null : reader.GetFieldValue<DateOnly>(21),
            TargetDisplayName = reader.IsDBNull(22) ? null : reader.GetString(22),
            TargetEmail = reader.IsDBNull(23) ? null : reader.GetString(23),
            DirectoryUserPrincipalName = reader.IsDBNull(24) ? null : reader.GetString(24),
            DirectoryMail = reader.IsDBNull(25) ? null : reader.GetString(25),
            DirectoryDisplayName = reader.IsDBNull(26) ? null : reader.GetString(26),
            DirectoryDepartmentName = reader.IsDBNull(27) ? null : reader.GetString(27),
            DirectoryEmployeeNumber = reader.IsDBNull(28) ? null : reader.GetInt32(28),
            DirectoryAccountEnabled = reader.IsDBNull(29) ? null : reader.GetBoolean(29)
        };
    }

    private static object? ResolveAutomationMappingValue(
        JsonElement element,
        AutomationPayloadContextRecord context,
        IReadOnlyDictionary<string, StoredWorkflowAnswerRecord> answersByKey)
    {
        if (element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty("source", out var sourceProperty)
            && sourceProperty.ValueKind == JsonValueKind.String)
        {
            return ResolveAutomationReference(element, sourceProperty.GetString()!, context, answersByKey);
        }

        return element.ValueKind switch
        {
            JsonValueKind.Object => element.EnumerateObject()
                .ToDictionary(
                    property => property.Name,
                    property => ResolveAutomationMappingValue(property.Value, context, answersByKey)),
            JsonValueKind.Array => element.EnumerateArray()
                .Select(item => ResolveAutomationMappingValue(item, context, answersByKey))
                .ToList(),
            _ => ConvertJsonElementToObject(element)
        };
    }

    private static object? ResolveAutomationReference(
        JsonElement element,
        string source,
        AutomationPayloadContextRecord context,
        IReadOnlyDictionary<string, StoredWorkflowAnswerRecord> answersByKey)
    {
        switch (source.Trim().ToLowerInvariant())
        {
            case "workflow":
            {
                var property = element.GetProperty("property").GetString();
                return ResolveWorkflowAutomationContextProperty(context, property);
            }
            case "target_person":
            case "person":
            {
                var property = element.GetProperty("property").GetString();
                return ResolvePersonAutomationContextProperty(context, property);
            }
            case "directory_identity":
            {
                var property = element.GetProperty("property").GetString();
                return ResolveDirectoryIdentityAutomationContextProperty(context, property);
            }
            case "answer":
            {
                var answerKey = element.GetProperty("answerKey").GetString();
                return answerKey is not null && answersByKey.TryGetValue(answerKey, out var answer)
                    ? ConvertStoredAnswerToValue(answer)
                    : null;
            }
            case "static":
                return element.TryGetProperty("value", out var valueProperty)
                    ? ConvertJsonElementToObject(valueProperty)
                    : null;
            default:
                throw new InvalidOperationException($"Unsupported automation input mapping source '{source}'.");
        }
    }

    private static object? ResolveWorkflowAutomationContextProperty(
        AutomationPayloadContextRecord context,
        string? property)
    {
        return property?.Trim().ToLowerInvariant() switch
        {
            "workflowid" => context.WorkflowId,
            "workflowuid" => context.WorkflowUid,
            "definitionkey" => context.WorkflowDefinitionKey,
            "departmentid" => context.DepartmentId,
            "roleid" => context.RoleId,
            "firstname" => context.FirstName,
            "lastname" => context.LastName,
            "employeenumber" => context.EmployeeNumber,
            "badgenumber" => context.BadgeNumber,
            "deadlinedate" => context.DeadlineDate?.ToString("yyyy-MM-dd"),
            "targetpersonid" => context.TargetPersonId,
            _ => null
        };
    }

    private static object? ResolvePersonAutomationContextProperty(
        AutomationPayloadContextRecord context,
        string? property)
    {
        return property?.Trim().ToLowerInvariant() switch
        {
            "personid" => context.TargetPersonId,
            "departmentid" => context.TargetDepartmentId,
            "roleid" => context.TargetRoleId,
            "appuserid" => context.TargetAppUserId,
            "directoryidentityid" => context.TargetDirectoryIdentityId,
            "firstname" => context.TargetFirstName,
            "lastname" => context.TargetLastName,
            "employeenumber" => context.TargetEmployeeNumber,
            "badgenumber" => context.TargetBadgeNumber,
            "employmentstatus" => context.TargetEmploymentStatus,
            "entrydate" => context.TargetEntryDate?.ToString("yyyy-MM-dd"),
            "exitdate" => context.TargetExitDate?.ToString("yyyy-MM-dd"),
            "displayname" => context.TargetDisplayName,
            "email" => context.TargetEmail,
            _ => null
        };
    }

    private static object? ResolveDirectoryIdentityAutomationContextProperty(
        AutomationPayloadContextRecord context,
        string? property)
    {
        return property?.Trim().ToLowerInvariant() switch
        {
            "directoryidentityid" => context.TargetDirectoryIdentityId,
            "userprincipalname" => context.DirectoryUserPrincipalName,
            "mail" => context.DirectoryMail,
            "displayname" => context.DirectoryDisplayName,
            "departmentname" => context.DirectoryDepartmentName,
            "employeenumber" => context.DirectoryEmployeeNumber,
            "accountenabled" => context.DirectoryAccountEnabled,
            _ => null
        };
    }

    private static object? ConvertStoredAnswerToValue(StoredWorkflowAnswerRecord answer)
    {
        if (answer.SelectedOptionValues.Count > 0)
        {
            return answer.SelectedOptionValues.ToList();
        }

        if (answer.ValueBoolean.HasValue)
        {
            return answer.ValueBoolean.Value;
        }

        if (!string.IsNullOrWhiteSpace(answer.ValueText))
        {
            return answer.ValueText;
        }

        if (answer.ValueNumber.HasValue)
        {
            return answer.ValueNumber.Value;
        }

        if (!string.IsNullOrWhiteSpace(answer.SelectedOptionValue))
        {
            return answer.SelectedOptionValue;
        }

        return null;
    }

    private static object? ConvertJsonElementToObject(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number when element.TryGetInt64(out var intValue) => intValue,
            JsonValueKind.Number when element.TryGetDecimal(out var decimalValue) => decimalValue,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            JsonValueKind.Array => element.EnumerateArray().Select(ConvertJsonElementToObject).ToList(),
            JsonValueKind.Object => element.EnumerateObject().ToDictionary(p => p.Name, p => ConvertJsonElementToObject(p.Value)),
            _ => null
        };
    }

    private static bool HasJsonValue(JsonElement? value)
    {
        return value.HasValue
               && value.Value.ValueKind is not JsonValueKind.Null
               && value.Value.ValueKind is not JsonValueKind.Undefined;
    }
}

internal sealed class AutomationPayloadContextRecord
{
    public required long WorkflowId { get; init; }
    public required Guid WorkflowUid { get; init; }
    public string? WorkflowDefinitionKey { get; init; }
    public required int DepartmentId { get; init; }
    public required int RoleId { get; init; }
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public int? EmployeeNumber { get; init; }
    public int? BadgeNumber { get; init; }
    public DateOnly? DeadlineDate { get; init; }
    public long? TargetPersonId { get; init; }
    public int? TargetDepartmentId { get; init; }
    public int? TargetRoleId { get; init; }
    public long? TargetAppUserId { get; init; }
    public long? TargetDirectoryIdentityId { get; init; }
    public string? TargetFirstName { get; init; }
    public string? TargetLastName { get; init; }
    public int? TargetEmployeeNumber { get; init; }
    public int? TargetBadgeNumber { get; init; }
    public string? TargetEmploymentStatus { get; init; }
    public DateOnly? TargetEntryDate { get; init; }
    public DateOnly? TargetExitDate { get; init; }
    public string? TargetDisplayName { get; init; }
    public string? TargetEmail { get; init; }
    public string? DirectoryUserPrincipalName { get; init; }
    public string? DirectoryMail { get; init; }
    public string? DirectoryDisplayName { get; init; }
    public string? DirectoryDepartmentName { get; init; }
    public int? DirectoryEmployeeNumber { get; init; }
    public bool? DirectoryAccountEnabled { get; init; }
}
