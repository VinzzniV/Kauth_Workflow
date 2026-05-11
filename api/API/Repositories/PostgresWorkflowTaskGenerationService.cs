using Npgsql;
using NpgsqlTypes;

namespace API;

internal sealed class PostgresWorkflowTaskGenerationService : IWorkflowTaskGenerationService
{
    public Task<int> GenerateWorkflowTasks(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long workflowId,
        int workflowDepartmentId,
        IReadOnlyDictionary<string, StoredWorkflowAnswerRecord> answersByKey,
        TaskGenerationStage stage)
        => GenerateWorkflowTasksAsync(connection, transaction, workflowId, workflowDepartmentId, answersByKey, stage);

    public Task<WorkflowTaskGenerationContext> LoadWorkflowTaskGenerationContext(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long workflowId)
        => LoadWorkflowTaskGenerationContextAsync(connection, transaction, workflowId);

    public Task<DateTime?> LoadWorkflowDueAt(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long workflowId)
        => LoadWorkflowDueAtAsync(connection, transaction, workflowId);

    internal static async Task<int> GenerateWorkflowTasksAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long workflowId,
        int workflowDepartmentId,
        IReadOnlyDictionary<string, StoredWorkflowAnswerRecord> answersByKey,
        TaskGenerationStage stage)
    {
        var workflowContext = await LoadWorkflowTaskGenerationContextAsync(connection, transaction, workflowId);
        var workflowDueAt = await LoadWorkflowDueAtAsync(connection, transaction, workflowId);
        if (!workflowContext.MeasureNodeId.HasValue)
        {
            return 0;
        }
        var templates = await LoadTaskTemplatesAsync(connection, transaction, workflowContext.MeasureNodeId.Value);
        var conditions = await LoadTaskTemplateConditionsAsync(connection, transaction, workflowContext.MeasureNodeId.Value);
        var dependencies = await LoadTaskTemplateDependenciesAsync(connection, transaction, workflowContext.MeasureNodeId.Value);

        var conditionsByTemplateId = conditions
            .GroupBy(condition => condition.TaskTemplateId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var postSupervisorTemplateIds = workflowContext.RequiresSupervisorStep
            ? TaskConditionEvaluator.GetPostSupervisorTemplateIds(
                templates,
                dependencies,
                workflowContext.ApprovalSpecKey)
            : new HashSet<int>();

        var selectedTemplates = templates
            .Where(template =>
            {
                if (stage == TaskGenerationStage.Full)
                {
                    // Das Approval-Template wird im Definition-Layer durch den runtime approval-Node
                    // als Runtime-Task erzeugt. Es darf hier nicht als Legacy-Task doppelt generiert werden.
                    if (workflowContext.RequiresSupervisorStep
                        && !string.IsNullOrWhiteSpace(workflowContext.ApprovalSpecKey)
                        && template.TemplateKey.Equals(
                            workflowContext.ApprovalSpecKey,
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
    workflow_node_task_spec_id,
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
    @workflowNodeTaskSpecId,
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
                insertTaskCommand.Parameters.AddWithValue("workflowNodeTaskSpecId", (long)template.Id);
                insertTaskCommand.Parameters.AddWithValue("taskKey", template.TemplateKey);
                insertTaskCommand.Parameters.AddWithValue("title", template.Title);
                insertTaskCommand.Parameters.AddWithValue("category", template.Category);
                insertTaskCommand.Parameters.AddWithValue("description", taskDescription);
                insertTaskCommand.Parameters.AddWithValue("iconKey", template.IconKey);
                insertTaskCommand.Parameters.Add("processAreaLabel", NpgsqlDbType.Varchar).Value =
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

            if (!string.IsNullOrWhiteSpace(workflowContext.ApprovalSpecKey)
                && template.TemplateKey.Equals(workflowContext.ApprovalSpecKey, StringComparison.OrdinalIgnoreCase))
            {
                var supervisorAssignment = await PostgresRepositorySharedHelpers.ResolveDepartmentRequirementSelectionAssignment(
                    connection,
                    transaction,
                    workflowDepartmentId);
                assigneeUserId = supervisorAssignment.UserId;
                assigneeResponsibilityId = supervisorAssignment.ResponsibilityId;
            }
            else if (template.DefaultResponsibilityId.HasValue)
            {
                assigneeUserId = await PostgresRepositorySharedHelpers.ResolvePrimaryAssigneeUserId(
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

    internal static async Task<WorkflowTaskGenerationContext> LoadWorkflowTaskGenerationContextAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long workflowId)
    {
        // LA5-C: MeasureNodeId zusaetzlich laden, um Task-Specs ueber den Maßnahmen-Node
        // zu laden statt ueber die Definition-Id. Fallback: published Version der Definition,
        // falls workflow_definition_version_id der Workflow-Zeile NULL ist (Legacy-Pfad).
        const string sql = @"
SELECT
    w.workflow_definition_id,
    pt.name,
    pt.requires_supervisor_step,
    pt.approval_spec_key,
    measure_node.id AS measure_node_id
FROM workflows w
JOIN workflow_definitions pt ON pt.id = w.workflow_definition_id
LEFT JOIN workflow_definition_versions v
    ON v.id = COALESCE(w.workflow_definition_version_id, (
        SELECT id FROM workflow_definition_versions
        WHERE workflow_definition_id = w.workflow_definition_id
          AND published_at IS NOT NULL
        ORDER BY published_at DESC
        LIMIT 1
    ))
LEFT JOIN workflow_nodes measure_node
    ON measure_node.workflow_definition_version_id = v.id
   AND measure_node.node_type LIKE 'measure_%'
WHERE w.id = @workflowId
ORDER BY measure_node.id
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
            WorkflowDefinitionId = reader.GetInt32(0),
            WorkflowDefinitionName = reader.GetString(1),
            RequiresSupervisorStep = reader.GetBoolean(2),
            ApprovalSpecKey = WorkflowStatusRules.EnsureApprovalTaskConfiguration(
                reader.GetString(1),
                reader.GetBoolean(2),
                reader.IsDBNull(3) ? null : reader.GetString(3)),
            MeasureNodeId = reader.IsDBNull(4) ? null : reader.GetInt64(4)
        };
    }

    internal static async Task<DateTime?> LoadWorkflowDueAtAsync(
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

    private static async Task<List<TaskTemplateRecord>> LoadTaskTemplatesAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long measureNodeId)
    {
        // LA5-C: liest aus workflow_node_task_specs statt task_templates.
        // Anker: workflow_node_id (Maßnahmen-Node der Workflow-Version).
        // is_active-Filter entfaellt — neues Schema hat keine Soft-Delete-Spalte.
        const string sql = @"
SELECT
    id,
    spec_key,
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
FROM workflow_node_task_specs
WHERE workflow_node_id = @measureNodeId
ORDER BY sort_order, id;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("measureNodeId", measureNodeId);
        await using var reader = await command.ExecuteReaderAsync();

        var templates = new List<TaskTemplateRecord>();
        while (await reader.ReadAsync())
        {
            templates.Add(new TaskTemplateRecord
            {
                Id = checked((int)reader.GetInt64(0)),
                TemplateKey = reader.GetString(1),
                Title = reader.GetString(2),
                Description = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                Category = reader.GetString(4),
                IconKey = PostgresRepositorySharedHelpers.NormalizeAdminTaskTemplateIconKey(reader.IsDBNull(5) ? null : reader.GetString(5)),
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

    private static async Task<List<TaskTemplateConditionRecord>> LoadTaskTemplateConditionsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long measureNodeId)
    {
        // LA5-C: liest aus workflow_node_task_spec_conditions.
        // condition_group entfaellt im neuen Schema (Inventur: immer 1) — Hardcode auf 1.
        const string sql = @"
SELECT
    c.workflow_node_task_spec_id,
    c.answer_key,
    c.operator,
    c.expected_value_text,
    c.expected_value_boolean,
    c.expected_value_number
FROM workflow_node_task_spec_conditions c
JOIN workflow_node_task_specs s ON s.id = c.workflow_node_task_spec_id
WHERE s.workflow_node_id = @measureNodeId
ORDER BY c.workflow_node_task_spec_id, c.id;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("measureNodeId", measureNodeId);
        await using var reader = await command.ExecuteReaderAsync();

        var conditions = new List<TaskTemplateConditionRecord>();
        while (await reader.ReadAsync())
        {
            conditions.Add(new TaskTemplateConditionRecord
            {
                TaskTemplateId = checked((int)reader.GetInt64(0)),
                ConditionGroup = 1,
                AnswerKey = reader.GetString(1),
                Operator = reader.GetString(2),
                ExpectedValueText = reader.IsDBNull(3) ? null : reader.GetString(3),
                ExpectedValueBoolean = reader.IsDBNull(4) ? null : reader.GetBoolean(4),
                ExpectedValueNumber = reader.IsDBNull(5) ? null : reader.GetDecimal(5)
            });
        }

        return conditions;
    }

    private static async Task<List<TaskTemplateDependencyRecord>> LoadTaskTemplateDependenciesAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long measureNodeId)
    {
        // LA5-C: liest aus workflow_node_task_spec_dependencies.
        // required_status entfaellt im neuen Schema (Inventur: immer 'done') — Hardcode.
        const string sql = @"
SELECT d.workflow_node_task_spec_id, d.depends_on_workflow_node_task_spec_id
FROM workflow_node_task_spec_dependencies d
WHERE d.workflow_node_id = @measureNodeId
ORDER BY d.workflow_node_task_spec_id, d.id;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("measureNodeId", measureNodeId);
        await using var reader = await command.ExecuteReaderAsync();

        var dependencies = new List<TaskTemplateDependencyRecord>();
        while (await reader.ReadAsync())
        {
            dependencies.Add(new TaskTemplateDependencyRecord
            {
                TaskTemplateId = checked((int)reader.GetInt64(0)),
                DependsOnTaskTemplateId = checked((int)reader.GetInt64(1)),
                RequiredStatus = "done"
            });
        }

        return dependencies;
    }
}
