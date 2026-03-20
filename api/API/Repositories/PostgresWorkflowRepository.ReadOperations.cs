using Npgsql;
using NpgsqlTypes;

namespace API;

internal sealed partial class PostgresWorkflowRepository
{
    private sealed class WorkflowListMetadata
    {
        public int TotalTaskCount { get; set; }
        public int OpenTaskCount { get; set; }
        public int InProgressTaskCount { get; set; }
        public int DoneTaskCount { get; set; }
        public int EndedTaskCount { get; set; }
        public int DepartmentTotalTaskCount { get; set; }
        public int DepartmentOpenTaskCount { get; set; }
        public int DepartmentInProgressTaskCount { get; set; }
        public int DepartmentDoneTaskCount { get; set; }
        public int DepartmentEndedTaskCount { get; set; }
        public Dictionary<string, WorkflowResponsibilityOptionDto> ResponsibilityOptions { get; } =
            new(StringComparer.Ordinal);
    }

    // Listen- und Detailabfragen fuer die Frontend-Uebersichten.
    public async Task<List<WorkflowListItemDto>> GetWorkflows()
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();

        const string sql = @"
SELECT
    w.id,
    w.uid,
    w.first_name,
    w.last_name,
    w.employee_number,
    w.badge_number,
    d.id,
    d.name,
    r.id,
    r.name,
    w.status,
    w.created_at,
    COUNT(n.id) FILTER (WHERE n.status = 'pending') AS pending_notifications,
    COUNT(n.id) FILTER (WHERE n.status = 'failed') AS failed_notifications
FROM workflows w
JOIN departments d ON d.id = w.department_id
JOIN app_roles r ON r.id = w.onboarding_role_id
LEFT JOIN workflow_notifications n ON n.workflow_id = w.id
GROUP BY w.id, d.id, d.name, r.id, r.name
ORDER BY w.created_at DESC;";

        var workflowRows = new List<(
            long WorkflowId,
            Guid Uid,
            string FirstName,
            string LastName,
            int EmployeeNumber,
            int BadgeNumber,
            int DepartmentId,
            string DepartmentName,
            int RoleId,
            string RoleName,
            string WorkflowStatus,
            DateTime CreatedAt,
            int PendingNotifications,
            int FailedNotifications)>();

        await using (var command = new NpgsqlCommand(sql, connection))
        {
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                workflowRows.Add((
                    reader.GetInt64(0),
                    reader.GetGuid(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.GetInt32(4),
                    reader.GetInt32(5),
                    reader.GetInt32(6),
                    reader.GetString(7),
                    reader.GetInt32(8),
                    reader.GetString(9),
                    reader.GetString(10),
                    reader.GetDateTime(11),
                    reader.GetInt32(12),
                    reader.GetInt32(13)));
            }
        }

        var workflowIds = workflowRows.Select(row => row.WorkflowId).ToArray();
        var definitions = await LoadAnswerDefinitionRecords(connection, null);
        var requirementSummariesByWorkflowId = await LoadWorkflowRequirementSummaries(connection, workflowIds, definitions);
        var metadataByWorkflowId = await LoadWorkflowListMetadata(connection, workflowIds);

