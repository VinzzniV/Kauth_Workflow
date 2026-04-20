using System.Text.Json;
using Npgsql;
using NpgsqlTypes;

namespace API;

internal sealed partial class PostgresWorkflowRepository
{
    private const string AutomationJobStatusPending = "pending";
    private const string AutomationJobStatusRunning = "running";
    private const string AutomationJobStatusSucceeded = "succeeded";
    private const string AutomationJobStatusFailed = "failed";
    private const string AutomationJobStatusCancelled = "cancelled";

    public async Task<IReadOnlyList<ActionDefinitionDto>> GetAdminActionDefinitions(CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync(cancellationToken);

        const string sql = """
SELECT
    id,
    action_key,
    name,
    description,
    handler_type,
    parameter_schema_json::text,
    is_active,
    requires_approval,
    is_idempotent,
    created_at,
    updated_at
FROM action_definitions
ORDER BY name, action_key, id;
""";

        var results = new List<ActionDefinitionDto>();

        try
        {
            await using var command = new NpgsqlCommand(sql, connection);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                results.Add(new ActionDefinitionDto
                {
                    Id = reader.GetInt64(0),
                    Key = reader.GetString(1),
                    Name = reader.GetString(2),
                    Description = reader.IsDBNull(3) ? null : reader.GetString(3),
                    HandlerType = reader.GetString(4),
                    ParameterSchema = reader.IsDBNull(5) ? null : ParseJsonElement(reader.GetString(5)),
                    IsActive = reader.GetBoolean(6),
                    RequiresApproval = reader.GetBoolean(7),
                    IsIdempotent = reader.GetBoolean(8),
                    CreatedAt = reader.GetDateTime(9),
                    UpdatedAt = reader.GetDateTime(10)
                });
            }
        }
        catch (PostgresException ex) when (IsMissingWorkflowBuilderAutomationSchema(ex))
        {
            return [];
        }

