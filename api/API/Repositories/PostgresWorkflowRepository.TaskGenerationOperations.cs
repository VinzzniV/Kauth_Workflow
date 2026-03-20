using Npgsql;
using NpgsqlTypes;

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
        int workflowDepartmentId)
    {
        await GenerateWorkflowTasks(
            connection,
            transaction,
            workflowId,
            workflowDepartmentId,
            new Dictionary<string, StoredWorkflowAnswerRecord>(StringComparer.OrdinalIgnoreCase),
            TaskGenerationStage.Initial);

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
            if (TerminalTaskStatuses.Contains(task.Status))
            {
                continue;
            }

            await PersistTaskStatus(connection, transaction, task.TaskId, "done");
            await SyncPrimaryAssignmentCompletion(connection, transaction, task.TaskId, "done");
        }
    }

    private static async Task<bool> CompleteWorkflowTaskByKey(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long workflowId,
        string taskKey)
    {
        const string sql = @"
SELECT id, status
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

        if (currentStatus.Equals("done", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (TerminalTaskStatuses.Contains(currentStatus))
        {
            throw new InvalidOperationException($"Task '{taskKey}' can no longer be completed.");
        }

        await reader.DisposeAsync();

        await PersistTaskStatus(connection, transaction, taskId, "done");
        await SyncPrimaryAssignmentCompletion(connection, transaction, taskId, "done");
        return true;
    }

    private static async Task GenerateWorkflowTasks(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long workflowId,
        int workflowDepartmentId,
        IReadOnlyDictionary<string, StoredWorkflowAnswerRecord> answersByKey,
        TaskGenerationStage stage)
    {
        var templates = await LoadTaskTemplates(connection, transaction);
        var conditions = await LoadTaskTemplateConditions(connection, transaction);
        var dependencies = await LoadTaskTemplateDependencies(connection, transaction);

        var conditionsByTemplateId = conditions
            .GroupBy(condition => condition.TaskTemplateId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var postSupervisorTemplateIds = GetPostSupervisorTemplateIds(templates, dependencies);

        var selectedTemplates = templates
            .Where(template =>
            {
                var isPostSupervisorTask = postSupervisorTemplateIds.Contains(template.Id);
                return stage == TaskGenerationStage.Initial
                    ? !isPostSupervisorTask
                    : isPostSupervisorTask;
            })
            .Where(template => !template.TemplateKey.Equals(LegacySupervisorHandoverTaskKey, StringComparison.OrdinalIgnoreCase))
            .Where(template =>
            {
                conditionsByTemplateId.TryGetValue(template.Id, out var templateConditions);
                templateConditions ??= new List<TaskTemplateConditionRecord>();
                return ShouldCreateTask(templateConditions, answersByKey);
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

        foreach (var template in selectedTemplates)
        {
            var hasSelectedDependency = dependenciesByTaskTemplateId.TryGetValue(template.Id, out var templateDependencies)
                && templateDependencies.Any(dependency => selectedTemplateIds.Contains(dependency.DependsOnTaskTemplateId));

            var status = hasSelectedDependency ? "blocked" : "ready";
            var taskDescription = BuildTaskDescription(template, answersByKey);

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
                insertTaskCommand.Parameters.AddWithValue("sortOrder", template.SortOrder);

                var scalar = await insertTaskCommand.ExecuteScalarAsync();
                if (scalar is null)
                {
                    throw new InvalidOperationException("Workflow task could not be created.");
                }

                workflowTaskId = (long)scalar;
            }

            createdTasks[template.Id] = new CreatedWorkflowTaskRecord
            {
                TaskTemplateId = template.Id,
                WorkflowTaskId = workflowTaskId,
                TaskKey = template.TemplateKey
            };

            var assigneeResponsibilityId = template.DefaultResponsibilityId;
            long? assigneeUserId = null;

            if (template.TemplateKey == "supervisor_fills_document")
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

            if (assigneeResponsibilityId.HasValue || assigneeUserId.HasValue)
            {
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
    }

    private static HashSet<int> GetPostSupervisorTemplateIds(
        IReadOnlyList<TaskTemplateRecord> templates,
        IReadOnlyList<TaskTemplateDependencyRecord> dependencies)
    {
        var supervisorTemplateId = templates
            .FirstOrDefault(template => template.TemplateKey.Equals(SupervisorRequirementTaskKey, StringComparison.OrdinalIgnoreCase))
            ?.Id;

        if (!supervisorTemplateId.HasValue)
        {
            return new HashSet<int>();
        }

        var dependentsByTemplateId = dependencies
            .GroupBy(dependency => dependency.DependsOnTaskTemplateId)
            .ToDictionary(group => group.Key, group => group.Select(item => item.TaskTemplateId).ToList());

        var postSupervisorTemplateIds = new HashSet<int>();
        var queue = new Queue<int>();
        queue.Enqueue(supervisorTemplateId.Value);

        while (queue.Count > 0)
        {
            var currentTemplateId = queue.Dequeue();
            if (!dependentsByTemplateId.TryGetValue(currentTemplateId, out var dependentTemplateIds))
            {
                continue;
            }

            foreach (var dependentTemplateId in dependentTemplateIds)
            {
                if (postSupervisorTemplateIds.Add(dependentTemplateId))
                {
                    queue.Enqueue(dependentTemplateId);
                }
            }
        }

        return postSupervisorTemplateIds;
    }

    private static async Task<List<TaskTemplateRecord>> LoadTaskTemplates(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction)
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
    sort_order
FROM task_templates
WHERE is_active = TRUE
ORDER BY sort_order, id;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        await using var reader = await command.ExecuteReaderAsync();

        var templates = new List<TaskTemplateRecord>();
        while (await reader.ReadAsync())
        {
            templates.Add(new TaskTemplateRecord
            {
                Id = reader.GetInt32(0),
                TemplateKey = reader.GetString(1),
                Title = reader.GetString(2),
                Description = reader.GetString(3),
                Category = reader.GetString(4),
                IconKey = reader.GetString(5),
                DefaultResponsibilityId = reader.IsDBNull(6) ? null : reader.GetInt32(6),
                ProcessAreaLabel = reader.IsDBNull(7) ? null : reader.GetString(7),
                IsDepartmentPhaseTask = reader.GetBoolean(8),
                IsRequired = reader.GetBoolean(9),
                SortOrder = reader.GetInt32(10)
            });
        }

        return templates;
    }

    private static async Task<List<TaskTemplateConditionRecord>> LoadTaskTemplateConditions(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction)
    {
        const string sql = @"
SELECT
    task_template_id,
    condition_group,
    answer_key,
    operator,
    expected_value_text,
    expected_value_boolean,
    expected_value_number
FROM task_template_conditions
ORDER BY task_template_id, condition_group, id;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
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
        NpgsqlTransaction transaction)
    {
        const string sql = @"
SELECT task_template_id, depends_on_task_template_id, required_status
FROM task_template_dependencies
ORDER BY task_template_id, id;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
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

    private static bool ShouldCreateTask(
        IReadOnlyList<TaskTemplateConditionRecord> conditions,
        IReadOnlyDictionary<string, StoredWorkflowAnswerRecord> answersByKey)
    {
        if (conditions.Count == 0)
        {
            return true;
        }

        foreach (var conditionGroup in conditions.GroupBy(condition => condition.ConditionGroup))
        {
            if (conditionGroup.All(condition => EvaluateCondition(condition, answersByKey)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool EvaluateCondition(
        TaskTemplateConditionRecord condition,
        IReadOnlyDictionary<string, StoredWorkflowAnswerRecord> answersByKey)
    {
        if (!answersByKey.TryGetValue(condition.AnswerKey, out var answer))
        {
            return false;
        }

        return condition.Operator switch
        {
            "is_true" => answer.ValueBoolean == true,
            "is_false" => answer.ValueBoolean == false,
            "is_null" => IsAnswerEmpty(answer),
            "is_not_null" => !IsAnswerEmpty(answer),
            "eq" => EvaluateEquality(answer, condition),
            "neq" => !EvaluateEquality(answer, condition),
            _ => false
        };
    }

    private static bool EvaluateEquality(StoredWorkflowAnswerRecord answer, TaskTemplateConditionRecord condition)
    {
        if (condition.ExpectedValueBoolean.HasValue)
        {
            return answer.ValueBoolean.HasValue && answer.ValueBoolean.Value == condition.ExpectedValueBoolean.Value;
        }

        if (condition.ExpectedValueNumber.HasValue)
        {
            return answer.ValueNumber.HasValue && answer.ValueNumber.Value == condition.ExpectedValueNumber.Value;
        }

        if (!string.IsNullOrWhiteSpace(condition.ExpectedValueText))
        {
            var expected = condition.ExpectedValueText.Trim();
            if (!string.IsNullOrWhiteSpace(answer.ValueText)
                && string.Equals(answer.ValueText.Trim(), expected, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(answer.SelectedOptionValue)
                && string.Equals(answer.SelectedOptionValue.Trim(), expected, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return answer.SelectedOptionValues.Any(
                optionValue => string.Equals(optionValue.Trim(), expected, StringComparison.OrdinalIgnoreCase));
        }

        return false;
    }

    private static bool IsAnswerEmpty(StoredWorkflowAnswerRecord answer)
    {
        return answer.ValueBoolean is null
               && string.IsNullOrWhiteSpace(answer.ValueText)
               && answer.ValueNumber is null
               && answer.SelectedOptionId is null
               && answer.SelectedOptionIds.Count == 0;
    }

    private static string BuildTaskDescription(
        TaskTemplateRecord template,
        IReadOnlyDictionary<string, StoredWorkflowAnswerRecord> answersByKey)
    {
        var description = template.Description.Trim();

        return template.TemplateKey switch
        {
            "permissions_from_reference_user" => AppendTaskContext(
                description,
                GetTextAnswer(answersByKey, RequirementKeys.ComparisonUserName) is { Length: > 0 } comparisonUserName
                    ? $"Referenzuser: {comparisonUserName}."
                    : null),
            "hardware_procure" or "hardware_setup" or "hardware_handover" => AppendTaskContext(
                description,
                GetHardwareTypeText(answersByKey) is { Length: > 0 } hardwareType
                    ? $"Gewünschte Hardware: {hardwareType}."
                    : null),
            _ => description
        };
    }

    private static string AppendTaskContext(string description, string? context)
    {
        if (string.IsNullOrWhiteSpace(context))
        {
            return description;
        }

        return string.IsNullOrWhiteSpace(description)
            ? context.Trim()
            : $"{description} {context.Trim()}";
    }
}
