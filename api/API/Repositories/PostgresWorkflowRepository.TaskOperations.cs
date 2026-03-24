using Npgsql;

namespace API;

internal sealed partial class PostgresWorkflowRepository
{
    // Statuswechsel aktualisieren Aufgabe, Abhaengigkeiten und daraus abgeleiteten Workflow-Status in einer Transaktion.
    public async Task<TaskWithWorkflowDto?> UpdateTaskStatus(long taskId, string status, long actorUserId)
    {
        var normalizedStatus = NormalizeTaskStatus(status);

        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        if (!await TryLockWorkflowForTaskStatusUpdate(connection, transaction, taskId))
        {
            return null;
        }

        var taskRecord = await LoadTaskStateForUpdate(connection, transaction, taskId);
        if (!taskRecord.HasValue)
        {
            return null;
        }

        var (workflowId, currentStatus, _, taskKey, taskTitle) = taskRecord.Value;
        EnsureTaskTransitionAllowed(currentStatus, normalizedStatus);

        if (currentStatus.Equals(normalizedStatus, StringComparison.OrdinalIgnoreCase))
        {
            await transaction.CommitAsync();
            return await GetTaskById(taskId);
        }

        if (taskKey.Equals(SupervisorRequirementTaskKey, StringComparison.OrdinalIgnoreCase)
            && TerminalTaskStatuses.Contains(normalizedStatus))
        {
            throw new InvalidOperationException(
                "Die Anforderungen der Abteilungsleitung muessen ueber den Schritt der Abteilungsleitung abgeschlossen werden.");
        }

        if (RequiresSatisfiedDependencies(normalizedStatus)
            && !await AreTaskDependenciesSatisfied(connection, transaction, taskId))
        {
            throw new InvalidOperationException("Task dependencies are not satisfied for the requested status.");
        }

        await PersistTaskStatus(connection, transaction, taskId, normalizedStatus);
        await InsertAuditEntry(
            connection,
            transaction,
            workflowId,
            taskId,
            actorUserId,
            "task_status_changed",
            currentStatus,
            normalizedStatus,
            BuildTaskStatusAuditDetail(taskTitle));
        await SyncPrimaryAssignmentCompletion(connection, transaction, taskId, normalizedStatus);
        await RecalculateWorkflowTaskAvailability(connection, transaction, workflowId);
        await RecalculateAndPersistWorkflowStatus(connection, transaction, workflowId, actorUserId);

        await transaction.CommitAsync();
        return await GetTaskById(taskId);
    }

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

        var taskRecord = await LoadTaskStateForUpdate(connection, transaction, taskId);
        if (!taskRecord.HasValue)
        {
            return null;
        }

        var (workflowId, currentStatus, _, _, taskTitle) = taskRecord.Value;
        if (TerminalTaskStatuses.Contains(currentStatus))
        {
            throw new InvalidOperationException("Assignment changes are not allowed for terminal task states.");
        }

        var oldAssigneeLabel = await LoadPrimaryTaskAssignmentAuditLabel(connection, transaction, taskId);

        if (request.AssigneeResponsibilityId.HasValue)
        {
            await EnsureAssignableResponsibilityExists(connection, transaction, request.AssigneeResponsibilityId.Value);
        }

        if (request.AssigneeUserId.HasValue)
        {
            await EnsureAssignableUserExists(connection, transaction, request.AssigneeUserId.Value);
        }

        if (request.AssigneeUserId.HasValue && request.AssigneeResponsibilityId.HasValue)
        {
            await EnsureUserHasResponsibility(
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
            ? await LoadAssigneeUserAuditLabel(connection, transaction, request.AssigneeUserId!.Value)
            : await LoadAssigneeResponsibilityAuditLabel(connection, transaction, request.AssigneeResponsibilityId!.Value);

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

        await InsertAuditEntry(
            connection,
            transaction,
            workflowId,
            taskId,
            actorUserId,
            "task_assigned",
            oldAssigneeLabel,
            newAssigneeLabel,
            BuildTaskStatusAuditDetail(taskTitle));

        await transaction.CommitAsync();
        return await GetTaskById(taskId);
    }

    public async Task<TaskWithWorkflowDto?> AddTaskComment(long taskId, string commentText, long actorUserId)
    {
        var normalizedComment = NormalizeTaskComment(commentText);

        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        if (!await TryLockWorkflowForTaskStatusUpdate(connection, transaction, taskId))
        {
            return null;
        }

        var taskRecord = await LoadTaskStateForUpdate(connection, transaction, taskId);
        if (!taskRecord.HasValue)
        {
            return null;
        }

        var (workflowId, currentStatus, _, _, _) = taskRecord.Value;
        var workflowStatus = await LoadWorkflowStatusForUpdate(connection, transaction, workflowId);
        if (workflowStatus is null)
        {
            return null;
        }

        if (WorkflowStatusRules.IsTerminal(workflowStatus))
        {
            throw new InvalidOperationException("Kommentare sind fuer abgeschlossene Workflows nicht mehr erlaubt.");
        }

        if (TerminalTaskStatuses.Contains(currentStatus))
        {
            throw new InvalidOperationException("Kommentare sind fuer beendete Aufgaben nicht mehr erlaubt.");
        }

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

        await InsertAuditEntry(
            connection,
            transaction,
            workflowId,
            taskId,
            actorUserId,
            "task_comment_added",
            null,
            null,
            normalizedComment);

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

    private static async Task<string?> LoadWorkflowStatusForUpdate(
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
