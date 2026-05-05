using Npgsql;

namespace API;

internal sealed partial class PostgresWorkflowRepository
{
    // Rotation-only: WorkflowTaskRef-Pfad besitzt der Lifecycle-Service.
    public async Task<TaskWithWorkflowDto?> UpdateTaskStatusByRef(string taskRef, string status, long actorUserId)
    {
        if (RotationTaskRef.TryParse(taskRef, out _))
        {
            return await _rotationRepository.UpdateRotationTaskStatusByRef(taskRef, status, actorUserId);
        }

        return null;
    }

    public async Task<TaskWithWorkflowDto?> DecideTaskApprovalByRef(
        string taskRef,
        TaskApprovalDecisionRequest request,
        long actorUserId)
    {
        if (RotationTaskRef.TryParse(taskRef, out _))
        {
            return await _rotationRepository.DecideRotationTaskApprovalByRef(taskRef, request, actorUserId);
        }

        return null;
    }

    public async Task<TaskWithWorkflowDto?> UpdateTaskAssignmentByRef(
        string taskRef,
        TaskAssignRequest request,
        long actorUserId)
    {
        if (WorkflowTaskRef.TryParse(taskRef, out var workflowTaskId))
        {
            return await UpdateTaskAssignment(workflowTaskId, request, actorUserId);
        }

        if (RotationTaskRef.TryParse(taskRef, out _))
        {
            return await _rotationRepository.UpdateRotationTaskAssignmentByRef(taskRef, request, actorUserId);
        }

        return null;
    }

    public async Task<TaskWithWorkflowDto?> AddTaskCommentByRef(string taskRef, string commentText, long actorUserId)
    {
        if (WorkflowTaskRef.TryParse(taskRef, out var workflowTaskId))
        {
            return await AddTaskComment(workflowTaskId, commentText, actorUserId);
        }

        if (RotationTaskRef.TryParse(taskRef, out _))
        {
            return await _rotationRepository.AddRotationTaskCommentByRef(taskRef, commentText, actorUserId);
        }

        return null;
    }

    public async Task<TaskStatusUpdateScopeResult?> UpdateTaskStatusInScope(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long taskId,
        string normalizedStatus,
        long actorUserId)
    {
        if (!await _statusCalculation.TryLockWorkflowForTaskStatusUpdate(connection, transaction, taskId))
            return null;

        var taskRecord = await _statusCalculation.LoadTaskStateForUpdate(connection, transaction, taskId);
        if (!taskRecord.HasValue)
            return null;

        var (workflowId, workflowUid, currentStatus, _, taskKey, isApprovalTask, taskTitle, nodeInstanceId, isRuntimeNodeTask) = taskRecord.Value;
        TaskStatusRules.EnsureTaskTransitionAllowed(currentStatus, normalizedStatus);

        if (currentStatus.Equals(normalizedStatus, StringComparison.OrdinalIgnoreCase))
            return new TaskStatusUpdateScopeResult(false, null, workflowId, workflowUid, false);

        if (isApprovalTask && TaskStatusRules.TerminalTaskStatuses.Contains(normalizedStatus))
        {
            throw new InvalidOperationException(
                "Die Anforderungen der Abteilungsleitung muessen ueber den Schritt der Abteilungsleitung abgeschlossen werden.");
        }

        if (TaskStatusRules.RequiresSatisfiedDependencies(normalizedStatus)
            && !await _statusCalculation.AreTaskDependenciesSatisfied(connection, transaction, taskId))
        {
            throw new InvalidOperationException("Task dependencies are not satisfied for the requested status.");
        }

        await _statusCalculation.PersistTaskStatus(connection, transaction, taskId, normalizedStatus);
        await _auditWrite.InsertAuditEntry(
            connection,
            transaction,
            workflowId,
            taskId,
            actorUserId,
            "task_status_changed",
            currentStatus,
            normalizedStatus,
            PostgresRepositorySharedHelpers.BuildTaskStatusAuditDetail(taskTitle));
        await _statusCalculation.SyncPrimaryAssignmentCompletion(connection, transaction, taskId, normalizedStatus);

        if (isRuntimeNodeTask)
        {
            var shouldComplete = nodeInstanceId.HasValue && TaskStatusRules.TerminalTaskStatuses.Contains(normalizedStatus);
            return new TaskStatusUpdateScopeResult(shouldComplete, nodeInstanceId, workflowId, workflowUid, false);
        }
        else
        {
            await _statusCalculation.RecalculateWorkflowTaskAvailability(connection, transaction, workflowId);
            await _statusCalculation.RecalculateAndPersistWorkflowStatus(connection, transaction, workflowId, actorUserId);
            return new TaskStatusUpdateScopeResult(false, null, workflowId, workflowUid, true);
        }
    }

