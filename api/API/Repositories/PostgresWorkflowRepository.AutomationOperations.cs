using System.Text.Json;
using Npgsql;

namespace API;

internal sealed partial class PostgresWorkflowRepository
{
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
            command.Parameters.AddWithValue("pendingStatus", PostgresWorkflowAutomationOperations.AutomationJobStatusPending);
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
                    Payload = reader.IsDBNull(15) ? null : PostgresRepositorySharedHelpers.ParseJsonElement(reader.GetString(15)),
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

        var nextAttemptNumber = await PostgresWorkflowAutomationOperations.LoadNextAutomationAttemptNumberAsync(connection, transaction, claimed.JobId, cancellationToken);
        await PostgresWorkflowAutomationOperations.InsertAutomationJobAttemptAsync(connection, transaction, claimed.JobId, nextAttemptNumber, cancellationToken);
        await PostgresWorkflowAutomationOperations.SetAutomationJobStatusAsync(
            connection,
            transaction,
            claimed.JobId,
            PostgresWorkflowAutomationOperations.AutomationJobStatusRunning,
            startedAtUtc: DateTime.UtcNow,
            completedAtUtc: null,
            availableAtUtc: null,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        claimed.AttemptNumber = nextAttemptNumber;
        return claimed;
    }

    public async Task CompleteAutomationJobSuccessInScope(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        ClaimedAutomationJobRecord job,
        WorkflowAutomationHandlerResult result,
        CancellationToken cancellationToken = default)
    {
        await PostgresWorkflowAutomationOperations.CompleteAutomationAttemptAsync(
            connection,
            transaction,
            job.JobId,
            job.AttemptNumber,
            PostgresWorkflowAutomationOperations.AutomationJobStatusSucceeded,
            errorMessage: null,
            cancellationToken);
        await PostgresWorkflowAutomationOperations.SetAutomationJobStatusAsync(
            connection,
            transaction,
            job.JobId,
            PostgresWorkflowAutomationOperations.AutomationJobStatusSucceeded,
            startedAtUtc: null,
            completedAtUtc: DateTime.UtcNow,
            availableAtUtc: null,
            cancellationToken);
        await PostgresWorkflowAutomationOperations.InsertAutomationLogsAsync(connection, transaction, job.JobId, result.Logs, cancellationToken);

        var nextAction = await PostgresWorkflowAutomationOperations.LoadNextAutomationNodeActionAsync(
            connection,
            transaction,
            job.WorkflowNodeId,
            job.ExecutionOrder,
            cancellationToken);
        if (nextAction is not null)
        {
            var answersByKey = await PostgresRepositorySharedHelpers.LoadStoredAnswersByKey(connection, transaction, job.WorkflowId);
            var payload = await PostgresWorkflowAutomationOperations.BuildAutomationJobPayloadAsync(
                connection,
                transaction,
                job.WorkflowId,
                nextAction.InputMapping,
                answersByKey,
                cancellationToken);

            await PostgresWorkflowAutomationOperations.CreateAutomationJobAsync(
                connection,
                transaction,
                job.WorkflowId,
                job.WorkflowNodeInstanceId,
                nextAction.Id,
                nextAction.ActionDefinitionId,
                payload,
                cancellationToken);
            await PostgresWorkflowRuntimeRepository.InsertWorkflowRuntimeEvent(
                connection,
                transaction,
                job.WorkflowId,
                job.WorkflowNodeInstanceId,
                "automation_action_succeeded",
                PostgresWorkflowRuntimeRepository.CreateJsonbPayload(new
                {
                    nodeKey = job.NodeKey,
                    actionKey = job.ActionKey,
                    executionOrder = job.ExecutionOrder
                }));
            return;
        }

        var header = await PostgresWorkflowRuntimeRepository.LoadRuntimeWorkflowHeader(connection, transaction, job.WorkflowUid)
            ?? throw new InvalidOperationException("Runtime workflow for automation job could not be loaded.");
        var graph = await PostgresRepositorySharedHelpers.LoadWorkflowDefinitionGraph(connection, transaction, header.WorkflowDefinitionVersionId);
        if (!graph.NodeById.TryGetValue(job.WorkflowNodeId, out var node))
        {
            throw new InvalidOperationException($"Runtime automation node '{job.NodeKey}' could not be resolved.");
        }

        await PostgresWorkflowRuntimeRepository.UpdateNodeInstanceStatus(
            connection,
            transaction,
            job.WorkflowNodeInstanceId,
            PostgresWorkflowRuntimeRepository.NodeInstanceStatusDone,
            result.Output.HasValue ? JsonSerializer.Serialize(result.Output.Value) : PostgresWorkflowRuntimeRepository.CreateJsonbPayload(new { actionKey = job.ActionKey, succeeded = true }));
        await PostgresWorkflowRuntimeRepository.InsertWorkflowRuntimeEvent(
            connection,
            transaction,
            job.WorkflowId,
            job.WorkflowNodeInstanceId,
            "automation_node_completed",
            PostgresWorkflowRuntimeRepository.CreateJsonbPayload(new { nodeKey = job.NodeKey, actionKey = job.ActionKey }));
        await _auditWrite.InsertAuditEntry(
            connection,
            transaction,
            job.WorkflowId,
            null,
            job.CreatedByUserId,
            "runtime_automation_completed",
            null,
            PostgresWorkflowRuntimeRepository.NodeInstanceStatusDone,
            job.NodeKey);

        await PostgresWorkflowRuntimeRepository.AdvanceRuntimeUntilWaitOrTerminal(
            connection,
            transaction,
            job.WorkflowId,
            graph,
            node,
            await PostgresRepositorySharedHelpers.LoadStoredAnswersByKey(connection, transaction, job.WorkflowId),
            job.CreatedByUserId);
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

        await PostgresWorkflowAutomationOperations.CompleteAutomationAttemptAsync(
            connection,
            transaction,
            job.JobId,
            job.AttemptNumber,
            PostgresWorkflowAutomationOperations.AutomationJobStatusFailed,
            errorMessage,
            cancellationToken);
        await PostgresWorkflowAutomationOperations.InsertAutomationLogsAsync(connection, transaction, job.JobId, logs, cancellationToken);

        if (shouldRetry && retryAvailableAt.HasValue)
        {
            await PostgresWorkflowAutomationOperations.SetAutomationJobStatusAsync(
                connection,
                transaction,
                job.JobId,
                PostgresWorkflowAutomationOperations.AutomationJobStatusPending,
                startedAtUtc: null,
                completedAtUtc: null,
                availableAtUtc: retryAvailableAt.Value,
                cancellationToken);
            await PostgresWorkflowRuntimeRepository.InsertWorkflowRuntimeEvent(
                connection,
                transaction,
                job.WorkflowId,
                job.WorkflowNodeInstanceId,
                "automation_retry_scheduled",
                PostgresWorkflowRuntimeRepository.CreateJsonbPayload(new
                {
                    nodeKey = job.NodeKey,
                    actionKey = job.ActionKey,
                    retryAvailableAt = retryAvailableAt.Value
                }));
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        await PostgresWorkflowAutomationOperations.SetAutomationJobStatusAsync(
            connection,
            transaction,
            job.JobId,
            PostgresWorkflowAutomationOperations.AutomationJobStatusFailed,
            startedAtUtc: null,
            completedAtUtc: DateTime.UtcNow,
            availableAtUtc: null,
            cancellationToken);
        await PostgresWorkflowAutomationOperations.CancelPendingAutomationJobsForNodeInstanceAsync(
            connection,
            transaction,
            job.WorkflowNodeInstanceId,
            cancellationToken);
        await PostgresWorkflowRuntimeRepository.UpdateNodeInstanceStatus(
            connection,
            transaction,
            job.WorkflowNodeInstanceId,
            PostgresWorkflowRuntimeRepository.NodeInstanceStatusFailed,
            PostgresWorkflowRuntimeRepository.CreateJsonbPayload(new
            {
                actionKey = job.ActionKey,
                error = errorMessage
            }));
        await PostgresWorkflowRuntimeRepository.InsertWorkflowRuntimeEvent(
            connection,
            transaction,
            job.WorkflowId,
            job.WorkflowNodeInstanceId,
            "automation_failed",
            PostgresWorkflowRuntimeRepository.CreateJsonbPayload(new
            {
                nodeKey = job.NodeKey,
                actionKey = job.ActionKey,
                error = errorMessage
            }));

        await PostgresWorkflowRuntimeRepository.FailRuntimeWorkflow(
            connection,
            transaction,
            job.WorkflowId,
            job.CreatedByUserId,
            errorMessage);

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task UnclaimAutomationJobAsync(long jobId, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await PostgresWorkflowAutomationOperations.SetAutomationJobStatusAsync(
            connection,
            transaction,
            jobId,
            PostgresWorkflowAutomationOperations.AutomationJobStatusPending,
            startedAtUtc: null,
            completedAtUtc: null,
            availableAtUtc: DateTime.UtcNow,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }
}