        return workflowRows
            .Select(row =>
            {
                metadataByWorkflowId.TryGetValue(row.WorkflowId, out var metadata);
                requirementSummariesByWorkflowId.TryGetValue(row.WorkflowId, out var requirementSummary);
                var workflowStatus = row.WorkflowStatus;
                var taskMetrics = BuildWorkflowTaskMetrics(metadata);

                return new WorkflowListItemDto
                {
                    Uid = row.Uid,
                    FirstName = row.FirstName,
                    LastName = row.LastName,
                    EmployeeNumber = row.EmployeeNumber,
                    BadgeNumber = row.BadgeNumber,
                    DepartmentId = row.DepartmentId,
                    DepartmentName = row.DepartmentName,
                    RoleId = row.RoleId,
                    RoleName = row.RoleName,
                    Status = WorkflowStatusRules.ToLegacyStatus(workflowStatus),
                    WorkflowStatus = workflowStatus,
                    CreatedAt = row.CreatedAt,
                    PendingNotifications = row.PendingNotifications,
                    FailedNotifications = row.FailedNotifications,
                    RequirementSummary = requirementSummary ?? WorkflowSummaryBuilder.CreateEmptyRequirementSummary(),
                    TaskMetrics = taskMetrics,
                    TaskSummary = WorkflowSummaryBuilder.BuildTaskSummaryText(taskMetrics),
                    ResponsibilityOptions = metadata?.ResponsibilityOptions.Values
                        .OrderBy(option => option.Label, StringComparer.CurrentCultureIgnoreCase)
                        .ToList()
                        ?? new List<WorkflowResponsibilityOptionDto>()
                };
            })
            .ToList();
    }

    public async Task<WorkflowDetailDto?> GetWorkflowByUid(Guid workflowUid)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();

        const string workflowSql = @"
SELECT
    w.id,
    w.uid,
    w.first_name,
    w.last_name,
    w.employee_number,
    w.badge_number,
    d.id,
    d.name,
    r.id,
    r.name,
    w.status,
    w.created_at
FROM workflows w
JOIN departments d ON d.id = w.department_id
JOIN app_roles r ON r.id = w.onboarding_role_id
WHERE w.uid = @uid
LIMIT 1;";

        long workflowId;
        WorkflowDetailDto workflow;

        await using (var workflowCommand = new NpgsqlCommand(workflowSql, connection))
        {
            workflowCommand.Parameters.AddWithValue("uid", workflowUid);
            await using var reader = await workflowCommand.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
            {
                return null;
            }

            workflowId = reader.GetInt64(0);
            var workflowStatus = reader.GetString(10);

            workflow = new WorkflowDetailDto
            {
                Uid = reader.GetGuid(1),
                FirstName = reader.GetString(2),
                LastName = reader.GetString(3),
                EmployeeNumber = reader.GetInt32(4),
                BadgeNumber = reader.GetInt32(5),
                DepartmentId = reader.GetInt32(6),
                DepartmentName = reader.GetString(7),
                RoleId = reader.GetInt32(8),
                RoleName = reader.GetString(9),
                Status = WorkflowStatusRules.ToLegacyStatus(workflowStatus),
                WorkflowStatus = workflowStatus,
                CreatedAt = reader.GetDateTime(11),
                Requirements = new List<WorkflowRequirementSnapshotDto>(),
                RequirementSummary = WorkflowSummaryBuilder.CreateEmptyRequirementSummary(),
                Tasks = new List<WorkflowTaskDto>(),
                TaskMetrics = WorkflowSummaryBuilder.CreateEmptyTaskMetrics(),
                TaskAreas = new List<WorkflowTaskAreaSummaryDto>(),
                Notifications = new List<WorkflowNotificationDto>()
            };
        }

        await LoadWorkflowRequirements(connection, workflowId, workflow.Requirements);
        await LoadWorkflowTasks(connection, workflowId, workflow.Tasks);
        await LoadWorkflowNotifications(connection, workflowId, workflow.Notifications);
        workflow.RequirementSummary = WorkflowSummaryBuilder.BuildRequirementSummary(workflow.Requirements);
        workflow.TaskMetrics = WorkflowSummaryBuilder.BuildTaskMetrics(workflow.Tasks);
        workflow.TaskAreas = WorkflowSummaryBuilder.BuildTaskAreaSummaries(workflow.Tasks);

        return workflow;
    }

    // Aufgaben werden fuer Fachbereiche immer im Kontext ihres Workflows geladen.
    public async Task<HashSet<int>> GetRequirementSelectionDepartmentIds(long userId)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();

        const string sql = @"