    public async Task<DecideTaskApprovalScopeResult?> DecideTaskApprovalInScope(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long taskId,
        TaskApprovalDecisionRequest request,
        long actorUserId)
    {
        if (!await _statusCalculation.TryLockWorkflowForTaskStatusUpdate(connection, transaction, taskId))
            return null;

        var taskRecord = await _statusCalculation.LoadTaskStateForUpdate(connection, transaction, taskId);
        if (!taskRecord.HasValue)
            return null;

        var (workflowId, workflowUid, currentStatus, _, taskKey, isApprovalTask, taskTitle, nodeInstanceId, isRuntimeNodeTask) = taskRecord.Value;
        if (!isApprovalTask)
            throw new InvalidOperationException($"Task '{taskKey}' is not an approval task.");

        if (!isRuntimeNodeTask || !nodeInstanceId.HasValue)
            throw new InvalidOperationException(
                "Die Anforderungen der Abteilungsleitung muessen ueber den Schritt der Abteilungsleitung abgeschlossen werden.");

        if (TaskStatusRules.TerminalTaskStatuses.Contains(currentStatus))
            throw new InvalidOperationException("Approval decisions are not allowed for terminal task states.");

        var normalizedComment = string.IsNullOrWhiteSpace(request.CommentText)
            ? null
            : NormalizeTaskComment(request.CommentText);

        await _statusCalculation.PersistTaskStatus(connection, transaction, taskId, "done");
        await _auditWrite.InsertAuditEntry(
            connection,
            transaction,
            workflowId,
            taskId,
            actorUserId,
            "task_status_changed",
            currentStatus,
            "done",
            PostgresRepositorySharedHelpers.BuildTaskStatusAuditDetail(taskTitle));
        await _statusCalculation.SyncPrimaryAssignmentCompletion(connection, transaction, taskId, "done");

        if (normalizedComment is not null)
            await InsertTaskComment(connection, transaction, taskId, workflowId, normalizedComment, actorUserId);

        return new DecideTaskApprovalScopeResult(nodeInstanceId.Value, workflowId, workflowUid);
    }

    public Task CompleteRuntimeTaskNodeInScope(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long workflowId,
        Guid workflowUid,
        long nodeInstanceId,
        long actorUserId,
        string? comment = null)
        => PostgresWorkflowRuntimeRepository.CompleteTaskNodeRuntimeSide(
            connection,
            transaction,
            workflowId,
            workflowUid,
            nodeInstanceId,
            actorUserId,
            comment);

    public Task TryAdvanceRuntimeSetupInScope(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long workflowId,
        Guid workflowUid,
        long actorUserId)
        => PostgresWorkflowRuntimeRepository.TryAdvanceSetupNodeIfReady(
            connection,
            transaction,
            workflowId,
            workflowUid,
            actorUserId);

    public Task ApplyApprovalNodeDecisionInScope(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long workflowId,
        Guid workflowUid,
        long nodeInstanceId,
        bool approved,
        long actorUserId)
        => PostgresWorkflowRuntimeRepository.ApplyApprovalNodeDecision(
            connection,
            transaction,
            workflowId,
            workflowUid,
            nodeInstanceId,
            approved,
            actorUserId);

