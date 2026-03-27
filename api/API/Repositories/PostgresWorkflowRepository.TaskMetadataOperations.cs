using Npgsql;
using NpgsqlTypes;

namespace API;

internal sealed partial class PostgresWorkflowRepository
{
    public async Task<List<TaskWithWorkflowDto>> GetTasks()
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();

        return await LoadTasks(connection, null, null);
    }

    public async Task<TaskWithWorkflowDto?> GetTaskById(long taskId)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();

        var tasks = await LoadTasks(connection, null, taskId);
        return tasks.FirstOrDefault();
    }

    private static async Task<Dictionary<long, WorkflowListMetadata>> LoadWorkflowListMetadata(
        NpgsqlConnection connection,
        IReadOnlyList<long> workflowIds)
    {
        var metadataByWorkflowId = new Dictionary<long, WorkflowListMetadata>();
        if (workflowIds.Count == 0)
        {
            return metadataByWorkflowId;
        }

        const string sql = @"
SELECT
    wt.workflow_id,
    wt.task_key,
    wt.status,
    wt.is_department_phase_task,
    selected_assignment.assignment_type,
    selected_responsibility.responsibility_key,
    selected_responsibility.name
FROM workflow_tasks wt
LEFT JOIN LATERAL (
    SELECT
        ta.assignment_type,
        ta.assignee_responsibility_id
    FROM task_assignments ta
    WHERE ta.workflow_task_id = wt.id
    ORDER BY ta.is_primary DESC, ta.id
    LIMIT 1
) selected_assignment ON TRUE
LEFT JOIN app_responsibilities selected_responsibility
    ON selected_responsibility.id = selected_assignment.assignee_responsibility_id
WHERE wt.workflow_id = ANY(@workflowIds);";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.Add("workflowIds", NpgsqlDbType.Array | NpgsqlDbType.Bigint).Value = workflowIds;

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var workflowId = reader.GetInt64(0);
            if (!metadataByWorkflowId.TryGetValue(workflowId, out var metadata))
            {
                metadata = new WorkflowListMetadata();
                metadataByWorkflowId[workflowId] = metadata;
            }

            metadata.TotalTaskCount += 1;

            var taskStatus = reader.GetString(2);
            var isDepartmentPhaseTask = reader.GetBoolean(3);
            if (taskStatus.Equals("done", StringComparison.OrdinalIgnoreCase))
            {
                metadata.DoneTaskCount += 1;
            }
            else if (taskStatus.Equals("in_progress", StringComparison.OrdinalIgnoreCase))
            {
                metadata.InProgressTaskCount += 1;
            }
            else if (taskStatus.Equals("open", StringComparison.OrdinalIgnoreCase)
                || taskStatus.Equals("ready", StringComparison.OrdinalIgnoreCase)
                || taskStatus.Equals("blocked", StringComparison.OrdinalIgnoreCase))
            {
                metadata.OpenTaskCount += 1;
            }

            if (isDepartmentPhaseTask)
            {
                metadata.DepartmentTotalTaskCount += 1;

                if (taskStatus.Equals("done", StringComparison.OrdinalIgnoreCase))
                {
                    metadata.DepartmentDoneTaskCount += 1;
                }
                else if (taskStatus.Equals("in_progress", StringComparison.OrdinalIgnoreCase))
                {
                    metadata.DepartmentInProgressTaskCount += 1;
                }
                else if (taskStatus.Equals("open", StringComparison.OrdinalIgnoreCase)
                    || taskStatus.Equals("ready", StringComparison.OrdinalIgnoreCase)
                    || taskStatus.Equals("blocked", StringComparison.OrdinalIgnoreCase))
                {
                    metadata.DepartmentOpenTaskCount += 1;
                }
            }

            var option = BuildWorkflowResponsibilityOption(
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetString(6));
            metadata.ResponsibilityOptions[option.Value] = option;
        }

        return metadataByWorkflowId;
    }

    private static WorkflowTaskMetricsDto BuildWorkflowTaskMetrics(WorkflowListMetadata? metadata)
    {
        if (metadata is null)
        {
            return WorkflowSummaryBuilder.CreateEmptyTaskMetrics();
        }

        return new WorkflowTaskMetricsDto
        {
            Overall = WorkflowSummaryBuilder.CreateTaskCountSummary(
                metadata.TotalTaskCount,
                metadata.OpenTaskCount,
                metadata.InProgressTaskCount,
                metadata.DoneTaskCount),
            DepartmentPhase = WorkflowSummaryBuilder.CreateTaskCountSummary(
                metadata.DepartmentTotalTaskCount,
                metadata.DepartmentOpenTaskCount,
                metadata.DepartmentInProgressTaskCount,
                metadata.DepartmentDoneTaskCount)
        };
    }

    private static async Task<Dictionary<long, WorkflowRequirementSummaryDto>> LoadWorkflowRequirementSummaries(
        NpgsqlConnection connection,
        IReadOnlyList<long> workflowIds,
        IReadOnlyDictionary<int, AnswerDefinitionRecord> definitions)
    {
        var summariesByWorkflowId = workflowIds.ToDictionary(
            workflowId => workflowId,
            _ => WorkflowSummaryBuilder.CreateEmptyRequirementSummary());

        if (workflowIds.Count == 0 || definitions.Count == 0)
        {
            return summariesByWorkflowId;
        }

        var answersByWorkflowId = new Dictionary<long, Dictionary<string, StoredWorkflowAnswerRecord>>();

        const string answersSql = @"
SELECT
    a.workflow_id,
    a.id,
    a.answer_definition_id,
    d.answer_key,
    d.input_type,
    a.value_boolean,
    a.value_text,
    a.value_number,
    a.selected_option_id,
    selected_option.option_value
FROM workflow_answers a
JOIN workflow_answer_definitions d ON d.id = a.answer_definition_id
LEFT JOIN workflow_answer_options selected_option ON selected_option.id = a.selected_option_id
WHERE a.workflow_id = ANY(@workflowIds)
ORDER BY a.workflow_id, d.sort_order, d.id, a.id;";

        await using (var command = new NpgsqlCommand(answersSql, connection))
        {
            command.Parameters.Add("workflowIds", NpgsqlDbType.Array | NpgsqlDbType.Bigint).Value = workflowIds;
            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var workflowId = reader.GetInt64(0);
                if (!answersByWorkflowId.TryGetValue(workflowId, out var answersByKey))
                {
                    answersByKey = new Dictionary<string, StoredWorkflowAnswerRecord>(StringComparer.OrdinalIgnoreCase);
                    answersByWorkflowId[workflowId] = answersByKey;
                }

                var answerKey = reader.GetString(3);
                answersByKey[answerKey] = new StoredWorkflowAnswerRecord
                {
                    WorkflowAnswerId = reader.GetInt64(1),
                    AnswerDefinitionId = reader.GetInt32(2),
                    AnswerKey = answerKey,
                    InputType = reader.GetString(4),
                    ValueBoolean = reader.IsDBNull(5) ? null : reader.GetBoolean(5),
                    ValueText = reader.IsDBNull(6) ? null : reader.GetString(6),
                    ValueNumber = reader.IsDBNull(7) ? null : reader.GetDecimal(7),
                    SelectedOptionId = reader.IsDBNull(8) ? null : reader.GetInt32(8),
                    SelectedOptionValue = reader.IsDBNull(9) ? null : reader.GetString(9),
                    SelectedOptionIds = new List<int>(),
                    SelectedOptionValues = new List<string>()
                };
            }
        }

        const string multiSelectSql = @"
