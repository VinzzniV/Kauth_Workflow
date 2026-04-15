using Npgsql;
using NpgsqlTypes;

namespace API;

internal sealed partial class PostgresWorkflowRepository
{
    private sealed class WorkflowTaskGenerationContext
    {
        public required int ProcessTypeId { get; init; }
        public required string ProcessTypeName { get; init; }
        public required bool RequiresSupervisorStep { get; init; }
        public string? ApprovalTaskTemplateKey { get; init; }
    }

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
        var generatedTaskCount = await GenerateWorkflowTasks(
            connection,
            transaction,
            workflowId,
            workflowDepartmentId,
            new Dictionary<string, StoredWorkflowAnswerRecord>(StringComparer.OrdinalIgnoreCase),
            TaskGenerationStage.Initial);

        if (generatedTaskCount > 0)
        {
            await InsertAuditEntry(
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

        await RecalculateWorkflowTaskAvailability(connection, transaction, workflowId);

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

            await PersistTaskStatus(connection, transaction, task.TaskId, "done");
            await SyncPrimaryAssignmentCompletion(connection, transaction, task.TaskId, "done");
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

        await PersistTaskStatus(connection, transaction, taskId, "done");
        await SyncPrimaryAssignmentCompletion(connection, transaction, taskId, "done");
        await InsertAuditEntry(
            connection,
            transaction,
            workflowId,
            taskId,
            actorUserId,
            "task_status_changed",
            currentStatus,
            "done",
            BuildTaskStatusAuditDetail(taskTitle));
        return true;
    }

    private static async Task<int> GenerateWorkflowTasks(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long workflowId,
        int workflowDepartmentId,
        IReadOnlyDictionary<string, StoredWorkflowAnswerRecord> answersByKey,
        TaskGenerationStage stage)
    {
        var workflowContext = await LoadWorkflowTaskGenerationContext(connection, transaction, workflowId);
        var workflowDueAt = await LoadWorkflowDueAt(connection, transaction, workflowId);
        var templates = await LoadTaskTemplates(connection, transaction, workflowContext.ProcessTypeId);
        var conditions = await LoadTaskTemplateConditions(connection, transaction, workflowContext.ProcessTypeId);
        var dependencies = await LoadTaskTemplateDependencies(connection, transaction, workflowContext.ProcessTypeId);

        var conditionsByTemplateId = conditions
            .GroupBy(condition => condition.TaskTemplateId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var postSupervisorTemplateIds = workflowContext.RequiresSupervisorStep
            ? TaskConditionEvaluator.GetPostSupervisorTemplateIds(
                templates,
                dependencies,
                workflowContext.ApprovalTaskTemplateKey)
            : new HashSet<int>();

        var selectedTemplates = templates
            .Where(template =>
            {
                if (stage == TaskGenerationStage.Full)
                {
                    // Das Approval-Template wird im Definition-Layer durch den runtime approval-Node
                    // als Runtime-Task erzeugt. Es darf hier nicht als Legacy-Task doppelt generiert werden.
                    if (workflowContext.RequiresSupervisorStep
                        && !string.IsNullOrWhiteSpace(workflowContext.ApprovalTaskTemplateKey)
                        && template.TemplateKey.Equals(
                            workflowContext.ApprovalTaskTemplateKey,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }

                    return true;
                }

                if (!workflowContext.RequiresSupervisorStep)
                {
                    return stage == TaskGenerationStage.Initial;
                }

                var isPostSupervisorTask = postSupervisorTemplateIds.Contains(template.Id);
                return stage == TaskGenerationStage.Initial
                    ? !isPostSupervisorTask
                    : isPostSupervisorTask;
            })
            .Where(template =>
            {
                conditionsByTemplateId.TryGetValue(template.Id, out var templateConditions);
                templateConditions ??= new List<TaskTemplateConditionRecord>();
                return TaskConditionEvaluator.ShouldCreateTask(templateConditions, answersByKey);
            })
            .OrderBy(template => template.SortOrder)
            .ThenBy(template => template.Id)
            .ToList();

        var selectedTemplateIds = selectedTemplates.Select(template => template.Id).ToHashSet();
        var dependenciesByTaskTemplateId = dependencies
            .GroupBy(dependency => dependency.TaskTemplateId)
            .ToDictionary(group => group.Key, group => group.ToList());

        const string insertTaskSql = @"
INSERT INTO workflow_tasks (
    workflow_id,
    task_template_id,
    task_key,
    title,
    category,
    description,
    icon_key,
    process_area_label,
    is_department_phase_task,
    status,
    is_required,
    due_in_days,
    due_at,
    sort_order,
    ready_at
)
VALUES (
    @workflowId,
    @taskTemplateId,
    @taskKey,
    @title,
    @category,
    @description,
    @iconKey,
    @processAreaLabel,
    @isDepartmentPhaseTask,
    @status,
    @isRequired,
    @dueInDays,
    CASE
        WHEN @workflowDueAt IS NOT NULL AND @dueInDays IS NOT NULL
            THEN LEAST(@workflowDueAt, NOW() + (@dueInDays * INTERVAL '1 day'))
        WHEN @workflowDueAt IS NOT NULL THEN @workflowDueAt
        WHEN @dueInDays IS NOT NULL THEN NOW() + (@dueInDays * INTERVAL '1 day')
        ELSE NULL
    END,
    @sortOrder,
    CASE WHEN @status = 'ready' THEN NOW() ELSE NULL END
)
RETURNING id;";

        const string insertAssignmentSql = @"
INSERT INTO task_assignments (
    workflow_task_id,
    assignee_user_id,
    assignee_responsibility_id,
    assignment_type,
    is_primary
)
VALUES (
    @workflowTaskId,
    @assigneeUserId,
    @assigneeResponsibilityId,
    @assignmentType,
    TRUE
);";

        const string insertWorkflowDependencySql = @"
INSERT INTO workflow_task_dependencies (
    workflow_task_id,
    depends_on_workflow_task_id,
    required_status
)
VALUES (
    @workflowTaskId,
    @dependsOnWorkflowTaskId,
    @requiredStatus
)
ON CONFLICT (workflow_task_id, depends_on_workflow_task_id) DO NOTHING;";

        var createdTasks = new Dictionary<int, CreatedWorkflowTaskRecord>();
        var generatedTaskCount = 0;

        foreach (var template in selectedTemplates)
        {
            var hasSelectedDependency = dependenciesByTaskTemplateId.TryGetValue(template.Id, out var templateDependencies)
                && templateDependencies.Any(dependency => selectedTemplateIds.Contains(dependency.DependsOnTaskTemplateId));

            var status = hasSelectedDependency ? "blocked" : "ready";
            var taskDescription = TaskConditionEvaluator.BuildTaskDescription(template, answersByKey);

            long workflowTaskId;
            await using (var insertTaskCommand = new NpgsqlCommand(insertTaskSql, connection, transaction))
            {
                insertTaskCommand.Parameters.AddWithValue("workflowId", workflowId);
                insertTaskCommand.Parameters.AddWithValue("taskTemplateId", template.Id);
                insertTaskCommand.Parameters.AddWithValue("taskKey", template.TemplateKey);
                insertTaskCommand.Parameters.AddWithValue("title", template.Title);
                insertTaskCommand.Parameters.AddWithValue("category", template.Category);
                insertTaskCommand.Parameters.AddWithValue("description", taskDescription);
                insertTaskCommand.Parameters.AddWithValue("iconKey", template.IconKey);
                insertTaskCommand.Parameters.Add("processAreaLabel", NpgsqlTypes.NpgsqlDbType.Varchar).Value =
                    (object?)template.ProcessAreaLabel ?? DBNull.Value;
                insertTaskCommand.Parameters.AddWithValue("isDepartmentPhaseTask", template.IsDepartmentPhaseTask);
                insertTaskCommand.Parameters.AddWithValue("status", status);
                insertTaskCommand.Parameters.AddWithValue("isRequired", template.IsRequired);
                insertTaskCommand.Parameters.Add("dueInDays", NpgsqlDbType.Integer).Value =
                    (object?)template.DueInDays ?? DBNull.Value;
                insertTaskCommand.Parameters.Add("workflowDueAt", NpgsqlDbType.TimestampTz).Value =
                    (object?)workflowDueAt ?? DBNull.Value;
                insertTaskCommand.Parameters.AddWithValue("sortOrder", template.SortOrder);

                var scalar = await insertTaskCommand.ExecuteScalarAsync();
                if (scalar is null)
                {
                    throw new InvalidOperationException("Workflow task could not be created.");
                }

                workflowTaskId = (long)scalar;
            }

            generatedTaskCount += 1;

            createdTasks[template.Id] = new CreatedWorkflowTaskRecord
            {
                TaskTemplateId = template.Id,
                WorkflowTaskId = workflowTaskId,
                TaskKey = template.TemplateKey
            };

            var assigneeResponsibilityId = template.DefaultResponsibilityId;
            long? assigneeUserId = null;

            if (!string.IsNullOrWhiteSpace(workflowContext.ApprovalTaskTemplateKey)
                && template.TemplateKey.Equals(workflowContext.ApprovalTaskTemplateKey, StringComparison.OrdinalIgnoreCase))
            {
                var supervisorAssignment = await ResolveDepartmentRequirementSelectionAssignment(
                    connection,
                    transaction,
                    workflowDepartmentId);
                assigneeUserId = supervisorAssignment.UserId;
                assigneeResponsibilityId = supervisorAssignment.ResponsibilityId;
            }
            else if (template.DefaultResponsibilityId.HasValue)
            {
                assigneeUserId = await ResolvePrimaryAssigneeUserId(
                    connection,
                    transaction,
                    template.DefaultResponsibilityId.Value,
                    workflowDepartmentId);
            }

            if (!assigneeResponsibilityId.HasValue && !assigneeUserId.HasValue)
            {
                throw new InvalidOperationException(
                    $"Task template '{template.TemplateKey}' cannot be generated without a responsible assignment.");
            }

            var assignmentType = assigneeUserId.HasValue ? "user" : "responsibility";
            var storedAssigneeResponsibilityId = assignmentType == "responsibility"
                ? assigneeResponsibilityId
                : null;

            await using var insertAssignmentCommand = new NpgsqlCommand(insertAssignmentSql, connection, transaction);
            insertAssignmentCommand.Parameters.AddWithValue("workflowTaskId", workflowTaskId);
            insertAssignmentCommand.Parameters.Add("assigneeUserId", NpgsqlDbType.Bigint).Value = (object?)assigneeUserId ?? DBNull.Value;
            insertAssignmentCommand.Parameters.Add("assigneeResponsibilityId", NpgsqlDbType.Integer).Value =
                (object?)storedAssigneeResponsibilityId ?? DBNull.Value;
            insertAssignmentCommand.Parameters.AddWithValue(
                "assignmentType",
                assignmentType);
            await insertAssignmentCommand.ExecuteNonQueryAsync();
        }

        foreach (var dependency in dependencies)
        {
            if (!createdTasks.TryGetValue(dependency.TaskTemplateId, out var workflowTask))
            {
                continue;
            }

            if (!createdTasks.TryGetValue(dependency.DependsOnTaskTemplateId, out var dependsOnWorkflowTask))
            {
                continue;
            }

            await using var insertDependencyCommand = new NpgsqlCommand(insertWorkflowDependencySql, connection, transaction);
            insertDependencyCommand.Parameters.AddWithValue("workflowTaskId", workflowTask.WorkflowTaskId);
            insertDependencyCommand.Parameters.AddWithValue("dependsOnWorkflowTaskId", dependsOnWorkflowTask.WorkflowTaskId);
            insertDependencyCommand.Parameters.AddWithValue("requiredStatus", dependency.RequiredStatus);
            await insertDependencyCommand.ExecuteNonQueryAsync();
        }

        return generatedTaskCount;
    }

    private static async Task<WorkflowTaskGenerationContext> LoadWorkflowTaskGenerationContext(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long workflowId)
    {
        const string sql = @"
SELECT
    w.process_type_id,
    pt.name,
    pt.requires_supervisor_step,
    pt.approval_task_template_key
FROM workflows w
JOIN process_types pt ON pt.id = w.process_type_id
WHERE w.id = @workflowId
LIMIT 1;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("workflowId", workflowId);
        await using var reader = await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            throw new InvalidOperationException("Workflow context could not be loaded for task generation.");
        }

        return new WorkflowTaskGenerationContext
        {
            ProcessTypeId = reader.GetInt32(0),
            ProcessTypeName = reader.GetString(1),
            RequiresSupervisorStep = reader.GetBoolean(2),
            ApprovalTaskTemplateKey = WorkflowStatusRules.EnsureApprovalTaskConfiguration(
                reader.GetString(1),
                reader.GetBoolean(2),
                reader.IsDBNull(3) ? null : reader.GetString(3))
        };
    }

    private static async Task<DateTime?> LoadWorkflowDueAt(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long workflowId)
    {
        const string sql = @"
SELECT deadline_date
FROM workflows
WHERE id = @workflowId
LIMIT 1;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("workflowId", workflowId);

        var scalar = await command.ExecuteScalarAsync();
        if (scalar is null || scalar is DBNull)
        {
            return null;
        }

        var deadlineDate = (DateOnly)scalar;
        return TaskDueDateRules.ToWorkflowDeadlineDueAt(deadlineDate);
    }

    private static async Task<List<TaskTemplateRecord>> LoadTaskTemplates(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int processTypeId)
    {
        const string sql = @"
SELECT
    id,
    template_key,
    title,
    description,
    category,
    icon_key,
    default_responsibility_id,
    process_area_label,
    is_department_phase_task,
    is_required,
    due_in_days,
    sort_order
FROM task_templates
WHERE process_type_id = @processTypeId
  AND is_active = TRUE
ORDER BY sort_order, id;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("processTypeId", processTypeId);
        await using var reader = await command.ExecuteReaderAsync();

        var templates = new List<TaskTemplateRecord>();
        while (await reader.ReadAsync())
        {
            templates.Add(new TaskTemplateRecord
            {
                Id = reader.GetInt32(0),
                TemplateKey = reader.GetString(1),
                Title = reader.GetString(2),
                Description = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                Category = reader.GetString(4),
                IconKey = NormalizeAdminTaskTemplateIconKey(reader.IsDBNull(5) ? null : reader.GetString(5)),
                DefaultResponsibilityId = reader.IsDBNull(6) ? null : reader.GetInt32(6),
                ProcessAreaLabel = reader.IsDBNull(7) ? null : reader.GetString(7),
                IsDepartmentPhaseTask = reader.GetBoolean(8),
                IsRequired = reader.GetBoolean(9),
                DueInDays = reader.IsDBNull(10) ? null : reader.GetInt32(10),
                SortOrder = reader.GetInt32(11)
            });
        }

        return templates;
    }

    private static async Task<List<TaskTemplateConditionRecord>> LoadTaskTemplateConditions(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int processTypeId)
    {
        const string sql = @"
SELECT
    c.task_template_id,
    c.condition_group,
    c.answer_key,
    c.operator,
    c.expected_value_text,
    c.expected_value_boolean,
    c.expected_value_number
FROM task_template_conditions c
JOIN task_templates t ON t.id = c.task_template_id
WHERE t.process_type_id = @processTypeId
ORDER BY c.task_template_id, c.condition_group, c.id;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("processTypeId", processTypeId);
        await using var reader = await command.ExecuteReaderAsync();

        var conditions = new List<TaskTemplateConditionRecord>();
        while (await reader.ReadAsync())
        {
            conditions.Add(new TaskTemplateConditionRecord
            {
                TaskTemplateId = reader.GetInt32(0),
                ConditionGroup = reader.GetInt32(1),
                AnswerKey = reader.GetString(2),
                Operator = reader.GetString(3),
                ExpectedValueText = reader.IsDBNull(4) ? null : reader.GetString(4),
                ExpectedValueBoolean = reader.IsDBNull(5) ? null : reader.GetBoolean(5),
                ExpectedValueNumber = reader.IsDBNull(6) ? null : reader.GetDecimal(6)
            });
        }

        return conditions;
    }

    private static async Task<List<TaskTemplateDependencyRecord>> LoadTaskTemplateDependencies(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int processTypeId)
    {
        const string sql = @"
SELECT d.task_template_id, d.depends_on_task_template_id, d.required_status
FROM task_template_dependencies d
JOIN task_templates t ON t.id = d.task_template_id
WHERE t.process_type_id = @processTypeId
ORDER BY d.task_template_id, d.id;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("processTypeId", processTypeId);
        await using var reader = await command.ExecuteReaderAsync();

        var dependencies = new List<TaskTemplateDependencyRecord>();
        while (await reader.ReadAsync())
        {
            dependencies.Add(new TaskTemplateDependencyRecord
            {
                TaskTemplateId = reader.GetInt32(0),
                DependsOnTaskTemplateId = reader.GetInt32(1),
                RequiredStatus = reader.GetString(2)
            });
        }

        return dependencies;
    }

}
