using Npgsql;
using NpgsqlTypes;

namespace API;

internal sealed partial class PostgresWorkflowRepository
{
    private static async Task<List<TaskWithWorkflowDto>> LoadTasks(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        long? taskId)
    {
        const string sql = @"
SELECT
    t.id,
    t.node_instance_id,
    t.task_template_id,
    t.task_key,
    CASE
        WHEN t.node_instance_id IS NOT NULL THEN COALESCE(runtime_node.node_type = 'approval', FALSE)
        WHEN pt.approval_task_template_key IS NULL THEN FALSE
        ELSE t.task_key = pt.approval_task_template_key
    END,
    t.node_instance_id IS NOT NULL,
    t.title,
    t.description,
    t.category,
    t.icon_key,
    t.status,
    t.is_required,
    t.due_in_days,
    t.due_at,
    t.sort_order,
    t.created_at,
    t.ready_at,
    t.started_at,
    t.completed_at,
    w.id,
    w.uid,
    w.status,
    w.deadline_date,
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
    template_responsibility.responsibility_type,
    template_responsibility.responsibility_key,
    template_responsibility.name
FROM workflow_tasks t
JOIN workflows w ON w.id = t.workflow_id
JOIN process_types pt ON pt.id = w.process_type_id
JOIN departments d ON d.id = w.department_id
JOIN app_roles r ON r.id = w.position_role_id
LEFT JOIN task_templates tt
    ON tt.id = t.task_template_id
    OR (t.task_template_id IS NULL AND tt.template_key = t.task_key)
LEFT JOIN workflow_node_instances runtime_node_instance ON runtime_node_instance.id = t.node_instance_id
LEFT JOIN workflow_nodes runtime_node ON runtime_node.id = runtime_node_instance.workflow_node_id
LEFT JOIN app_responsibilities template_responsibility ON template_responsibility.id = tt.default_responsibility_id
LEFT JOIN departments template_department ON template_department.id = template_responsibility.department_id
WHERE (@taskId IS NULL OR t.id = @taskId)
ORDER BY w.created_at DESC, t.sort_order, t.id;";

        var tasks = new List<TaskWithWorkflowDto>();
        var taskById = new Dictionary<long, WorkflowTaskDto>();

        await using (var command = new NpgsqlCommand(sql, connection, transaction))
        {
            command.Parameters.Add("taskId", NpgsqlDbType.Bigint).Value = (object?)taskId ?? DBNull.Value;
            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var task = new WorkflowTaskDto
                {
                    Id = reader.GetInt64(0),
                    NodeInstanceId = reader.IsDBNull(1) ? null : reader.GetInt64(1),
                    TaskTemplateId = reader.IsDBNull(2) ? null : reader.GetInt32(2),
                    TaskKey = reader.GetString(3),
                    IsApprovalTask = reader.GetBoolean(4),
                    IsRuntimeNodeTask = reader.GetBoolean(5),
                    Title = reader.GetString(6),
                    Description = reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
                    Category = reader.GetString(8),
                    IconKey = NormalizeAdminTaskTemplateIconKey(reader.IsDBNull(9) ? null : reader.GetString(9)),
                    Status = reader.GetString(10),
                    IsRequired = reader.GetBoolean(11),
                    DueInDays = reader.IsDBNull(12) ? null : reader.GetInt32(12),
                    DueAt = ResolveEffectiveTaskDueAt(
                        reader.IsDBNull(22) ? null : reader.GetFieldValue<DateOnly>(22),
                        reader.IsDBNull(13) ? null : reader.GetDateTime(13)),
                    SlaStatus = ResolveTaskSlaStatus(
                        reader.GetString(10),
                        ResolveEffectiveTaskDueAt(
                            reader.IsDBNull(22) ? null : reader.GetFieldValue<DateOnly>(22),
                            reader.IsDBNull(13) ? null : reader.GetDateTime(13))),
                    SortOrder = reader.GetInt32(14),
                    CreatedAt = reader.GetDateTime(15),
                    ReadyAt = reader.IsDBNull(16) ? null : reader.GetDateTime(16),
                    StartedAt = reader.IsDBNull(17) ? null : reader.GetDateTime(17),
                    CompletedAt = reader.IsDBNull(18) ? null : reader.GetDateTime(18),
                    ProcessArea = ResolveTaskProcessArea(
                        reader.IsDBNull(32) ? null : reader.GetString(32),
                        reader.IsDBNull(34) ? null : reader.GetString(34),
                        reader.IsDBNull(35) ? null : reader.GetString(35),
                        reader.IsDBNull(36) ? null : reader.GetString(36),
                        reader.IsDBNull(37) ? null : reader.GetString(37)),
                    IsDepartmentPhaseTask = reader.GetBoolean(33),
                    CanAddComment = false,
                    CanDecideApproval = false,
                    Assignments = new List<WorkflowTaskAssignmentDto>(),
                    Dependencies = new List<WorkflowTaskDependencyDto>(),
                    Comments = new List<WorkflowTaskCommentDto>()
                };

                taskById[task.Id] = task;

                var workflowStatus = reader.GetString(21);
                tasks.Add(new TaskWithWorkflowDto
                {
                    Task = task,
                    Workflow = new TaskWorkflowContextDto
                    {
                        WorkflowId = reader.GetInt64(19),
                        WorkflowUid = reader.GetGuid(20),
                        WorkflowStatus = workflowStatus,
                        WorkflowLegacyStatus = WorkflowStatusRules.ToLegacyStatus(workflowStatus),
                        WorkflowCreatedAt = reader.GetDateTime(23),
                        FirstName = reader.GetString(24),
                        LastName = reader.GetString(25),
                        EmployeeNumber = reader.GetInt32(26),
                        BadgeNumber = reader.GetInt32(27),
                        DepartmentId = reader.GetInt32(28),
                        DepartmentName = reader.GetString(29),
                        RoleId = reader.GetInt32(30),
                        RoleName = reader.GetString(31)
                    }
                });
            }
        }

