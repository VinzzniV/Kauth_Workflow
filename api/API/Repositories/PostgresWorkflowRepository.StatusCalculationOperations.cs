using Npgsql;

namespace API;

internal sealed partial class PostgresWorkflowRepository
{
    private static string NormalizeTaskStatus(string status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            throw new InvalidOperationException("Status is required.");
        }

        var normalizedStatus = status.Trim().ToLowerInvariant();
        if (!AllowedTaskStatuses.Contains(normalizedStatus))
        {
            throw new InvalidOperationException($"Task status '{status}' is invalid.");
        }

        return normalizedStatus;
    }

    private static void EnsureTaskTransitionAllowed(string currentStatus, string requestedStatus)
    {
        if (currentStatus.Equals(requestedStatus, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!AllowedTaskTransitions.TryGetValue(currentStatus, out var allowedTransitions))
        {
            throw new InvalidOperationException($"Current task status '{currentStatus}' is not supported.");
        }

        if (!allowedTransitions.Contains(requestedStatus))
        {
            throw new InvalidOperationException(
                $"Task transition from '{currentStatus}' to '{requestedStatus}' is not allowed.");
        }
    }

    private static bool RequiresSatisfiedDependencies(string requestedStatus)
    {
        return requestedStatus.Equals("ready", StringComparison.OrdinalIgnoreCase)
               || requestedStatus.Equals("in_progress", StringComparison.OrdinalIgnoreCase)
               || requestedStatus.Equals("done", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<bool> TryLockWorkflowForTaskStatusUpdate(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long taskId)
    {
        const string sql = @"
SELECT 1
FROM workflow_tasks wt
JOIN workflows w ON w.id = wt.workflow_id
WHERE wt.id = @taskId
LIMIT 1
FOR UPDATE OF w;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("taskId", taskId);
        return await command.ExecuteScalarAsync() is not null;
    }

    private static async Task<(long WorkflowId, string CurrentStatus, bool IsRequired, string TaskKey)?> LoadTaskStateForUpdate(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long taskId)
    {
        const string sql = @"
SELECT workflow_id, status, is_required, task_key
FROM workflow_tasks
WHERE id = @taskId
FOR UPDATE;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("taskId", taskId);

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }

        return (reader.GetInt64(0), reader.GetString(1), reader.GetBoolean(2), reader.GetString(3));
    }

    private static async Task<bool> AreTaskDependenciesSatisfied(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long taskId)
    {
        const string sql = @"
SELECT
    COUNT(*) AS total_count,
    COUNT(*) FILTER (WHERE dep.status = d.required_status) AS satisfied_count
FROM workflow_task_dependencies d
JOIN workflow_tasks dep ON dep.id = d.depends_on_workflow_task_id
WHERE d.workflow_task_id = @taskId
  AND dep.task_key <> @legacyTaskKey;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("taskId", taskId);
        command.Parameters.AddWithValue("legacyTaskKey", LegacySupervisorHandoverTaskKey);

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return true;
        }

        var totalCount = reader.GetInt64(0);
        var satisfiedCount = reader.GetInt64(1);
        return totalCount == satisfiedCount;
    }

    private static async Task PersistTaskStatus(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long taskId,
        string nextStatus)
    {
        const string sql = @"
UPDATE workflow_tasks
SET
    status = @status,
    ready_at = CASE
        WHEN @status = 'ready' THEN COALESCE(ready_at, NOW())
        WHEN @status IN ('in_progress', 'done') THEN COALESCE(ready_at, NOW())
        ELSE ready_at
    END,
    started_at = CASE
        WHEN @status IN ('in_progress', 'done') THEN COALESCE(started_at, NOW())
        WHEN @status IN ('open', 'ready', 'blocked') THEN NULL
        ELSE started_at
    END,
    completed_at = CASE
        WHEN @status = 'done' THEN COALESCE(completed_at, NOW())
        WHEN @status IN ('open', 'ready', 'in_progress', 'blocked', 'skipped', 'cancelled') THEN NULL
        ELSE completed_at
    END,
    cancelled_at = CASE
        WHEN @status IN ('cancelled', 'skipped') THEN COALESCE(cancelled_at, NOW())
        WHEN @status IN ('open', 'ready', 'in_progress', 'blocked', 'done') THEN NULL
        ELSE cancelled_at
    END
WHERE id = @taskId;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("status", nextStatus);
        command.Parameters.AddWithValue("taskId", taskId);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task SyncPrimaryAssignmentCompletion(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long taskId,
        string taskStatus)
    {
        if (!TerminalTaskStatuses.Contains(taskStatus))
        {
            return;
        }

        const string sql = @"
UPDATE task_assignments
SET completed_at = COALESCE(completed_at, NOW())
WHERE workflow_task_id = @taskId
  AND is_primary = TRUE;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("taskId", taskId);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task RecalculateWorkflowTaskAvailability(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long workflowId)
    {
        const string taskSql = @"
SELECT id, status
FROM workflow_tasks
WHERE workflow_id = @workflowId
ORDER BY id
FOR UPDATE;";

        var tasks = new Dictionary<long, string>();
        await using (var taskCommand = new NpgsqlCommand(taskSql, connection, transaction))
        {
            taskCommand.Parameters.AddWithValue("workflowId", workflowId);
            await using var reader = await taskCommand.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                tasks[reader.GetInt64(0)] = reader.GetString(1);
            }
        }

        if (tasks.Count == 0)
        {
            return;
        }

        const string dependencySql = @"
SELECT
    d.workflow_task_id,
    d.required_status,
    dep.status,
    dep.task_key
FROM workflow_task_dependencies d
JOIN workflow_tasks target ON target.id = d.workflow_task_id
JOIN workflow_tasks dep ON dep.id = d.depends_on_workflow_task_id
WHERE target.workflow_id = @workflowId
ORDER BY d.workflow_task_id, d.id;";

        var dependenciesByTask = new Dictionary<long, List<(string RequiredStatus, string DependencyStatus)>>();
        await using (var dependencyCommand = new NpgsqlCommand(dependencySql, connection, transaction))
        {
            dependencyCommand.Parameters.AddWithValue("workflowId", workflowId);
            await using var reader = await dependencyCommand.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                if (reader.GetString(3).Equals(LegacySupervisorHandoverTaskKey, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var workflowTaskId = reader.GetInt64(0);
                if (!dependenciesByTask.TryGetValue(workflowTaskId, out var dependencies))
                {
                    dependencies = new List<(string RequiredStatus, string DependencyStatus)>();
                    dependenciesByTask[workflowTaskId] = dependencies;
                }

                dependencies.Add((reader.GetString(1), reader.GetString(2)));
            }
        }

        foreach (var taskEntry in tasks.ToList())
        {
            var taskId = taskEntry.Key;
            var currentStatus = taskEntry.Value;

            if (TerminalTaskStatuses.Contains(currentStatus))
            {
                continue;
            }

            dependenciesByTask.TryGetValue(taskId, out var dependencies);
            var hasUnsatisfiedDependencies = dependencies is not null
                && dependencies.Any(dependency => !dependency.DependencyStatus.Equals(dependency.RequiredStatus, StringComparison.OrdinalIgnoreCase));

            if (!hasUnsatisfiedDependencies
                && (currentStatus.Equals("blocked", StringComparison.OrdinalIgnoreCase)
                    || currentStatus.Equals("open", StringComparison.OrdinalIgnoreCase)))
            {
                const string markReadySql = @"
UPDATE workflow_tasks
SET
    status = 'ready',
    ready_at = NOW()
WHERE id = @taskId;";

                await using var command = new NpgsqlCommand(markReadySql, connection, transaction);
                command.Parameters.AddWithValue("taskId", taskId);
                await command.ExecuteNonQueryAsync();
                tasks[taskId] = "ready";
                continue;
            }

            if (hasUnsatisfiedDependencies
                && (currentStatus.Equals("ready", StringComparison.OrdinalIgnoreCase)
                    || currentStatus.Equals("in_progress", StringComparison.OrdinalIgnoreCase)
                    || currentStatus.Equals("open", StringComparison.OrdinalIgnoreCase)))
            {
                const string markBlockedSql = @"
UPDATE workflow_tasks
SET status = 'blocked'
WHERE id = @taskId;";

                await using var command = new NpgsqlCommand(markBlockedSql, connection, transaction);
                command.Parameters.AddWithValue("taskId", taskId);
                await command.ExecuteNonQueryAsync();
                tasks[taskId] = "blocked";
            }
        }
    }

    private static async Task RecalculateAndPersistWorkflowStatus(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long workflowId)
    {
        const string taskStatusSql = @"
SELECT task_key, status, is_required
FROM workflow_tasks
WHERE workflow_id = @workflowId;";

        var taskStates = new List<(string TaskKey, string Status, bool IsRequired)>();
        await using (var command = new NpgsqlCommand(taskStatusSql, connection, transaction))
        {
            command.Parameters.AddWithValue("workflowId", workflowId);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                taskStates.Add((reader.GetString(0), reader.GetString(1), reader.GetBoolean(2)));
            }
        }

        var completionRelevantStatuses = taskStates
            .Select(task => task.Status)
            .ToList();

        var nextWorkflowStatus = taskStates.Count == 0
            ? "draft"
            : completionRelevantStatuses.All(status =>
                    status.Equals("done", StringComparison.OrdinalIgnoreCase)
                    || status.Equals("skipped", StringComparison.OrdinalIgnoreCase))
                ? "completed"
                : DetermineActiveWorkflowStatus(taskStates);

        const string workflowStatusUpdateSql = @"
UPDATE workflows
SET
    status = @status,
    started_at = CASE
        WHEN @status IN ('waiting_for_supervisor', 'waiting_for_department', 'in_progress', 'completed', 'cancelled')
            THEN COALESCE(started_at, NOW())
        ELSE started_at
    END,
    completed_at = CASE
        WHEN @status = 'completed' THEN COALESCE(completed_at, NOW())
        ELSE NULL
    END,
    cancelled_at = CASE
        WHEN @status = 'cancelled' THEN COALESCE(cancelled_at, NOW())
        ELSE NULL
    END
WHERE id = @workflowId;";

        await using var updateCommand = new NpgsqlCommand(workflowStatusUpdateSql, connection, transaction);
        updateCommand.Parameters.AddWithValue("status", nextWorkflowStatus);
        updateCommand.Parameters.AddWithValue("workflowId", workflowId);
        await updateCommand.ExecuteNonQueryAsync();
    }

    private static string DetermineActiveWorkflowStatus(
        IReadOnlyList<(string TaskKey, string Status, bool IsRequired)> taskStates)
    {
        var supervisorTask = taskStates.FirstOrDefault(task =>
            task.TaskKey.Equals(SupervisorRequirementTaskKey, StringComparison.OrdinalIgnoreCase));
        var departmentTasks = taskStates
            .Where(task => !task.TaskKey.Equals(SupervisorRequirementTaskKey, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var hasDepartmentTasksInProgress = departmentTasks.Any(task =>
            task.Status.Equals("in_progress", StringComparison.OrdinalIgnoreCase));
        var hasActiveDepartmentTasks = departmentTasks.Any(task =>
            task.Status.Equals("open", StringComparison.OrdinalIgnoreCase)
            || task.Status.Equals("ready", StringComparison.OrdinalIgnoreCase)
            || task.Status.Equals("blocked", StringComparison.OrdinalIgnoreCase)
            || task.Status.Equals("in_progress", StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(supervisorTask.TaskKey))
        {
            if (supervisorTask.Status.Equals("ready", StringComparison.OrdinalIgnoreCase)
                || supervisorTask.Status.Equals("in_progress", StringComparison.OrdinalIgnoreCase))
            {
                return "waiting_for_supervisor";
            }

            if (supervisorTask.Status.Equals("done", StringComparison.OrdinalIgnoreCase))
            {
                return hasDepartmentTasksInProgress ? "in_progress" : "waiting_for_department";
            }

            return "draft";
        }

        if (hasDepartmentTasksInProgress)
        {
            return "in_progress";
        }

        return hasActiveDepartmentTasks ? "waiting_for_department" : "draft";
    }
}
