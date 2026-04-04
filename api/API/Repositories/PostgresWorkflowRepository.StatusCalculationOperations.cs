using Npgsql;

namespace API;

internal sealed partial class PostgresWorkflowRepository
{
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

    private static async Task<(long WorkflowId, string CurrentStatus, bool IsRequired, string TaskKey, bool IsApprovalTask, string TaskTitle)?> LoadTaskStateForUpdate(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long taskId)
    {
        const string sql = @"
SELECT
    wt.workflow_id,
    wt.status,
    wt.is_required,
    wt.task_key,
    CASE
        WHEN pt.approval_task_template_key IS NULL THEN FALSE
        ELSE wt.task_key = pt.approval_task_template_key
    END,
    wt.title
FROM workflow_tasks wt
JOIN workflows w ON w.id = wt.workflow_id
JOIN process_types pt ON pt.id = w.process_type_id
WHERE wt.id = @taskId
FOR UPDATE OF wt;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("taskId", taskId);

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }

        return (
            reader.GetInt64(0),
            reader.GetString(1),
            reader.GetBoolean(2),
            reader.GetString(3),
            reader.GetBoolean(4),
            reader.GetString(5));
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
WHERE d.workflow_task_id = @taskId;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("taskId", taskId);

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
    due_at = COALESCE(
        due_at,
        CASE
            WHEN due_in_days IS NOT NULL
                AND (SELECT w.deadline_date FROM workflows w WHERE w.id = workflow_tasks.workflow_id) IS NOT NULL
                THEN LEAST(
                    (SELECT w.deadline_date::timestamp AT TIME ZONE 'UTC'
                     FROM workflows w
                     WHERE w.id = workflow_tasks.workflow_id),
                    created_at + (due_in_days * INTERVAL '1 day'))
            WHEN due_in_days IS NOT NULL THEN created_at + (due_in_days * INTERVAL '1 day')
            ELSE (
                SELECT w.deadline_date::timestamp AT TIME ZONE 'UTC'
                FROM workflows w
                WHERE w.id = workflow_tasks.workflow_id)
        END),
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
        WHEN @status IN ('open', 'ready', 'in_progress', 'blocked') THEN NULL
        ELSE completed_at
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
        if (!TaskStatusRules.TerminalTaskStatuses.Contains(taskStatus))
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

            if (TaskStatusRules.TerminalTaskStatuses.Contains(currentStatus))
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
    due_at = COALESCE(
        due_at,
        CASE
            WHEN due_in_days IS NOT NULL
                AND (SELECT w.deadline_date FROM workflows w WHERE w.id = workflow_tasks.workflow_id) IS NOT NULL
                THEN LEAST(
                    (SELECT w.deadline_date::timestamp AT TIME ZONE 'UTC'
                     FROM workflows w
                     WHERE w.id = workflow_tasks.workflow_id),
                    created_at + (due_in_days * INTERVAL '1 day'))
            WHEN due_in_days IS NOT NULL THEN created_at + (due_in_days * INTERVAL '1 day')
            ELSE (
                SELECT w.deadline_date::timestamp AT TIME ZONE 'UTC'
                FROM workflows w
                WHERE w.id = workflow_tasks.workflow_id)
        END),
    ready_at = NOW()
WHERE id = @taskId;";

                await using var command = new NpgsqlCommand(markReadySql, connection, transaction);
                command.Parameters.AddWithValue("taskId", taskId);
                await command.ExecuteNonQueryAsync();
                tasks[taskId] = "ready";
                continue;
            }

            if (hasUnsatisfiedDependencies && TaskStatusRules.CanAutoBlockTask(currentStatus))
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

    private async Task RecalculateAndPersistWorkflowStatus(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long workflowId,
        long? actorUserId)
    {
        const string workflowStatusContextSql = @"
SELECT
    w.status,
    pt.name,
    pt.requires_supervisor_step,
    pt.approval_task_template_key
FROM workflows w
JOIN process_types pt ON pt.id = w.process_type_id
WHERE w.id = @workflowId
FOR UPDATE OF w;";

        string? currentWorkflowStatus = null;
        string processTypeName = "Workflow";
        var requiresSupervisorStep = true;
        string? approvalTaskTemplateKey = null;
        await using (var workflowStatusContextCommand = new NpgsqlCommand(workflowStatusContextSql, connection, transaction))
        {
            workflowStatusContextCommand.Parameters.AddWithValue("workflowId", workflowId);
            await using var reader = await workflowStatusContextCommand.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                currentWorkflowStatus = reader.IsDBNull(0) ? null : reader.GetString(0);
                processTypeName = reader.GetString(1);
                requiresSupervisorStep = reader.GetBoolean(2);
                approvalTaskTemplateKey = WorkflowStatusRules.EnsureApprovalTaskConfiguration(
                    processTypeName,
                    requiresSupervisorStep,
                    reader.IsDBNull(3) ? null : reader.GetString(3));
            }
        }

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
                    status.Equals("done", StringComparison.OrdinalIgnoreCase))
                ? "completed"
                : WorkflowStatusRules.DetermineActiveWorkflowStatus(taskStates, processTypeName, requiresSupervisorStep, approvalTaskTemplateKey);

        const string workflowStatusUpdateSql = @"
UPDATE workflows
SET
    status = @status,
    started_at = CASE
        WHEN @status IN ('waiting_for_supervisor', 'waiting_for_department', 'in_progress', 'completed')
            THEN COALESCE(started_at, NOW())
        ELSE started_at
    END,
    completed_at = CASE
        WHEN @status = 'completed' THEN COALESCE(completed_at, NOW())
        ELSE NULL
    END
WHERE id = @workflowId;";

        await using var updateCommand = new NpgsqlCommand(workflowStatusUpdateSql, connection, transaction);
        updateCommand.Parameters.AddWithValue("status", nextWorkflowStatus);
        updateCommand.Parameters.AddWithValue("workflowId", workflowId);
        await updateCommand.ExecuteNonQueryAsync();

        if (!string.Equals(currentWorkflowStatus, nextWorkflowStatus, StringComparison.OrdinalIgnoreCase))
        {
            await InsertAuditEntry(
                connection,
                transaction,
                workflowId,
                null,
                actorUserId,
                "workflow_status_changed",
                currentWorkflowStatus,
                nextWorkflowStatus);
        }
    }
}