        await LoadTaskAssignmentsAndDependencies(connection, transaction, taskById);
        await LoadTaskComments(connection, transaction, taskById);
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
                        reader.IsDBNull(12) ? null : reader.GetString(12),
                        reader.IsDBNull(10) ? null : reader.GetString(10),
                        reader.IsDBNull(11) ? null : reader.GetString(11));
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
ORDER BY d.workflow_task_id, d.id;";

        await using (var dependencyCommand = new NpgsqlCommand(dependencySql, connection, transaction))
        {
            dependencyCommand.Parameters.Add("taskIds", NpgsqlDbType.Array | NpgsqlDbType.Bigint).Value = taskIds;
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

    private static async Task LoadTaskComments(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        IReadOnlyDictionary<long, WorkflowTaskDto> taskById)
    {
        if (taskById.Count == 0)
        {
            return;
        }

        const string sql = @"
SELECT
    c.id,
    c.workflow_task_id,
    c.author_user_id,
    u.display_name,
    c.comment_text,
    c.created_at
FROM workflow_task_comments c
LEFT JOIN app_users u ON u.id = c.author_user_id
WHERE c.workflow_task_id = ANY(@taskIds)
ORDER BY c.workflow_task_id, c.created_at DESC, c.id DESC;";

        var taskIds = taskById.Keys.ToArray();

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.Add("taskIds", NpgsqlDbType.Array | NpgsqlDbType.Bigint).Value = taskIds;
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var workflowTaskId = reader.GetInt64(1);
            if (!taskById.TryGetValue(workflowTaskId, out var task))
            {
                continue;
            }

            task.Comments.Add(new WorkflowTaskCommentDto
            {
                Id = reader.GetInt64(0),
                TaskId = workflowTaskId,
                AuthorUserId = reader.IsDBNull(2) ? null : reader.GetInt64(2),
                AuthorUserName = reader.IsDBNull(3) ? null : reader.GetString(3),
                CommentText = reader.GetString(4),
                CreatedAt = reader.GetDateTime(5)
            });
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
    wt.node_instance_id,
    wt.task_template_id,
    wt.task_key,
    CASE
        WHEN wt.node_instance_id IS NOT NULL THEN COALESCE(runtime_node.node_type = 'approval', FALSE)
        WHEN pt.approval_task_template_key IS NULL THEN FALSE
        ELSE wt.task_key = pt.approval_task_template_key
    END,
    wt.node_instance_id IS NOT NULL,
    wt.title,
    wt.description,
    wt.category,
    wt.icon_key,
    wt.status,
    wt.is_required,
    wt.due_in_days,
    wt.due_at,
    wt.sort_order,
    wt.created_at,
    wt.ready_at,
    wt.started_at,
    wt.completed_at,
    w.deadline_date,
    COALESCE(wt.process_area_label, tt.process_area_label),
    CASE
        WHEN wt.task_template_id IS NULL AND tt.id IS NOT NULL THEN tt.is_department_phase_task
        ELSE wt.is_department_phase_task
    END,
    template_department.name,
    template_responsibility.responsibility_type,
    template_responsibility.responsibility_key,
    template_responsibility.name
FROM workflow_tasks wt
JOIN workflows w ON w.id = wt.workflow_id
JOIN process_types pt ON pt.id = w.process_type_id
LEFT JOIN task_templates tt
    ON tt.id = wt.task_template_id
    OR (wt.task_template_id IS NULL AND tt.template_key = wt.task_key)
LEFT JOIN workflow_node_instances runtime_node_instance ON runtime_node_instance.id = wt.node_instance_id
LEFT JOIN workflow_nodes runtime_node ON runtime_node.id = runtime_node_instance.workflow_node_id
LEFT JOIN app_responsibilities template_responsibility ON template_responsibility.id = tt.default_responsibility_id
LEFT JOIN departments template_department ON template_department.id = template_responsibility.department_id
WHERE wt.workflow_id = @workflowId
ORDER BY wt.sort_order, wt.id;";

        var taskById = new Dictionary<long, WorkflowTaskDto>();

        await using (var command = new NpgsqlCommand(taskSql, connection))
        {
            command.Parameters.AddWithValue("workflowId", workflowId);
            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var task = new WorkflowTaskDto
                {
                    Id = reader.GetInt64(0),
                    NodeInstanceId = reader.IsDBNull(1) ? null : reader.GetInt64(1),
                    TaskTemplateId = reader.IsDBNull(2) ? null : reader.GetInt32(2),
                    TaskKey = reader.GetString(3),
                    IsApprovalTask = reader.GetBoolean(4),
                    IsRuntimeNodeTask = reader.GetBoolean(5),
                    Title = reader.GetString(6),
                    Description = reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
                    Category = reader.GetString(8),
                    IconKey = NormalizeAdminTaskTemplateIconKey(reader.IsDBNull(9) ? null : reader.GetString(9)),
                    Status = reader.GetString(10),
                    IsRequired = reader.GetBoolean(11),
                    DueInDays = reader.IsDBNull(12) ? null : reader.GetInt32(12),
                    DueAt = ResolveEffectiveTaskDueAt(
                        reader.IsDBNull(19) ? null : reader.GetFieldValue<DateOnly>(19),
                        reader.IsDBNull(13) ? null : reader.GetDateTime(13)),
                    SlaStatus = ResolveTaskSlaStatus(
                        reader.GetString(10),
                        ResolveEffectiveTaskDueAt(
                            reader.IsDBNull(19) ? null : reader.GetFieldValue<DateOnly>(19),
                            reader.IsDBNull(13) ? null : reader.GetDateTime(13))),
                    SortOrder = reader.GetInt32(14),
                    CreatedAt = reader.GetDateTime(15),
                    ReadyAt = reader.IsDBNull(16) ? null : reader.GetDateTime(16),
                    StartedAt = reader.IsDBNull(17) ? null : reader.GetDateTime(17),
                    CompletedAt = reader.IsDBNull(18) ? null : reader.GetDateTime(18),
                    ProcessArea = ResolveTaskProcessArea(
                        reader.IsDBNull(20) ? null : reader.GetString(20),
                        reader.IsDBNull(22) ? null : reader.GetString(22),
                        reader.IsDBNull(23) ? null : reader.GetString(23),
                        reader.IsDBNull(24) ? null : reader.GetString(24),
                        reader.IsDBNull(25) ? null : reader.GetString(25)),
                    IsDepartmentPhaseTask = reader.GetBoolean(21),
                    CanAddComment = false,
                    CanDecideApproval = false,
                    Assignments = new List<WorkflowTaskAssignmentDto>(),
                    Dependencies = new List<WorkflowTaskDependencyDto>(),
                    Comments = new List<WorkflowTaskCommentDto>()
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
        await LoadTaskComments(connection, null, taskById);
    }

    private static string ResolveTaskSlaStatus(string taskStatus, DateTime? dueAt)
    {
        return TaskDueDateRules.ResolveSlaStatus(taskStatus, dueAt, TaskStatusRules.TerminalTaskStatuses);
    }

    private static DateTime? ResolveEffectiveTaskDueAt(DateOnly? workflowDeadlineDate, DateTime? taskDueAt)
    {
        return TaskDueDateRules.ResolveEffectiveDueAt(workflowDeadlineDate, taskDueAt);
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

    public async Task<PersonWorkflowHistoryDto?> GetPersonWorkflowHistory(long personId)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();

        const string sql = @"
SELECT
    p.id,
    COALESCE(
        NULLIF(BTRIM(CONCAT_WS(' ', COALESCE(p.first_name, latest.first_name), COALESCE(p.last_name, latest.last_name))), ''),
        u.display_name,
        'Person #' || p.id::text
    ) AS display_name,
    COALESCE(latest.department_id, p.department_id, u.department_id) AS resolved_department_id,
    d.name AS department_name,
    COALESCE(latest.employee_number, p.employee_number) AS employee_number,
    COALESCE(latest.badge_number, p.badge_number) AS badge_number,
    COALESCE(p.first_name, latest.first_name) AS first_name,
    COALESCE(p.last_name, latest.last_name) AS last_name,
    w.uid,
    pt.key,
    pt.name,
    pt.requires_target_person,
    w.first_name,
    w.last_name,
    r.name,
    w_dept.name,
    w.status,
    w.created_at,
    w.completed_at,
    w.archived_at,
    wd.definition_key,
    COALESCE(NULLIF(BTRIM(v.name), ''), wd.name),
    COALESCE(vpt.requires_target_person, pt.requires_target_person)
FROM people p
LEFT JOIN app_users u ON u.id = p.app_user_id
LEFT JOIN LATERAL (
    SELECT
        wl.department_id,
        wl.employee_number,
        wl.badge_number,
        wl.first_name,
        wl.last_name
    FROM workflows wl
    WHERE (
            wl.target_person_id = p.id
            OR (p.employee_number IS NOT NULL AND wl.employee_number = p.employee_number)
      )
    ORDER BY wl.created_at DESC
    LIMIT 1
) latest ON TRUE
LEFT JOIN departments d ON d.id = COALESCE(latest.department_id, p.department_id, u.department_id)
LEFT JOIN workflows w
    ON (
        w.target_person_id = p.id
        OR (p.employee_number IS NOT NULL AND w.employee_number = p.employee_number)
   )
LEFT JOIN process_types pt ON pt.id = w.process_type_id
LEFT JOIN workflow_definition_versions v ON v.id = w.workflow_definition_version_id
LEFT JOIN workflow_definitions wd ON wd.id = v.workflow_definition_id
LEFT JOIN process_types vpt ON vpt.id = v.primary_legacy_process_type_id
LEFT JOIN app_roles r ON r.id = w.position_role_id
LEFT JOIN departments w_dept ON w_dept.id = w.department_id
WHERE p.id = @personId
ORDER BY w.created_at DESC NULLS LAST;";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("personId", personId);

        await using var reader = await command.ExecuteReaderAsync();

        PersonWorkflowHistoryDto? result = null;
        var workflows = new List<PersonWorkflowSummaryDto>();

        while (await reader.ReadAsync())
        {
            if (result is null)
            {
                result = new PersonWorkflowHistoryDto
                {
                    PersonId = reader.GetInt64(0),
                    DisplayName = reader.GetString(1),
                    DepartmentId = reader.IsDBNull(2) ? null : reader.GetInt32(2),
                    DepartmentName = reader.IsDBNull(3) ? null : reader.GetString(3),
                    EmployeeNumber = reader.IsDBNull(4) ? null : reader.GetInt32(4),
                    BadgeNumber = reader.IsDBNull(5) ? null : reader.GetInt32(5),
                    FirstName = reader.IsDBNull(6) ? null : reader.GetString(6),
                    LastName = reader.IsDBNull(7) ? null : reader.GetString(7),
                    Workflows = workflows
                };
            }

            // A person may have no workflows yet — skip the null workflow row.
            if (reader.IsDBNull(8))
            {
                continue;
            }

            var workflowStatus = reader.GetString(16);
            workflows.Add(new PersonWorkflowSummaryDto
            {
                Uid = reader.GetGuid(8),
                ProcessType = new WorkflowProcessTypeDto
                {
                    Key = reader.IsDBNull(20) ? reader.GetString(9) : reader.GetString(20),
                    Name = reader.IsDBNull(21) ? reader.GetString(10) : reader.GetString(21),
                    RequiresTargetPerson = reader.IsDBNull(22) ? reader.GetBoolean(11) : reader.GetBoolean(22)
                },
                FirstName = reader.GetString(12),
                LastName = reader.GetString(13),
                RoleName = reader.IsDBNull(14) ? string.Empty : reader.GetString(14),
                DepartmentName = reader.IsDBNull(15) ? string.Empty : reader.GetString(15),
                Status = WorkflowStatusRules.ToLegacyStatus(workflowStatus),
                WorkflowStatus = workflowStatus,
                CreatedAt = reader.GetDateTime(17),
                CompletedAt = reader.IsDBNull(18) ? null : reader.GetDateTime(18),
                ArchivedAt = reader.IsDBNull(19) ? null : reader.GetDateTime(19)
            });
        }

        return result;
    }
}