SELECT
    a.workflow_id,
    d.answer_key,
    o.id,
    o.option_value
FROM workflow_answers a
JOIN workflow_answer_definitions d ON d.id = a.answer_definition_id
JOIN workflow_answer_selected_options aso ON aso.workflow_answer_id = a.id
JOIN workflow_answer_options o ON o.id = aso.answer_option_id
WHERE a.workflow_id = ANY(@workflowIds)
ORDER BY a.workflow_id, d.sort_order, d.id, o.sort_order, o.id;";

        await using (var command = new NpgsqlCommand(multiSelectSql, connection))
        {
            command.Parameters.Add("workflowIds", NpgsqlDbType.Array | NpgsqlDbType.Bigint).Value = workflowIds;
            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var workflowId = reader.GetInt64(0);
                if (!answersByWorkflowId.TryGetValue(workflowId, out var answersByKey))
                {
                    continue;
                }

                var answerKey = reader.GetString(1);
                if (!answersByKey.TryGetValue(answerKey, out var answer))
                {
                    continue;
                }

                answer.SelectedOptionIds.Add(reader.GetInt32(2));
                answer.SelectedOptionValues.Add(reader.GetString(3));
            }
        }

        foreach (var workflowId in workflowIds)
        {
            answersByWorkflowId.TryGetValue(
                workflowId,
                out var answersByKey);

            summariesByWorkflowId[workflowId] = WorkflowSummaryBuilder.BuildRequirementSummary(
                definitions.Values,
                answersByKey ?? new Dictionary<string, StoredWorkflowAnswerRecord>(StringComparer.OrdinalIgnoreCase));
        }

        return summariesByWorkflowId;
    }

    private static WorkflowResponsibilityOptionDto BuildWorkflowResponsibilityOption(
        string? assignmentType,
        string? responsibilityKey,
        string? responsibilityName)
    {
        if (!string.IsNullOrWhiteSpace(responsibilityKey) && !string.IsNullOrWhiteSpace(responsibilityName))
        {
            return new WorkflowResponsibilityOptionDto
            {
                Value = responsibilityKey,
                Label = responsibilityName
            };
        }

        if (!string.IsNullOrWhiteSpace(responsibilityName))
        {
            return new WorkflowResponsibilityOptionDto
            {
                Value = responsibilityName,
                Label = responsibilityName
            };
        }

        if (string.Equals(assignmentType, "user", StringComparison.OrdinalIgnoreCase))
        {
            return new WorkflowResponsibilityOptionDto
            {
                Value = "__direct_user__",
                Label = "Direkt zugewiesen"
            };
        }

        if (string.IsNullOrWhiteSpace(assignmentType))
        {
            return new WorkflowResponsibilityOptionDto
            {
                Value = "__unassigned__",
                Label = "Nicht zugewiesen"
            };
        }

        return new WorkflowResponsibilityOptionDto
        {
            Value = "__unassigned__",
            Label = "Ohne Zuständigkeits-Zuordnung"
        };
    }
}