    // Zuweisungen werden ebenfalls transaktional aktualisiert, damit Aufgaben- und Workflow-Sicht konsistent bleiben.
    public async Task<TaskWithWorkflowDto?> UpdateTaskAssignment(long taskId, TaskAssignRequest request, long actorUserId)
    {
        if (request.AssigneeUserId is null && request.AssigneeResponsibilityId is null)
        {
            throw new InvalidOperationException("Either assigneeUserId or assigneeResponsibilityId must be provided.");
        }

        if (request.AssigneeUserId is <= 0)
        {
            throw new InvalidOperationException("assigneeUserId must be greater than zero.");
        }

        if (request.AssigneeResponsibilityId is <= 0)
        {
            throw new InvalidOperationException("assigneeResponsibilityId must be greater than zero.");
        }

        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        var taskRecord = await _statusCalculation.LoadTaskStateForUpdate(connection, transaction, taskId);
        if (!taskRecord.HasValue)
        {
            return null;
        }

        var (workflowId, _, currentStatus, _, _, _, taskTitle, _, _) = taskRecord.Value;
        if (TaskStatusRules.TerminalTaskStatuses.Contains(currentStatus))
        {
            throw new InvalidOperationException("Assignment changes are not allowed for terminal task states.");
        }

        var oldAssigneeLabel = await PostgresRepositorySharedHelpers.LoadPrimaryTaskAssignmentAuditLabel(connection, transaction, taskId);

        if (request.AssigneeResponsibilityId.HasValue)
        {
            await PostgresRepositorySharedHelpers.EnsureAssignableResponsibilityExists(connection, transaction, request.AssigneeResponsibilityId.Value);
        }

        if (request.AssigneeUserId.HasValue)
        {
            await PostgresRepositorySharedHelpers.EnsureAssignableUserExists(connection, transaction, request.AssigneeUserId.Value);
        }

        if (request.AssigneeUserId.HasValue && request.AssigneeResponsibilityId.HasValue)
        {
            await PostgresRepositorySharedHelpers.EnsureUserHasResponsibility(
                connection,
                transaction,
                request.AssigneeUserId.Value,
                request.AssigneeResponsibilityId.Value);
        }

        var assignmentType = request.AssigneeUserId.HasValue ? "user" : "responsibility";
        var storedAssigneeUserId = request.AssigneeUserId;
        var storedAssigneeResponsibilityId = assignmentType == "responsibility"
            ? request.AssigneeResponsibilityId
            : null;
        var newAssigneeLabel = assignmentType == "user"
            ? await PostgresRepositorySharedHelpers.LoadAssigneeUserAuditLabel(connection, transaction, request.AssigneeUserId!.Value)
            : await PostgresRepositorySharedHelpers.LoadAssigneeResponsibilityAuditLabel(connection, transaction, request.AssigneeResponsibilityId!.Value);

        const string clearPrimarySql = @"
UPDATE task_assignments
SET is_primary = FALSE
WHERE workflow_task_id = @taskId
  AND is_primary = TRUE;";

        await using (var clearPrimaryCommand = new NpgsqlCommand(clearPrimarySql, connection, transaction))
        {
            clearPrimaryCommand.Parameters.AddWithValue("taskId", taskId);
            await clearPrimaryCommand.ExecuteNonQueryAsync();
        }

        const string insertAssignmentSql = @"
INSERT INTO task_assignments (
    workflow_task_id,
    assignee_user_id,
    assignee_responsibility_id,
    assignment_type,
    is_primary,
    assigned_at,
    completed_at
)
VALUES (
    @taskId,
    @assigneeUserId,
    @assigneeResponsibilityId,
    @assignmentType,
    TRUE,
    NOW(),
    NULL
);";

        await using (var insertAssignmentCommand = new NpgsqlCommand(insertAssignmentSql, connection, transaction))
        {
            insertAssignmentCommand.Parameters.AddWithValue("taskId", taskId);
            insertAssignmentCommand.Parameters.AddWithValue("assigneeUserId", (object?)storedAssigneeUserId ?? DBNull.Value);
            insertAssignmentCommand.Parameters.AddWithValue(
                "assigneeResponsibilityId",
                (object?)storedAssigneeResponsibilityId ?? DBNull.Value);
            insertAssignmentCommand.Parameters.AddWithValue(
                "assignmentType",
                assignmentType);
            await insertAssignmentCommand.ExecuteNonQueryAsync();
        }

        await _auditWrite.InsertAuditEntry(
            connection,
            transaction,
            workflowId,
            taskId,
            actorUserId,
            "task_assigned",
            oldAssigneeLabel,
            newAssigneeLabel,
            PostgresRepositorySharedHelpers.BuildTaskStatusAuditDetail(taskTitle));

        await transaction.CommitAsync();
        return await GetTaskById(taskId);
    }