WITH explicit_departments AS (
    SELECT ds.department_id
    FROM department_settings ds
    LEFT JOIN people requirement_person ON requirement_person.id = ds.requirement_approver_person_id
    LEFT JOIN people lead_person ON lead_person.id = ds.department_lead_person_id
    WHERE requirement_person.app_user_id = @userId
       OR (requirement_person.app_user_id IS NULL AND lead_person.app_user_id = @userId)
),
fallback_departments AS (
    SELECT r.department_id
    FROM app_user_responsibilities ur
    JOIN app_responsibilities r ON r.id = ur.app_responsibility_id
    WHERE ur.app_user_id = @userId
      AND r.responsibility_type = 'department_lead'
      AND r.department_id IS NOT NULL
      AND r.is_active = TRUE

    UNION

    SELECT r.department_id
    FROM app_user_groups ug
    JOIN app_group_responsibilities gr ON gr.app_group_id = ug.app_group_id
    JOIN app_responsibilities r ON r.id = gr.app_responsibility_id
    WHERE ug.app_user_id = @userId
      AND r.responsibility_type = 'department_lead'
      AND r.department_id IS NOT NULL
      AND r.is_active = TRUE
)
SELECT department_id FROM explicit_departments
UNION
SELECT department_id FROM fallback_departments;";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("userId", userId);

        await using var reader = await command.ExecuteReaderAsync();
        var departmentIds = new HashSet<int>();
        while (await reader.ReadAsync())
        {
            departmentIds.Add(reader.GetInt32(0));
        }

        return departmentIds;
    }

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
WHERE wt.workflow_id = ANY(@workflowIds)
  AND wt.task_key <> @legacyTaskKey;";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.Add("workflowIds", NpgsqlDbType.Array | NpgsqlDbType.Bigint).Value = workflowIds;
        command.Parameters.AddWithValue("legacyTaskKey", LegacySupervisorHandoverTaskKey);

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
            else if (taskStatus.Equals("skipped", StringComparison.OrdinalIgnoreCase))
            {
                metadata.EndedTaskCount += 1;
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
                else if (taskStatus.Equals("skipped", StringComparison.OrdinalIgnoreCase))
                {
                    metadata.DepartmentEndedTaskCount += 1;
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
                metadata.DoneTaskCount,
                metadata.EndedTaskCount),
            DepartmentPhase = WorkflowSummaryBuilder.CreateTaskCountSummary(
                metadata.DepartmentTotalTaskCount,
                metadata.DepartmentOpenTaskCount,
                metadata.DepartmentInProgressTaskCount,
                metadata.DepartmentDoneTaskCount,
                metadata.DepartmentEndedTaskCount)
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

    private static async Task<List<TaskWithWorkflowDto>> LoadTasks(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        long? taskId)
    {
        const string sql = @"
SELECT
    t.id,
    t.task_template_id,
    t.task_key,
    t.title,
    t.description,
    t.category,
    t.icon_key,
    t.status,
    t.is_required,
    t.sort_order,
    t.created_at,
    t.ready_at,
    t.started_at,
    t.completed_at,
    t.cancelled_at,
    w.id,
    w.uid,
    w.status,
    w.created_at,
    w.first_name,
    w.last_name,
    w.employee_number,
    w.badge_number,
    d.id,
    d.name,
    r.id,
    r.name,
    COALESCE(t.process_area_label, tt.process_area_label),
    CASE
        WHEN t.task_template_id IS NULL AND tt.id IS NOT NULL THEN tt.is_department_phase_task
        ELSE t.is_department_phase_task
    END,
    template_department.name,
    template_responsibility.responsibility_type
FROM workflow_tasks t
JOIN workflows w ON w.id = t.workflow_id
JOIN departments d ON d.id = w.department_id
JOIN app_roles r ON r.id = w.onboarding_role_id
LEFT JOIN task_templates tt
    ON tt.id = t.task_template_id
    OR (t.task_template_id IS NULL AND tt.template_key = t.task_key)
LEFT JOIN app_responsibilities template_responsibility ON template_responsibility.id = tt.default_responsibility_id
LEFT JOIN departments template_department ON template_department.id = template_responsibility.department_id
WHERE (@taskId IS NULL OR t.id = @taskId)
  AND t.task_key <> @legacyTaskKey
ORDER BY w.created_at DESC, t.sort_order, t.id;";

        var tasks = new List<TaskWithWorkflowDto>();
        var taskById = new Dictionary<long, WorkflowTaskDto>();

        await using (var command = new NpgsqlCommand(sql, connection, transaction))
        {
            command.Parameters.Add("taskId", NpgsqlDbType.Bigint).Value = (object?)taskId ?? DBNull.Value;
            command.Parameters.AddWithValue("legacyTaskKey", LegacySupervisorHandoverTaskKey);
            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var task = new WorkflowTaskDto
                {
                    Id = reader.GetInt64(0),
                    TaskTemplateId = reader.IsDBNull(1) ? null : reader.GetInt32(1),
                    TaskKey = reader.GetString(2),
                    Title = reader.GetString(3),
                    Description = reader.GetString(4),
                    Category = reader.GetString(5),
                    IconKey = reader.GetString(6),
                    Status = reader.GetString(7),
                    IsRequired = reader.GetBoolean(8),
                    SortOrder = reader.GetInt32(9),
                    CreatedAt = reader.GetDateTime(10),
                    ReadyAt = reader.IsDBNull(11) ? null : reader.GetDateTime(11),
                    StartedAt = reader.IsDBNull(12) ? null : reader.GetDateTime(12),
                    CompletedAt = reader.IsDBNull(13) ? null : reader.GetDateTime(13),
                    CancelledAt = reader.IsDBNull(14) ? null : reader.GetDateTime(14),
                    ProcessArea = ResolveTaskProcessArea(
                        reader.IsDBNull(27) ? null : reader.GetString(27),
                        reader.IsDBNull(29) ? null : reader.GetString(29),
                        reader.IsDBNull(30) ? null : reader.GetString(30)),
                    IsDepartmentPhaseTask = reader.GetBoolean(28),
                    Assignments = new List<WorkflowTaskAssignmentDto>(),
                    Dependencies = new List<WorkflowTaskDependencyDto>()
                };

                taskById[task.Id] = task;

                var workflowStatus = reader.GetString(17);
                tasks.Add(new TaskWithWorkflowDto
                {
                    Task = task,
                    Workflow = new TaskWorkflowContextDto
                    {
                        WorkflowId = reader.GetInt64(15),
                        WorkflowUid = reader.GetGuid(16),
                        WorkflowStatus = workflowStatus,
                        WorkflowLegacyStatus = WorkflowStatusRules.ToLegacyStatus(workflowStatus),
                        WorkflowCreatedAt = reader.GetDateTime(18),
                        FirstName = reader.GetString(19),
                        LastName = reader.GetString(20),
                        EmployeeNumber = reader.GetInt32(21),
                        BadgeNumber = reader.GetInt32(22),
                        DepartmentId = reader.GetInt32(23),
                        DepartmentName = reader.GetString(24),
                        RoleId = reader.GetInt32(25),
                        RoleName = reader.GetString(26)
                    }
                });
            }
        }

        await LoadTaskAssignmentsAndDependencies(connection, transaction, taskById);
        return tasks;
    }

    private static async Task LoadTaskAssignmentsAndDependencies(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        IReadOnlyDictionary<long, WorkflowTaskDto> taskById)
    {
        if (taskById.Count == 0)
        {
            return;
        }

        var taskIds = taskById.Keys.ToArray();

        const string assignmentSql = @"
SELECT
    ta.workflow_task_id,
    ta.id,
    ta.assignment_type,
    ta.is_primary,
    ta.assigned_at,
    ta.completed_at,
    ta.assignee_user_id,
    u.display_name,
    u.email,
    ta.assignee_responsibility_id,
    r.responsibility_key,
    r.name,
    r.responsibility_type,
    d.name
FROM task_assignments ta
LEFT JOIN app_users u ON u.id = ta.assignee_user_id
LEFT JOIN app_responsibilities r ON r.id = ta.assignee_responsibility_id
LEFT JOIN departments d ON d.id = r.department_id
WHERE ta.workflow_task_id = ANY(@taskIds)
ORDER BY ta.workflow_task_id, ta.is_primary DESC, ta.id;";

        await using (var assignmentCommand = new NpgsqlCommand(assignmentSql, connection, transaction))
        {
            assignmentCommand.Parameters.Add("taskIds", NpgsqlDbType.Array | NpgsqlDbType.Bigint).Value = taskIds;
            await using var reader = await assignmentCommand.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var workflowTaskId = reader.GetInt64(0);
                if (!taskById.TryGetValue(workflowTaskId, out var task))
                {
                    continue;
                }

                task.Assignments.Add(new WorkflowTaskAssignmentDto
                {
                    Id = reader.GetInt64(1),
                    AssignmentType = reader.GetString(2),
                    IsPrimary = reader.GetBoolean(3),
                    AssignedAt = reader.GetDateTime(4),
                    CompletedAt = reader.IsDBNull(5) ? null : reader.GetDateTime(5),
                    AssigneeUserId = reader.IsDBNull(6) ? null : reader.GetInt64(6),
                    AssigneeUserName = reader.IsDBNull(7) ? null : reader.GetString(7),
                    AssigneeUserEmail = reader.IsDBNull(8) ? null : reader.GetString(8),
                    AssigneeResponsibilityId = reader.IsDBNull(9) ? null : reader.GetInt32(9),
                    AssigneeResponsibilityKey = reader.IsDBNull(10) ? null : reader.GetString(10),
                    AssigneeResponsibilityName = reader.IsDBNull(11) ? null : reader.GetString(11),
                    AssigneeResponsibilityType = reader.IsDBNull(12) ? null : reader.GetString(12)
                });

                if (reader.GetBoolean(3) && string.IsNullOrWhiteSpace(task.ProcessArea))
                {
                    var resolvedArea = ResolveTaskProcessArea(
                        null,
                        reader.IsDBNull(13) ? null : reader.GetString(13),
                        reader.IsDBNull(12) ? null : reader.GetString(12));
                    if (!string.IsNullOrWhiteSpace(resolvedArea))
                    {
                        task.ProcessArea = resolvedArea;
                    }
                }
            }
        }

        const string dependencySql = @"
