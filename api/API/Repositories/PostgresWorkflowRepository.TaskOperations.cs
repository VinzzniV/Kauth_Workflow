using Npgsql;

namespace API;

internal sealed partial class PostgresWorkflowRepository
{
    // Statuswechsel aktualisieren Aufgabe, Abhaengigkeiten und daraus abgeleiteten Workflow-Status in einer Transaktion.
    public async Task<TaskWithWorkflowDto?> UpdateTaskStatus(long taskId, string status)
    {
        var normalizedStatus = NormalizeTaskStatus(status);

        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        var taskRecord = await LoadTaskStateForUpdate(connection, transaction, taskId);
        if (!taskRecord.HasValue)
        {
            return null;
        }

        var (workflowId, currentStatus, _, taskKey) = taskRecord.Value;
        EnsureTaskTransitionAllowed(currentStatus, normalizedStatus);

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
        await SyncPrimaryAssignmentCompletion(connection, transaction, taskId, normalizedStatus);
        await RecalculateWorkflowTaskAvailability(connection, transaction, workflowId);
        await RecalculateAndPersistWorkflowStatus(connection, transaction, workflowId);

        await transaction.CommitAsync();
        return await GetTaskById(taskId);
    }

    // Zuweisungen werden ebenfalls transaktional aktualisiert, damit Aufgaben- und Workflow-Sicht konsistent bleiben.
    public async Task<TaskWithWorkflowDto?> UpdateTaskAssignment(long taskId, TaskAssignRequest request)
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

        var (_, currentStatus, _, _) = taskRecord.Value;
        if (TerminalTaskStatuses.Contains(currentStatus))
        {
            throw new InvalidOperationException("Assignment changes are not allowed for terminal task states.");
        }

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

        await transaction.CommitAsync();
        return await GetTaskById(taskId);
    }
}