        return results;
    }

    public async Task<IReadOnlyList<AutomationJobDetailDto>> GetAutomationJobs(
        Guid workflowUid,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync(cancellationToken);

        const string jobSql = """
SELECT
    j.id,
    j.workflow_id,
    j.workflow_node_instance_id,
    n.node_key,
    ad.action_key,
    ad.name,
    wna.execution_order,
    wna.on_error_behavior,
    j.status,
    j.payload_json::text,
    j.available_at,
    j.created_at,
    j.started_at,
    j.completed_at
FROM automation_jobs j
INNER JOIN workflows w ON w.id = j.workflow_id
INNER JOIN workflow_node_actions wna ON wna.id = j.workflow_node_action_id
INNER JOIN action_definitions ad ON ad.id = j.action_definition_id
INNER JOIN workflow_node_instances ni ON ni.id = j.workflow_node_instance_id
INNER JOIN workflow_nodes n ON n.id = ni.workflow_node_id
WHERE w.uid = @workflowUid
ORDER BY j.created_at, j.id;
""";

        var jobs = new List<AutomationJobDetailDto>();
        var jobIds = new List<long>();
        await using (var command = new NpgsqlCommand(jobSql, connection))
        {
            command.Parameters.AddWithValue("workflowUid", workflowUid);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var jobId = reader.GetInt64(0);
                jobIds.Add(jobId);
                jobs.Add(new AutomationJobDetailDto
                {
                    Id = jobId,
                    WorkflowId = reader.GetInt64(1),
                    WorkflowNodeInstanceId = reader.GetInt64(2),
                    NodeKey = reader.GetString(3),
                    ActionKey = reader.GetString(4),
                    ActionName = reader.GetString(5),
                    ExecutionOrder = reader.GetInt32(6),
                    OnErrorBehavior = reader.GetString(7),
                    Status = reader.GetString(8),
                    Payload = reader.IsDBNull(9) ? null : ParseJsonElement(reader.GetString(9)),
                    AvailableAt = reader.IsDBNull(10) ? null : reader.GetDateTime(10),
                    CreatedAt = reader.GetDateTime(11),
                    StartedAt = reader.IsDBNull(12) ? null : reader.GetDateTime(12),
                    CompletedAt = reader.IsDBNull(13) ? null : reader.GetDateTime(13),
                    Attempts = new List<AutomationJobAttemptDto>(),
                    Logs = new List<AutomationJobLogDto>()
                });
            }
        }

        if (jobIds.Count == 0)
        {
            return jobs;
        }

        var jobById = jobs.ToDictionary(job => job.Id);

        const string attemptSql = """
SELECT
    automation_job_id,
    id,
    attempt_number,
    status,
    error_message,
    started_at,
    completed_at
FROM automation_job_attempts
WHERE automation_job_id = ANY(@jobIds)
ORDER BY automation_job_id, attempt_number, id;
""";

        await using (var command = new NpgsqlCommand(attemptSql, connection))
        {
            command.Parameters.AddWithValue("jobIds", jobIds.ToArray());
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                if (!jobById.TryGetValue(reader.GetInt64(0), out var job))
                {
                    continue;
                }

                job.Attempts.Add(new AutomationJobAttemptDto
                {
                    Id = reader.GetInt64(1),
                    AttemptNumber = reader.GetInt32(2),
                    Status = reader.GetString(3),
                    ErrorMessage = reader.IsDBNull(4) ? null : reader.GetString(4),
                    StartedAt = reader.GetDateTime(5),
                    CompletedAt = reader.IsDBNull(6) ? null : reader.GetDateTime(6)
                });
            }
        }

        const string logSql = """
SELECT
    automation_job_id,
    id,
    level,
    message,
    details_json::text,
    created_at
FROM automation_job_logs
WHERE automation_job_id = ANY(@jobIds)
ORDER BY automation_job_id, created_at, id;
""";

        await using (var command = new NpgsqlCommand(logSql, connection))
        {
            command.Parameters.AddWithValue("jobIds", jobIds.ToArray());
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                if (!jobById.TryGetValue(reader.GetInt64(0), out var job))
                {
                    continue;
                }

                job.Logs.Add(new AutomationJobLogDto
                {
                    Id = reader.GetInt64(1),
                    Level = reader.GetString(2),
                    Message = reader.GetString(3),
                    Details = reader.IsDBNull(4) ? null : ParseJsonElement(reader.GetString(4)),
                    CreatedAt = reader.GetDateTime(5)
                });
            }
        }

        return jobs;
    }

    public async Task<ClaimedAutomationJobRecord?> ClaimNextPendingAutomationJob(CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        const string claimSql = """
SELECT
    j.id,
    j.workflow_id,
    w.uid,
    j.workflow_node_instance_id,
    ni.workflow_node_id,
    n.node_key,
    n.node_type,
    j.workflow_node_action_id,
    wna.execution_order,
    wna.on_error_behavior,
    ad.id,
    ad.action_key,
    ad.name,
    ad.handler_type,
    ad.is_idempotent,
    j.payload_json::text,
    w.created_by_user_id
FROM automation_jobs j
INNER JOIN workflows w ON w.id = j.workflow_id
INNER JOIN workflow_node_instances ni ON ni.id = j.workflow_node_instance_id
INNER JOIN workflow_nodes n ON n.id = ni.workflow_node_id
INNER JOIN workflow_node_actions wna ON wna.id = j.workflow_node_action_id
INNER JOIN action_definitions ad ON ad.id = j.action_definition_id
WHERE j.status = @pendingStatus
  AND j.available_at <= NOW()
ORDER BY j.available_at, j.created_at, j.id
FOR UPDATE SKIP LOCKED
LIMIT 1;
""";

        ClaimedAutomationJobRecord? claimed = null;
        await using (var command = new NpgsqlCommand(claimSql, connection, transaction))
        {
            command.Parameters.AddWithValue("pendingStatus", AutomationJobStatusPending);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                claimed = new ClaimedAutomationJobRecord
                {
                    JobId = reader.GetInt64(0),
                    WorkflowId = reader.GetInt64(1),
                    WorkflowUid = reader.GetGuid(2),
                    WorkflowNodeInstanceId = reader.GetInt64(3),
                    WorkflowNodeId = reader.GetInt64(4),
                    NodeKey = reader.GetString(5),
                    NodeType = reader.GetString(6),
                    WorkflowNodeActionId = reader.GetInt64(7),
                    ExecutionOrder = reader.GetInt32(8),
                    OnErrorBehavior = reader.GetString(9),
                    ActionDefinitionId = reader.GetInt64(10),
                    ActionKey = reader.GetString(11),
                    ActionName = reader.GetString(12),
                    HandlerType = reader.GetString(13),
                    IsIdempotent = reader.GetBoolean(14),
                    Payload = reader.IsDBNull(15) ? null : ParseJsonElement(reader.GetString(15)),
                    CreatedByUserId = reader.IsDBNull(16) ? null : reader.GetInt64(16),
                    AttemptNumber = 0
                };
            }
        }

        if (claimed is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        var nextAttemptNumber = await LoadNextAutomationAttemptNumber(connection, transaction, claimed.JobId, cancellationToken);
        await InsertAutomationJobAttempt(connection, transaction, claimed.JobId, nextAttemptNumber, cancellationToken);
        await SetAutomationJobStatus(
            connection,
            transaction,
            claimed.JobId,
            AutomationJobStatusRunning,
            startedAtUtc: DateTime.UtcNow,
            completedAtUtc: null,
            availableAtUtc: null,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        claimed.AttemptNumber = nextAttemptNumber;
        return claimed;
    }

    private static async Task<int> LoadNextAutomationAttemptNumber(
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

    private static async Task InsertAutomationJobAttempt(
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

    private static async Task CompleteAutomationAttempt(
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

    private static async Task SetAutomationJobStatus(
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

    private static async Task InsertAutomationLogs(
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

    private static string NormalizeAutomationLogLevel(string level)
    {
        var normalized = level.Trim().ToLowerInvariant();
        return normalized is "debug" or "info" or "warning" or "error" ? normalized : "info";
    }

    private static bool IsMissingWorkflowBuilderAutomationSchema(PostgresException ex)
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

    private static async Task<bool> ActionDefinitionExists(
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

    private static async Task<long> ResolveActionDefinitionIdByKey(
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

    public async Task CompleteAutomationJobSuccess(
        ClaimedAutomationJobRecord job,
        WorkflowAutomationHandlerResult result,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await CompleteAutomationAttempt(
            connection,
            transaction,
            job.JobId,
            job.AttemptNumber,
            AutomationJobStatusSucceeded,
            errorMessage: null,
            cancellationToken);
        await SetAutomationJobStatus(
            connection,
            transaction,
            job.JobId,
            AutomationJobStatusSucceeded,
            startedAtUtc: null,
            completedAtUtc: DateTime.UtcNow,
            availableAtUtc: null,
            cancellationToken);
        await InsertAutomationLogs(connection, transaction, job.JobId, result.Logs, cancellationToken);

        var nextAction = await LoadNextAutomationNodeAction(
            connection,
            transaction,
            job.WorkflowNodeId,
            job.ExecutionOrder,
            cancellationToken);
        if (nextAction is not null)
        {
            var answersByKey = await LoadStoredAnswersByKey(connection, transaction, job.WorkflowId);
            var payload = await BuildAutomationJobPayload(
                connection,
                transaction,
                job.WorkflowId,
                nextAction.InputMapping,
                answersByKey,
                cancellationToken);

            await CreateAutomationJob(
                connection,
                transaction,
                job.WorkflowId,
                job.WorkflowNodeInstanceId,
                nextAction.Id,
                nextAction.ActionDefinitionId,
                payload,
                cancellationToken);
            await InsertWorkflowRuntimeEvent(
                connection,
                transaction,
                job.WorkflowId,
                job.WorkflowNodeInstanceId,
                "automation_action_succeeded",
                CreateJsonbPayload(new
                {
                    nodeKey = job.NodeKey,
                    actionKey = job.ActionKey,
                    executionOrder = job.ExecutionOrder
                }));
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        var header = await LoadRuntimeWorkflowHeader(connection, transaction, job.WorkflowUid)
            ?? throw new InvalidOperationException("Runtime workflow for automation job could not be loaded.");
        var graph = await LoadWorkflowDefinitionGraph(connection, transaction, header.WorkflowDefinitionVersionId);
        if (!graph.NodeById.TryGetValue(job.WorkflowNodeId, out var node))
        {
            throw new InvalidOperationException($"Runtime automation node '{job.NodeKey}' could not be resolved.");
        }

        await UpdateNodeInstanceStatus(
            connection,
            transaction,
            job.WorkflowNodeInstanceId,
            NodeInstanceStatusDone,
            result.Output.HasValue ? JsonSerializer.Serialize(result.Output.Value) : CreateJsonbPayload(new { actionKey = job.ActionKey, succeeded = true }));
        await InsertWorkflowRuntimeEvent(
            connection,
            transaction,
            job.WorkflowId,
            job.WorkflowNodeInstanceId,
            "automation_node_completed",
            CreateJsonbPayload(new { nodeKey = job.NodeKey, actionKey = job.ActionKey }));
        await InsertAuditEntry(
            connection,
            transaction,
            job.WorkflowId,
            null,
            job.CreatedByUserId,
            "runtime_automation_completed",
            null,
            NodeInstanceStatusDone,
            job.NodeKey);

        await AdvanceRuntimeUntilWaitOrTerminal(
            connection,
            transaction,
            job.WorkflowId,
            graph,
            node,
            await LoadStoredAnswersByKey(connection, transaction, job.WorkflowId),
            job.CreatedByUserId);

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task CompleteAutomationJobFailure(
        ClaimedAutomationJobRecord job,
        string errorMessage,
        bool shouldRetry,
        DateTime? retryAvailableAt,
        IReadOnlyList<WorkflowAutomationLogEntry> logs,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await CompleteAutomationAttempt(
            connection,
            transaction,
            job.JobId,
            job.AttemptNumber,
            AutomationJobStatusFailed,
            errorMessage,
            cancellationToken);
        await InsertAutomationLogs(connection, transaction, job.JobId, logs, cancellationToken);

        if (shouldRetry && retryAvailableAt.HasValue)
        {
            await SetAutomationJobStatus(
                connection,
                transaction,
                job.JobId,
                AutomationJobStatusPending,
                startedAtUtc: null,
                completedAtUtc: null,
                availableAtUtc: retryAvailableAt.Value,
                cancellationToken);
            await InsertWorkflowRuntimeEvent(
                connection,
                transaction,
                job.WorkflowId,
                job.WorkflowNodeInstanceId,
                "automation_retry_scheduled",
                CreateJsonbPayload(new
                {
                    nodeKey = job.NodeKey,
                    actionKey = job.ActionKey,
                    retryAvailableAt = retryAvailableAt.Value
                }));
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        await SetAutomationJobStatus(
            connection,
            transaction,
            job.JobId,
            AutomationJobStatusFailed,
            startedAtUtc: null,
            completedAtUtc: DateTime.UtcNow,
            availableAtUtc: null,
            cancellationToken);
        await CancelPendingAutomationJobsForNodeInstance(
            connection,
            transaction,
            job.WorkflowNodeInstanceId,
            cancellationToken);
        await UpdateNodeInstanceStatus(
            connection,
            transaction,
            job.WorkflowNodeInstanceId,
            NodeInstanceStatusFailed,
            CreateJsonbPayload(new
            {
                actionKey = job.ActionKey,
                error = errorMessage
            }));
        await InsertWorkflowRuntimeEvent(
            connection,
            transaction,
            job.WorkflowId,
            job.WorkflowNodeInstanceId,
            "automation_failed",
            CreateJsonbPayload(new
            {
                nodeKey = job.NodeKey,
                actionKey = job.ActionKey,
                error = errorMessage
            }));

        await FailRuntimeWorkflow(
            connection,
            transaction,
            job.WorkflowId,
            job.CreatedByUserId,
            errorMessage);

        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task CancelPendingAutomationJobsForNodeInstance(
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

    private static async Task<WorkflowNodeActionRecord?> LoadNextAutomationNodeAction(
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
            InputMapping = reader.IsDBNull(4) ? null : ParseJsonElement(reader.GetString(4)),
            ActionKey = reader.GetString(5),
            ActionName = reader.GetString(6),
            HandlerType = reader.GetString(7),
            IsIdempotent = reader.GetBoolean(8)
        };
    }

    private static async Task<long> CreateAutomationJob(
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

    private static async Task<JsonElement> BuildAutomationJobPayload(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long workflowId,
        JsonElement? inputMapping,
        IReadOnlyDictionary<string, StoredWorkflowAnswerRecord> answersByKey,
        CancellationToken cancellationToken)
    {
        var context = await LoadAutomationPayloadContext(connection, transaction, workflowId, cancellationToken);
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

    private static async Task<AutomationPayloadContextRecord> LoadAutomationPayloadContext(
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

    private sealed class WorkflowNodeActionRecord
    {
        public required long Id { get; init; }
        public required long ActionDefinitionId { get; init; }
        public required int ExecutionOrder { get; init; }
        public required string OnErrorBehavior { get; init; }
        public JsonElement? InputMapping { get; init; }
        public required string ActionKey { get; init; }
        public required string ActionName { get; init; }
        public required string HandlerType { get; init; }
        public required bool IsIdempotent { get; init; }
    }

    private sealed class AutomationPayloadContextRecord
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
}