    public async Task<TaskWithWorkflowDto?> AddTaskComment(long taskId, string commentText, long actorUserId)
    {
        var normalizedComment = NormalizeTaskComment(commentText);

        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        if (!await _statusCalculation.TryLockWorkflowForTaskStatusUpdate(connection, transaction, taskId))
        {
            return null;
        }

        var taskRecord = await _statusCalculation.LoadTaskStateForUpdate(connection, transaction, taskId);
        if (!taskRecord.HasValue)
        {
            return null;
        }

        var (workflowId, _, currentStatus, _, _, _, _, _, _) = taskRecord.Value;
        var workflowStatus = await LoadWorkflowStatusForUpdate(connection, transaction, workflowId);
        if (workflowStatus is null)
        {
            return null;
        }

        if (WorkflowStatusRules.IsTerminal(workflowStatus))
        {
            throw new InvalidOperationException("Kommentare sind fuer abgeschlossene Workflows nicht mehr erlaubt.");
        }

        if (TaskStatusRules.TerminalTaskStatuses.Contains(currentStatus))
        {
            throw new InvalidOperationException("Kommentare sind fuer beendete Aufgaben nicht mehr erlaubt.");
        }

        await InsertTaskComment(connection, transaction, taskId, workflowId, normalizedComment, actorUserId);

        await transaction.CommitAsync();
        return await GetTaskById(taskId);
    }

    private static string NormalizeTaskComment(string commentText)
    {
        if (string.IsNullOrWhiteSpace(commentText))
        {
            throw new InvalidOperationException("Kommentar darf nicht leer sein.");
        }

        var normalized = commentText.Trim();
        if (normalized.Length > 2000)
        {
            throw new InvalidOperationException("Kommentar darf maximal 2000 Zeichen haben.");
        }

        return normalized;
    }

    private static async Task InsertTaskComment(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long taskId,
        long workflowId,
        string normalizedComment,
        long actorUserId)
    {
        const string sql = @"
INSERT INTO workflow_task_comments (
    workflow_task_id,
    author_user_id,
    comment_text
)
VALUES (
    @taskId,
    @actorUserId,
    @commentText
);";

        await using (var command = new NpgsqlCommand(sql, connection, transaction))
        {
            command.Parameters.AddWithValue("taskId", taskId);
            command.Parameters.AddWithValue("actorUserId", actorUserId);
            command.Parameters.AddWithValue("commentText", normalizedComment);
            await command.ExecuteNonQueryAsync();
        }

        await PostgresRepositorySharedHelpers.InsertAuditEntry(
            connection,
            transaction,
            workflowId,
            taskId,
            actorUserId,
            "task_comment_added",
            null,
            null,
            normalizedComment);
    }

    internal static async Task<string?> LoadWorkflowStatusForUpdate(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long workflowId)
    {
        const string sql = @"
SELECT status
FROM workflows
WHERE id = @workflowId
FOR UPDATE;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("workflowId", workflowId);
        return await command.ExecuteScalarAsync() as string;
    }
}
