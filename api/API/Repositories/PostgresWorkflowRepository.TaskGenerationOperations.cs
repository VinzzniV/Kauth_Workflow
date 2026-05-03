using Npgsql;

namespace API;

internal sealed partial class PostgresWorkflowRepository
{
    private static async Task<bool> WorkflowHasAnyTasks(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long workflowId)
    {
        const string sql = @"
SELECT EXISTS(
    SELECT 1
    FROM workflow_tasks
    WHERE workflow_id = @workflowId
);";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("workflowId", workflowId);
        return (bool)(await command.ExecuteScalarAsync() ?? false);
    }

    private static async Task BackfillLegacyInitialTasksForSupervisorCompletion(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long workflowId,
        int workflowDepartmentId,
        long actorUserId)
    {
        var generatedTaskCount = await PostgresWorkflowTaskGenerationService.GenerateWorkflowTasksAsync(
            connection,
            transaction,
            workflowId,
            workflowDepartmentId,
            new Dictionary<string, StoredWorkflowAnswerRecord>(StringComparer.OrdinalIgnoreCase),
            TaskGenerationStage.Initial);

        if (generatedTaskCount > 0)
        {
            await PostgresRepositorySharedHelpers.InsertAuditEntry(
                connection,
                transaction,
                workflowId,
                null,
                actorUserId,
                "tasks_generated",
                null,
                null,
                $"{generatedTaskCount} Aufgabe(n) initial erstellt");
        }

        await PostgresWorkflowStatusCalculationService.RecalculateWorkflowTaskAvailabilityAsync(connection, transaction, workflowId);

        const string sql = @"
SELECT id, status
FROM workflow_tasks
WHERE workflow_id = @workflowId
FOR UPDATE;";

        var initialTasks = new List<(long TaskId, string Status)>();
        await using (var command = new NpgsqlCommand(sql, connection, transaction))
        {
            command.Parameters.AddWithValue("workflowId", workflowId);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                initialTasks.Add((reader.GetInt64(0), reader.GetString(1)));
            }
        }

        foreach (var task in initialTasks)
        {
            if (TaskStatusRules.TerminalTaskStatuses.Contains(task.Status))
            {
                continue;
            }

            await PostgresWorkflowStatusCalculationService.PersistTaskStatusAsync(connection, transaction, task.TaskId, "done");
            await PostgresWorkflowStatusCalculationService.SyncPrimaryAssignmentCompletionAsync(connection, transaction, task.TaskId, "done");
        }
    }

    private async Task<bool> CompleteWorkflowTaskByKey(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long workflowId,
        string taskKey,
        long? actorUserId = null)
    {
        const string sql = @"
SELECT id, status, title
FROM workflow_tasks
WHERE workflow_id = @workflowId
  AND task_key = @taskKey
LIMIT 1
FOR UPDATE;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("workflowId", workflowId);
        command.Parameters.AddWithValue("taskKey", taskKey);

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return false;
        }

        var taskId = reader.GetInt64(0);
        var currentStatus = reader.GetString(1);
        var taskTitle = reader.GetString(2);

        if (currentStatus.Equals("done", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (TaskStatusRules.TerminalTaskStatuses.Contains(currentStatus))
        {
            throw new InvalidOperationException($"Task '{taskKey}' can no longer be completed.");
        }

        await reader.DisposeAsync();

        await _statusCalculation.PersistTaskStatus(connection, transaction, taskId, "done");
        await _statusCalculation.SyncPrimaryAssignmentCompletion(connection, transaction, taskId, "done");
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
        return true;
    }
}
