using Npgsql;
using NpgsqlTypes;

namespace API;

internal sealed partial class PostgresWorkflowRepository
{
    // Narrowing applied at SQL level for non-admin, non-override users:
    // limits the result set to tasks where (a) the workflow is not in a terminal status
    // and (b) the user has at least one primary assignment matching either the user id
    // or one of their effective responsibilities. Mirrors the in-memory predicate
    // IsAssignedToTask in AuthorizationPolicyService.
    internal readonly record struct WorkflowTaskListNarrowingFilter(long UserId, int[] EffectiveResponsibilityIds);

    private static async Task<List<TaskWithWorkflowDto>> LoadTasks(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        long? taskId,
        WorkflowTaskListNarrowingFilter? narrowing = null)
    {
        var narrowingClause = narrowing.HasValue
            ? @"
  AND w.status <> 'completed'
  AND EXISTS (
      SELECT 1
      FROM task_assignments wta
      WHERE wta.workflow_task_id = t.id
        AND wta.is_primary = TRUE
        AND (
            (wta.assignment_type = 'user' AND wta.assignee_user_id = @narrowUserId)
            OR (wta.assignment_type = 'responsibility' AND wta.assignee_responsibility_id = ANY(@narrowResponsibilityIds))
        )
  )"
            : string.Empty;

        var sql = @"
SELECT
    t.id,
    t.node_instance_id,
    t.workflow_node_task_spec_id,
    t.task_key,
    CASE
        WHEN t.node_instance_id IS NOT NULL THEN COALESCE(runtime_node.node_type = 'approval', FALSE)
        WHEN pt.approval_spec_key IS NULL THEN FALSE
        ELSE t.task_key = pt.approval_spec_key
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
        WHEN t.workflow_node_task_spec_id IS NULL AND tt.id IS NOT NULL THEN tt.is_department_phase_task
        ELSE t.is_department_phase_task
    END,
    template_department.name,
    template_responsibility.responsibility_type,
    template_responsibility.responsibility_key,
    template_responsibility.name
FROM workflow_tasks t
JOIN workflows w ON w.id = t.workflow_id
JOIN workflow_definitions pt ON pt.id = w.workflow_definition_id
JOIN departments d ON d.id = w.department_id
JOIN app_roles r ON r.id = w.position_role_id
LEFT JOIN workflow_node_task_specs tt
    ON tt.id = t.workflow_node_task_spec_id
    OR (t.workflow_node_task_spec_id IS NULL AND tt.spec_key = t.task_key)
LEFT JOIN workflow_node_instances runtime_node_instance ON runtime_node_instance.id = t.node_instance_id
LEFT JOIN workflow_nodes runtime_node ON runtime_node.id = runtime_node_instance.workflow_node_id
LEFT JOIN app_responsibilities template_responsibility ON template_responsibility.id = tt.default_responsibility_id
LEFT JOIN departments template_department ON template_department.id = template_responsibility.department_id
WHERE (@taskId IS NULL OR t.id = @taskId)" + narrowingClause + @"
ORDER BY w.created_at DESC, t.sort_order, t.id;";

        var tasks = new List<TaskWithWorkflowDto>();
        var taskById = new Dictionary<long, WorkflowTaskDto>();

        await using (var command = new NpgsqlCommand(sql, connection, transaction))
        {
            command.Parameters.Add("taskId", NpgsqlDbType.Bigint).Value = (object?)taskId ?? DBNull.Value;
            if (narrowing.HasValue)
            {
                command.Parameters.AddWithValue("narrowUserId", narrowing.Value.UserId);
                command.Parameters.Add("narrowResponsibilityIds", NpgsqlDbType.Array | NpgsqlDbType.Integer).Value =
                    narrowing.Value.EffectiveResponsibilityIds.Length == 0 ? Array.Empty<int>() : narrowing.Value.EffectiveResponsibilityIds;
            }
            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var task = new WorkflowTaskDto
                {
                    Id = reader.GetInt64(0),
                    NodeInstanceId = reader.IsDBNull(1) ? null : reader.GetInt64(1),
                    TaskTemplateId = reader.IsDBNull(2) ? null : checked((int)reader.GetInt64(2)),
                    TaskKey = reader.GetString(3),
                    IsApprovalTask = reader.GetBoolean(4),
                    IsRuntimeNodeTask = reader.GetBoolean(5),
                    Title = reader.GetString(6),
                    Description = reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
                    Category = reader.GetString(8),
                    IconKey = PostgresRepositorySharedHelpers.NormalizeAdminTaskTemplateIconKey(reader.IsDBNull(9) ? null : reader.GetString(9)),
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
                    TaskRef = WorkflowTaskRef.Build(task.Id),
                    TaskFamily = TaskFamilyNames.Workflow,
                    Task = task,
                    Workflow = new TaskWorkflowContextDto
                    {
                        WorkflowId = reader.GetInt64(19),
                        WorkflowUid = reader.GetGuid(20),
                        WorkflowStatus = workflowStatus,
                        WorkflowCreatedAt = reader.GetDateTime(23),
                        FirstName = reader.GetString(24),
                        LastName = reader.GetString(25),
                        EmployeeNumber = reader.GetInt32(26),
                        BadgeNumber = reader.GetInt32(27),
                        DepartmentId = reader.GetInt32(28),
                        DepartmentName = reader.GetString(29),
                        RoleId = reader.GetInt32(30),
                        RoleName = reader.GetString(31)
                    },
                    Rotation = null
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
    wt.workflow_node_task_spec_id,
    wt.task_key,
    CASE
        WHEN wt.node_instance_id IS NOT NULL THEN COALESCE(runtime_node.node_type = 'approval', FALSE)
        WHEN pt.approval_spec_key IS NULL THEN FALSE
        ELSE wt.task_key = pt.approval_spec_key
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
        WHEN wt.workflow_node_task_spec_id IS NULL AND tt.id IS NOT NULL THEN tt.is_department_phase_task
        ELSE wt.is_department_phase_task
    END,
    template_department.name,
    template_responsibility.responsibility_type,
    template_responsibility.responsibility_key,
    template_responsibility.name
FROM workflow_tasks wt
JOIN workflows w ON w.id = wt.workflow_id
JOIN workflow_definitions pt ON pt.id = w.workflow_definition_id
LEFT JOIN workflow_node_task_specs tt
    ON tt.id = wt.workflow_node_task_spec_id
    OR (wt.workflow_node_task_spec_id IS NULL AND tt.spec_key = wt.task_key)
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
                    TaskTemplateId = reader.IsDBNull(2) ? null : checked((int)reader.GetInt64(2)),
                    TaskKey = reader.GetString(3),
                    IsApprovalTask = reader.GetBoolean(4),
                    IsRuntimeNodeTask = reader.GetBoolean(5),
                    Title = reader.GetString(6),
                    Description = reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
                    Category = reader.GetString(8),
                    IconKey = PostgresRepositorySharedHelpers.NormalizeAdminTaskTemplateIconKey(reader.IsDBNull(9) ? null : reader.GetString(9)),
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
WITH latest_workflow AS (
    SELECT DISTINCT ON (resolved.person_id)
        resolved.person_id,
        resolved.department_id,
        resolved.position_role_id,
        resolved.employee_number,
        resolved.badge_number,
        resolved.first_name,
        resolved.last_name
    FROM (
        SELECT
            w.target_person_id AS person_id,
            w.department_id,
            w.position_role_id,
            w.employee_number,
            w.badge_number,
            w.first_name,
            w.last_name,
            w.created_at,
            w.id
        FROM workflows w
        WHERE w.target_person_id IS NOT NULL

        UNION ALL

        SELECT
            p.id AS person_id,
            w.department_id,
            w.position_role_id,
            w.employee_number,
            w.badge_number,
            w.first_name,
            w.last_name,
            w.created_at,
            w.id
        FROM people p
        JOIN workflows w ON p.employee_number IS NOT NULL AND w.employee_number = p.employee_number
    ) resolved
    ORDER BY resolved.person_id, resolved.created_at DESC, resolved.id DESC
),
latest_completed_onboarding AS (
    SELECT DISTINCT ON (resolved.person_id)
        resolved.person_id,
        resolved.workflow_uid,
        resolved.completed_at
    FROM (
        SELECT
            w.target_person_id AS person_id,
            w.uid AS workflow_uid,
            COALESCE(w.completed_at, w.created_at) AS completed_at,
            w.id
        FROM workflows w
        JOIN workflow_definitions pt ON pt.id = w.workflow_definition_id
        LEFT JOIN workflow_definition_versions v ON v.id = w.workflow_definition_version_id
        LEFT JOIN workflow_definitions vpt ON vpt.id = v.workflow_definition_id
        WHERE w.target_person_id IS NOT NULL
          AND pt.definition_key = 'onboarding'
          AND (w.workflow_definition_version_id IS NULL OR vpt.definition_key = 'onboarding')
          AND w.status = 'completed'

        UNION ALL

        SELECT
            p.id AS person_id,
            w.uid AS workflow_uid,
            COALESCE(w.completed_at, w.created_at) AS completed_at,
            w.id
        FROM people p
        JOIN workflows w ON p.employee_number IS NOT NULL AND w.employee_number = p.employee_number
        JOIN workflow_definitions pt ON pt.id = w.workflow_definition_id
        LEFT JOIN workflow_definition_versions v ON v.id = w.workflow_definition_version_id
        LEFT JOIN workflow_definitions vpt ON vpt.id = v.workflow_definition_id
        WHERE pt.definition_key = 'onboarding'
          AND (w.workflow_definition_version_id IS NULL OR vpt.definition_key = 'onboarding')
          AND w.status = 'completed'
    ) resolved
    ORDER BY resolved.person_id, resolved.completed_at DESC, resolved.id DESC
)
SELECT
    p.id,
    COALESCE(
        NULLIF(BTRIM(CONCAT_WS(' ', COALESCE(p.first_name, latest_workflow.first_name), COALESCE(p.last_name, latest_workflow.last_name))), ''),
        u.display_name,
        linked_directory.display_name,
        'Person #' || p.id::text
    ) AS display_name,
    COALESCE(p.department_id, latest_workflow.department_id, u.department_id) AS resolved_department_id,
    d.name AS department_name,
    COALESCE(p.current_position_role_id, latest_workflow.position_role_id) AS role_id,
    role_ref.name AS role_name,
    COALESCE(p.employee_number, latest_workflow.employee_number, linked_directory.employee_number) AS employee_number,
    COALESCE(p.badge_number, latest_workflow.badge_number) AS badge_number,
    COALESCE(p.first_name, latest_workflow.first_name) AS first_name,
    COALESCE(p.last_name, latest_workflow.last_name) AS last_name,
    COALESCE(
        NULLIF(BTRIM(p.employment_status), ''),
        CASE
            WHEN p.exit_date IS NOT NULL THEN 'exited'
            WHEN p.app_user_id IS NOT NULL THEN 'active'
            ELSE 'planned'
        END
    ) AS employment_status,
    p.entry_date,
    p.exit_date,
    p.app_user_id,
    p.directory_identity_id,
    CASE
        WHEN p.directory_identity_id IS NOT NULL THEN 'linked'
        WHEN p.app_user_id IS NOT NULL THEN 'user_only'
        ELSE 'unlinked'
    END AS directory_link_status,
    linked_directory.display_name AS directory_display_name,
    linked_directory.user_principal_name,
    linked_directory.mail,
    linked_directory.employee_number AS directory_employee_number,
    latest_completed_onboarding.workflow_uid AS latest_completed_onboarding_workflow_uid,
    latest_completed_onboarding.completed_at AS latest_completed_onboarding_at,
    w.uid,
    pt.definition_key,
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
    COALESCE(NULLIF(BTRIM(wd.name), ''), pt.name),
    COALESCE(vpt.requires_target_person, pt.requires_target_person)
FROM people p
LEFT JOIN app_users u ON u.id = p.app_user_id
LEFT JOIN directory_identities linked_directory ON linked_directory.id = p.directory_identity_id
LEFT JOIN latest_workflow ON latest_workflow.person_id = p.id
LEFT JOIN latest_completed_onboarding ON latest_completed_onboarding.person_id = p.id
LEFT JOIN departments d ON d.id = COALESCE(p.department_id, latest_workflow.department_id, u.department_id)
LEFT JOIN app_roles role_ref ON role_ref.id = COALESCE(p.current_position_role_id, latest_workflow.position_role_id)
LEFT JOIN workflows w
    ON (
        w.target_person_id = p.id
        OR (p.employee_number IS NOT NULL AND w.employee_number = p.employee_number)
   )
LEFT JOIN workflow_definitions pt ON pt.id = w.workflow_definition_id
LEFT JOIN workflow_definition_versions v ON v.id = w.workflow_definition_version_id
LEFT JOIN workflow_definitions wd ON wd.id = v.workflow_definition_id
LEFT JOIN workflow_definitions vpt ON vpt.id = v.workflow_definition_id
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
                    RoleId = reader.IsDBNull(4) ? null : reader.GetInt32(4),
                    RoleName = reader.IsDBNull(5) ? null : reader.GetString(5),
                    EmployeeNumber = reader.IsDBNull(6) ? null : reader.GetInt32(6),
                    BadgeNumber = reader.IsDBNull(7) ? null : reader.GetInt32(7),
                    FirstName = reader.IsDBNull(8) ? null : reader.GetString(8),
                    LastName = reader.IsDBNull(9) ? null : reader.GetString(9),
                    EmploymentStatus = reader.IsDBNull(10) ? null : reader.GetString(10),
                    EntryDate = reader.IsDBNull(11) ? null : reader.GetFieldValue<DateOnly>(11),
                    ExitDate = reader.IsDBNull(12) ? null : reader.GetFieldValue<DateOnly>(12),
                    AppUserId = reader.IsDBNull(13) ? null : reader.GetInt64(13),
                    DirectoryIdentityId = reader.IsDBNull(14) ? null : reader.GetInt64(14),
                    DirectoryLinkStatus = reader.IsDBNull(15) ? null : reader.GetString(15),
                    DirectoryDisplayName = reader.IsDBNull(16) ? null : reader.GetString(16),
                    DirectoryUserPrincipalName = reader.IsDBNull(17) ? null : reader.GetString(17),
                    DirectoryMail = reader.IsDBNull(18) ? null : reader.GetString(18),
                    DirectoryEmployeeNumber = reader.IsDBNull(19) ? null : reader.GetInt32(19),
                    LatestCompletedOnboardingWorkflowUid = reader.IsDBNull(20) ? null : reader.GetGuid(20),
                    LatestCompletedOnboardingAt = reader.IsDBNull(21) ? null : reader.GetDateTime(21),
                    Workflows = workflows
                };
            }

            // A person may have no workflows yet — skip the null workflow row.
            if (reader.IsDBNull(22))
            {
                continue;
            }

            var workflowStatus = reader.GetString(30);
            workflows.Add(new PersonWorkflowSummaryDto
            {
                Uid = reader.GetGuid(22),
                ProcessType = new WorkflowProcessTypeDto
                {
                    Key = reader.IsDBNull(34) ? reader.GetString(23) : reader.GetString(34),
                    Name = reader.IsDBNull(35) ? reader.GetString(24) : reader.GetString(35),
                    RequiresTargetPerson = reader.IsDBNull(36) ? reader.GetBoolean(25) : reader.GetBoolean(36)
                },
                FirstName = reader.GetString(26),
                LastName = reader.GetString(27),
                RoleName = reader.IsDBNull(28) ? string.Empty : reader.GetString(28),
                DepartmentName = reader.IsDBNull(29) ? string.Empty : reader.GetString(29),
                WorkflowStatus = workflowStatus,
                CreatedAt = reader.GetDateTime(31),
                CompletedAt = reader.IsDBNull(32) ? null : reader.GetDateTime(32),
                ArchivedAt = reader.IsDBNull(33) ? null : reader.GetDateTime(33)
            });
        }

        return result;
    }
}