SELECT
    d.workflow_task_id,
    d.depends_on_workflow_task_id,
    d.required_status,
    wt.task_key,
    wt.title
FROM workflow_task_dependencies d
JOIN workflow_tasks wt ON wt.id = d.depends_on_workflow_task_id
WHERE d.workflow_task_id = ANY(@taskIds)
  AND wt.task_key <> @legacyTaskKey
ORDER BY d.workflow_task_id, d.id;";

        await using (var dependencyCommand = new NpgsqlCommand(dependencySql, connection, transaction))
        {
            dependencyCommand.Parameters.Add("taskIds", NpgsqlDbType.Array | NpgsqlDbType.Bigint).Value = taskIds;
            dependencyCommand.Parameters.AddWithValue("legacyTaskKey", LegacySupervisorHandoverTaskKey);
            await using var reader = await dependencyCommand.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var workflowTaskId = reader.GetInt64(0);
                if (!taskById.TryGetValue(workflowTaskId, out var task))
                {
                    continue;
                }

                task.Dependencies.Add(new WorkflowTaskDependencyDto
                {
                    WorkflowTaskId = workflowTaskId,
                    DependsOnWorkflowTaskId = reader.GetInt64(1),
                    RequiredStatus = reader.GetString(2),
                    DependsOnTaskKey = reader.GetString(3),
                    DependsOnTitle = reader.GetString(4)
                });
            }
        }
    }

    private static async Task LoadWorkflowTasks(
        NpgsqlConnection connection,
        long workflowId,
        List<WorkflowTaskDto> tasks)
    {
        const string taskSql = @"
SELECT
    wt.id,
    wt.task_template_id,
    wt.task_key,
    wt.title,
    wt.description,
    wt.category,
    wt.icon_key,
    wt.status,
    wt.is_required,
    wt.sort_order,
    wt.created_at,
    wt.ready_at,
    wt.started_at,
    wt.completed_at,
    wt.cancelled_at,
    COALESCE(wt.process_area_label, tt.process_area_label),
    CASE
        WHEN wt.task_template_id IS NULL AND tt.id IS NOT NULL THEN tt.is_department_phase_task
        ELSE wt.is_department_phase_task
    END,
    template_department.name,
    template_responsibility.responsibility_type
FROM workflow_tasks wt
LEFT JOIN task_templates tt
    ON tt.id = wt.task_template_id
    OR (wt.task_template_id IS NULL AND tt.template_key = wt.task_key)
LEFT JOIN app_responsibilities template_responsibility ON template_responsibility.id = tt.default_responsibility_id
LEFT JOIN departments template_department ON template_department.id = template_responsibility.department_id
WHERE wt.workflow_id = @workflowId
  AND wt.task_key <> @legacyTaskKey
ORDER BY wt.sort_order, wt.id;";

        var taskById = new Dictionary<long, WorkflowTaskDto>();

        await using (var command = new NpgsqlCommand(taskSql, connection))
        {
            command.Parameters.AddWithValue("workflowId", workflowId);
            command.Parameters.AddWithValue("legacyTaskKey", LegacySupervisorHandoverTaskKey);
            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var task = new WorkflowTaskDto
                {
                    Id = reader.GetInt64(0),
                    TaskTemplateId = reader.IsDBNull(1) ? null : reader.GetInt32(1),
                    TaskKey = reader.GetString(2),
                    Title = reader.GetString(3),
                    Description = reader.GetString(4),
                    Category = reader.GetString(5),
                    IconKey = reader.GetString(6),
                    Status = reader.GetString(7),
                    IsRequired = reader.GetBoolean(8),
                    SortOrder = reader.GetInt32(9),
                    CreatedAt = reader.GetDateTime(10),
                    ReadyAt = reader.IsDBNull(11) ? null : reader.GetDateTime(11),
                    StartedAt = reader.IsDBNull(12) ? null : reader.GetDateTime(12),
                    CompletedAt = reader.IsDBNull(13) ? null : reader.GetDateTime(13),
                    CancelledAt = reader.IsDBNull(14) ? null : reader.GetDateTime(14),
                    ProcessArea = ResolveTaskProcessArea(
                        reader.IsDBNull(15) ? null : reader.GetString(15),
                        reader.IsDBNull(17) ? null : reader.GetString(17),
                        reader.IsDBNull(18) ? null : reader.GetString(18)),
                    IsDepartmentPhaseTask = reader.GetBoolean(16),
                    Assignments = new List<WorkflowTaskAssignmentDto>(),
                    Dependencies = new List<WorkflowTaskDependencyDto>()
                };

                taskById[task.Id] = task;
                tasks.Add(task);
            }
        }

        if (tasks.Count == 0)
        {
            return;
        }

        await LoadTaskAssignmentsAndDependencies(connection, null, taskById);
    }

    private static async Task LoadWorkflowNotifications(
        NpgsqlConnection connection,
        long workflowId,
        List<WorkflowNotificationDto> notifications)
    {
        const string sql = @"
SELECT
    id,
    target_name,
    target_email,
    notification_type,
    status,
    attempts,
    recipient_user_id,
    last_error,
    created_at,
    sent_at
FROM workflow_notifications
WHERE workflow_id = @workflowId
ORDER BY created_at, id;";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("workflowId", workflowId);

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            notifications.Add(new WorkflowNotificationDto
            {
                Id = reader.GetInt64(0),
                TargetName = reader.GetString(1),
                TargetEmail = reader.GetString(2),
                NotificationType = reader.GetString(3),
                Status = reader.GetString(4),
                Attempts = reader.GetInt32(5),
                RecipientUserId = reader.IsDBNull(6) ? null : reader.GetInt64(6),
                LastError = reader.IsDBNull(7) ? null : reader.GetString(7),
                CreatedAt = reader.GetDateTime(8),
                SentAt = reader.IsDBNull(9) ? null : reader.GetDateTime(9)
            });
        }
    }
}
