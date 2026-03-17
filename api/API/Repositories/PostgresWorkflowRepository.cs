using Npgsql;
using NpgsqlTypes;

namespace API;

// Kapselt den kompletten PostgreSQL-Zugriff fuer Workflows, Anforderungen, Aufgaben und Benachrichtigungen.
internal sealed partial class PostgresWorkflowRepository : IWorkflowRepository
{
    // Diese Regeln definieren den erlaubten Lebenszyklus einzelner Aufgaben.
    private static readonly HashSet<string> AllowedTaskStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "open",
        "ready",
        "in_progress",
        "blocked",
        "done",
        "skipped",
        "cancelled"
    };

    private static readonly HashSet<string> TerminalTaskStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "done",
        "skipped",
        "cancelled"
    };

    private static readonly Dictionary<string, HashSet<string>> AllowedTaskTransitions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["open"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "ready", "in_progress", "blocked", "done", "skipped", "cancelled" },
        ["ready"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "in_progress", "blocked", "done", "skipped", "cancelled" },
        ["in_progress"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "done", "blocked", "skipped", "cancelled" },
        ["blocked"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "ready", "cancelled" },
        ["done"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
        ["skipped"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
        ["cancelled"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    };

    private const string SupervisorRequirementTaskKey = "supervisor_fills_document";
    private const string LegacySupervisorHandoverTaskKey = "onboarding_doc_sent_to_supervisor";

    private enum TaskGenerationStage
    {
        Initial,
        AfterSupervisor
    }

    // Die Verbindung wird bewusst direkt aus der Umgebung gelesen, damit API und Container identisch konfiguriert bleiben.
    private static string GetConnectionString()
    {
        var connectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("CONNECTION_STRING is not configured.");
        }

        return connectionString;
    }

    // Stammdaten fuer die HR-Erfassung.
    public async Task<List<DepartmentDto>> GetDepartments()
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();

        const string sql = @"
SELECT id, name
FROM departments
ORDER BY name;";

        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();

        var departments = new List<DepartmentDto>();
        while (await reader.ReadAsync())
        {
            departments.Add(new DepartmentDto
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1)
            });
        }

        return departments;
    }

    public async Task<List<RoleDto>> GetRoles()
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();

        const string sql = @"
SELECT r.id, r.department_id, d.name, r.name, r.is_active
FROM app_roles r
JOIN departments d ON d.id = r.department_id
WHERE r.role_kind = 'position'
ORDER BY d.name, r.name;";

        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();

        var roles = new List<RoleDto>();
        while (await reader.ReadAsync())
        {
            roles.Add(new RoleDto
            {
                Id = reader.GetInt32(0),
                DepartmentId = reader.GetInt32(1),
                DepartmentName = reader.GetString(2),
                Name = reader.GetString(3),
                IsActive = reader.GetBoolean(4)
            });
        }

        return roles;
    }

    // Anforderungen und Rollenempfehlungen bilden die Eingabemaske fuer neue Workflows.
    public async Task<List<RequirementDto>> GetRequirements(int? roleId = null)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();

        return await LoadRequirements(connection, null);
    }

    public async Task<WorkflowConfigDto?> GetWorkflowConfig(int? roleId)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();

        if (roleId.HasValue && !await RoleExists(connection, null, roleId.Value))
        {
            return null;
        }

        var requirements = await LoadRequirements(connection, null);
        var roleRecommendations = roleId.HasValue
            ? await LoadRoleRecommendations(connection, null, roleId.Value)
            : new RoleRecommendationsDto
            {
                RecommendedRequirementIds = new List<int>(),
                DefaultValues = new List<RoleRecommendationDefaultValueDto>(),
                DefaultSelectedOptions = new List<RoleRecommendationSelectedOptionsDto>()
            };

        return new WorkflowConfigDto
        {
            Requirements = requirements,
            RoleRecommendations = roleRecommendations
        };
    }

    // Erstellt den Workflow, initialisiert Benachrichtigungen und liefert anschliessend die neue UID zurueck.
    public async Task<WorkflowCreationResult> CreateWorkflow(CreateWorkflowRequest request, long createdByUserId)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        await EnsureValidPositionRole(connection, transaction, request.RoleId, request.DepartmentId);

        const string workflowInsertSql = @"
INSERT INTO workflows (
    department_id,
    onboarding_role_id,
    created_by_user_id,
    first_name,
    last_name,
    employee_number,
    badge_number,
    status,
    started_at
)
VALUES (
    @departmentId,
    @roleId,
    @createdByUserId,
    @firstName,
    @lastName,
    @employeeNumber,
    @badgeNumber,
    'draft',
    NULL
)
RETURNING id, uid;";

        long workflowId;
        Guid workflowUid;

        await using (var workflowInsertCommand = new NpgsqlCommand(workflowInsertSql, connection, transaction))
        {
            workflowInsertCommand.Parameters.AddWithValue("departmentId", request.DepartmentId);
            workflowInsertCommand.Parameters.AddWithValue("roleId", request.RoleId);
            workflowInsertCommand.Parameters.AddWithValue("createdByUserId", createdByUserId);
            workflowInsertCommand.Parameters.AddWithValue("firstName", request.FirstName);
            workflowInsertCommand.Parameters.AddWithValue("lastName", request.LastName);
            workflowInsertCommand.Parameters.AddWithValue("employeeNumber", request.EmployeeNumber);
            workflowInsertCommand.Parameters.AddWithValue("badgeNumber", request.BadgeNumber);

            await using var reader = await workflowInsertCommand.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                throw new InvalidOperationException("Workflow could not be created.");
            }

            workflowId = reader.GetInt64(0);
            workflowUid = reader.GetGuid(1);
        }

        await GenerateWorkflowTasks(
            connection,
            transaction,
            workflowId,
            request.DepartmentId,
            new Dictionary<string, StoredWorkflowAnswerRecord>(StringComparer.OrdinalIgnoreCase),
            TaskGenerationStage.Initial);

        await RecalculateWorkflowTaskAvailability(connection, transaction, workflowId);
        await RecalculateAndPersistWorkflowStatus(connection, transaction, workflowId);

        var notificationTargets = await CreateWorkflowNotifications(
            connection,
            transaction,
            workflowId,
            request.DepartmentId);

        await transaction.CommitAsync();

        return new WorkflowCreationResult
        {
            WorkflowId = workflowId,
            Uid = workflowUid,
            NotificationTargets = notificationTargets
        };
    }

    // Der Schritt der Abteilungsleitung ersetzt vorhandene Antworten und erzeugt daraus den weiteren Aufgabenplan.
    public async Task<WorkflowDetailDto?> CompleteSupervisorStep(
        Guid workflowUid,
        IReadOnlyList<RequirementSelectionInputDto> selections)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        const string workflowSql = @"
SELECT
    id,
    onboarding_role_id,
    department_id,
    status
FROM workflows
WHERE uid = @workflowUid
LIMIT 1
FOR UPDATE;";

        long workflowId;
        int workflowRoleId;
        int workflowDepartmentId;
        string workflowStatus;

        await using (var workflowCommand = new NpgsqlCommand(workflowSql, connection, transaction))
        {
            workflowCommand.Parameters.AddWithValue("workflowUid", workflowUid);
            await using var reader = await workflowCommand.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
            {
                return null;
            }

            workflowId = reader.GetInt64(0);
            workflowRoleId = reader.GetInt32(1);
            workflowDepartmentId = reader.GetInt32(2);
            workflowStatus = reader.GetString(3);
        }

        if (!string.Equals(workflowStatus, "waiting_for_supervisor", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Der Schritt der Abteilungsleitung kann nur abgeschlossen werden, solange der Fall auf die Abteilungsleitung wartet.");
        }

        const string clearAnswersSql = @"
DELETE FROM workflow_answers
WHERE workflow_id = @workflowId;";

        await using (var clearAnswersCommand = new NpgsqlCommand(clearAnswersSql, connection, transaction))
        {
            clearAnswersCommand.Parameters.AddWithValue("workflowId", workflowId);
            await clearAnswersCommand.ExecuteNonQueryAsync();
        }

        var answerDefinitions = await LoadAnswerDefinitionRecords(connection, transaction);
        var roleDefaults = await LoadRoleDefaultRecords(connection, transaction, workflowRoleId);

        var storedAnswers = await PersistWorkflowAnswers(
            connection,
            transaction,
            workflowId,
            selections,
            answerDefinitions,
            roleDefaults);

        var answersByKey = storedAnswers
            .GroupBy(answer => answer.AnswerKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);

        ValidateSupervisorSelections(answersByKey);

        if (!await WorkflowHasAnyTasks(connection, transaction, workflowId))
        {
            await BackfillLegacyInitialTasksForSupervisorCompletion(
                connection,
                transaction,
                workflowId,
                workflowDepartmentId);
        }
        else
        {
            await CompleteWorkflowTaskByKey(
                connection,
                transaction,
                workflowId,
                SupervisorRequirementTaskKey);
        }

        await GenerateWorkflowTasks(
            connection,
            transaction,
            workflowId,
            workflowDepartmentId,
            answersByKey,
            TaskGenerationStage.AfterSupervisor);

        await RecalculateWorkflowTaskAvailability(connection, transaction, workflowId);
        await RecalculateAndPersistWorkflowStatus(connection, transaction, workflowId);

        await transaction.CommitAsync();
        return await GetWorkflowByUid(workflowUid);
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
                        reader.GetString(2),
                        reader.IsDBNull(27) ? null : reader.GetString(27),
                        reader.IsDBNull(28) ? null : reader.GetString(28)),
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

                if (reader.GetBoolean(3))
                {
                    var resolvedArea = ResolveTaskProcessArea(
                        task.TaskKey,
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
    ready_at = COALESCE(ready_at, NOW())
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

    private static async Task EnsureAssignableResponsibilityExists(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int responsibilityId)
    {
        const string sql = @"
SELECT id
FROM app_responsibilities
WHERE id = @responsibilityId
  AND is_active = TRUE
LIMIT 1;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("responsibilityId", responsibilityId);
        var scalar = await command.ExecuteScalarAsync();
        if (scalar is null)
        {
            throw new InvalidOperationException("Assignee responsibility is invalid or inactive.");
        }
    }

    private static async Task EnsureAssignableUserExists(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long userId)
    {
        const string sql = @"
SELECT id
FROM app_users
WHERE id = @userId
  AND is_active = TRUE
LIMIT 1;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("userId", userId);
        var scalar = await command.ExecuteScalarAsync();
        if (scalar is null)
        {
            throw new InvalidOperationException("Assignee user is invalid or inactive.");
        }
    }

    private static async Task EnsureUserHasResponsibility(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long userId,
        int responsibilityId)
    {
        const string sql = @"
SELECT 1
FROM app_user_responsibilities
WHERE app_user_id = @userId
  AND app_responsibility_id = @responsibilityId
LIMIT 1;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("userId", userId);
        command.Parameters.AddWithValue("responsibilityId", responsibilityId);

        var scalar = await command.ExecuteScalarAsync();
        if (scalar is null)
        {
            throw new InvalidOperationException("The selected user is not directly assigned to the selected responsibility.");
        }
    }

    private static async Task<List<RequirementDto>> LoadRequirements(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction)
    {
        var definitions = await LoadAnswerDefinitionRecords(connection, transaction);

        return definitions.Values
            .OrderBy(definition => definition.SortOrder)
            .ThenBy(definition => definition.DefinitionId)
            .Select(definition => new RequirementDto
            {
                Id = definition.DefinitionId,
                Key = definition.Key,
                Title = definition.Title,
                Description = definition.Description,
                Category = definition.Category,
                IconKey = definition.IconKey,
                InputType = definition.InputType,
                IsRequired = definition.IsRequired,
                SortOrder = definition.SortOrder,
                Options = definition.OptionsById.Values
                    .OrderBy(option => option.SortOrder)
                    .ThenBy(option => option.OptionId)
                    .Select(option => new RequirementOptionDto
                    {
                        Id = option.OptionId,
                        Key = option.OptionKey,
                        Value = option.OptionValue,
                        Label = option.OptionLabel,
                        SortOrder = option.SortOrder,
                        IsDefault = false
                    })
                    .ToList()
            })
            .ToList();
    }

    private static async Task<Dictionary<int, AnswerDefinitionRecord>> LoadAnswerDefinitionRecords(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction)
    {
        const string sql = @"
SELECT
    d.id,
    d.answer_key,
    d.title,
    d.description,
    d.category,
    d.icon_key,
    d.input_type,
    d.is_required,
    d.sort_order,
    o.id,
    o.option_key,
    o.option_value,
    o.option_label,
    o.sort_order
FROM workflow_answer_definitions d
LEFT JOIN workflow_answer_options o ON o.answer_definition_id = d.id
WHERE d.is_active = TRUE
ORDER BY d.sort_order, d.id, o.sort_order, o.id;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        await using var reader = await command.ExecuteReaderAsync();

        var definitions = new Dictionary<int, AnswerDefinitionRecord>();
        while (await reader.ReadAsync())
        {
            var definitionId = reader.GetInt32(0);
            if (!definitions.TryGetValue(definitionId, out var definition))
            {
                definition = new AnswerDefinitionRecord
                {
                    DefinitionId = definitionId,
                    Key = reader.GetString(1),
                    Title = reader.GetString(2),
                    Description = reader.GetString(3),
                    Category = reader.GetString(4),
                    IconKey = reader.GetString(5),
                    InputType = reader.GetString(6),
                    IsRequired = reader.GetBoolean(7),
                    SortOrder = reader.GetInt32(8),
                    OptionsById = new Dictionary<int, AnswerOptionRecord>()
                };

                definitions.Add(definitionId, definition);
            }

            if (!reader.IsDBNull(9))
            {
                var optionId = reader.GetInt32(9);
                definition.OptionsById[optionId] = new AnswerOptionRecord
                {
                    OptionId = optionId,
                    OptionKey = reader.GetString(10),
                    OptionValue = reader.GetString(11),
                    OptionLabel = reader.GetString(12),
                    SortOrder = reader.GetInt32(13)
                };
            }
        }

        return definitions;
    }

    private static async Task<RoleRecommendationsDto> LoadRoleRecommendations(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        int roleId)
    {
        const string sql = @"
SELECT
    ard.answer_definition_id,
    ard.is_recommended,
    ard.is_default,
    ard.default_value_boolean,
    ard.default_value_text,
    ard.default_value_number,
    MAX(CASE WHEN ardo.is_default THEN ardo.answer_option_id END) AS default_selected_option_id,
    COALESCE(
        ARRAY_AGG(ardo.answer_option_id ORDER BY o.sort_order) FILTER (WHERE ardo.is_default),
        ARRAY[]::INTEGER[]
    ) AS default_selected_option_ids
FROM app_role_answer_defaults ard
LEFT JOIN app_role_answer_default_options ardo
    ON ardo.app_role_id = ard.app_role_id
    AND ardo.answer_definition_id = ard.answer_definition_id
LEFT JOIN workflow_answer_options o ON o.id = ardo.answer_option_id
WHERE ard.app_role_id = @roleId
GROUP BY
    ard.answer_definition_id,
    ard.is_recommended,
    ard.is_default,
    ard.default_value_boolean,
    ard.default_value_text,
    ard.default_value_number,
    ard.sort_order
ORDER BY ard.sort_order, ard.answer_definition_id;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("roleId", roleId);

        await using var reader = await command.ExecuteReaderAsync();

        var recommendedRequirementIds = new List<int>();
        var defaultValues = new List<RoleRecommendationDefaultValueDto>();
        var defaultSelectedOptions = new List<RoleRecommendationSelectedOptionsDto>();

        while (await reader.ReadAsync())
        {
            var requirementId = reader.GetInt32(0);
            var isRecommended = reader.GetBoolean(1);
            var isDefault = reader.GetBoolean(2);
            bool? defaultValueBoolean = reader.IsDBNull(3) ? null : reader.GetBoolean(3);
            var defaultValueText = reader.IsDBNull(4) ? null : reader.GetString(4);
            decimal? defaultValueNumber = reader.IsDBNull(5) ? null : reader.GetDecimal(5);
            int? selectedOptionId = reader.IsDBNull(6) ? null : reader.GetInt32(6);
            var selectedOptionIds = reader.IsDBNull(7)
                ? new List<int>()
                : ((int[])reader.GetValue(7)).Distinct().ToList();

            if (isRecommended)
            {
                recommendedRequirementIds.Add(requirementId);
            }

            if (isDefault || defaultValueBoolean.HasValue || !string.IsNullOrWhiteSpace(defaultValueText) || defaultValueNumber.HasValue)
            {
                defaultValues.Add(new RoleRecommendationDefaultValueDto
                {
                    RequirementId = requirementId,
                    ValueBoolean = defaultValueBoolean,
                    ValueText = defaultValueText,
                    ValueNumber = defaultValueNumber
                });
            }

            if (isDefault || selectedOptionId.HasValue || selectedOptionIds.Count > 0)
            {
                defaultSelectedOptions.Add(new RoleRecommendationSelectedOptionsDto
                {
                    RequirementId = requirementId,
                    SelectedOptionId = selectedOptionId,
                    SelectedOptionIds = selectedOptionIds
                });
            }
        }

        return new RoleRecommendationsDto
        {
            RecommendedRequirementIds = recommendedRequirementIds.Distinct().ToList(),
            DefaultValues = defaultValues,
            DefaultSelectedOptions = defaultSelectedOptions
        };
    }

    private static async Task<Dictionary<int, RoleDefaultRecord>> LoadRoleDefaultRecords(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int roleId)
    {
        const string sql = @"
SELECT
    ard.answer_definition_id,
    ard.default_value_boolean,
    ard.default_value_text,
    ard.default_value_number,
    MAX(CASE WHEN ardo.is_default THEN ardo.answer_option_id END) AS default_selected_option_id,
    COALESCE(
        ARRAY_AGG(ardo.answer_option_id ORDER BY o.sort_order) FILTER (WHERE ardo.is_default),
        ARRAY[]::INTEGER[]
    ) AS default_selected_option_ids
FROM app_role_answer_defaults ard
LEFT JOIN app_role_answer_default_options ardo
    ON ardo.app_role_id = ard.app_role_id
    AND ardo.answer_definition_id = ard.answer_definition_id
LEFT JOIN workflow_answer_options o ON o.id = ardo.answer_option_id
WHERE ard.app_role_id = @roleId
GROUP BY
    ard.answer_definition_id,
    ard.default_value_boolean,
    ard.default_value_text,
    ard.default_value_number,
    ard.sort_order
ORDER BY ard.sort_order, ard.answer_definition_id;";

        var defaults = new Dictionary<int, RoleDefaultRecord>();

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("roleId", roleId);
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var answerDefinitionId = reader.GetInt32(0);
            defaults[answerDefinitionId] = new RoleDefaultRecord
            {
                AnswerDefinitionId = answerDefinitionId,
                DefaultValueBoolean = reader.IsDBNull(1) ? null : reader.GetBoolean(1),
                DefaultValueText = reader.IsDBNull(2) ? null : reader.GetString(2),
                DefaultValueNumber = reader.IsDBNull(3) ? null : reader.GetDecimal(3),
                DefaultSelectedOptionId = reader.IsDBNull(4) ? null : reader.GetInt32(4),
                DefaultSelectedOptionIds = reader.IsDBNull(5)
                    ? new List<int>()
                    : ((int[])reader.GetValue(5)).Distinct().ToList()
            };
        }

        return defaults;
    }

    private static async Task<List<StoredWorkflowAnswerRecord>> PersistWorkflowAnswers(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long workflowId,
        IReadOnlyList<RequirementSelectionInputDto> selections,
        IReadOnlyDictionary<int, AnswerDefinitionRecord> definitions,
        IReadOnlyDictionary<int, RoleDefaultRecord> roleDefaults)
    {
        var submittedByDefinitionId = new Dictionary<int, RequirementSelectionInputDto>();
        foreach (var selection in selections)
        {
            if (definitions.ContainsKey(selection.RequirementId))
            {
                submittedByDefinitionId[selection.RequirementId] = selection;
            }
        }

        const string insertSql = @"
INSERT INTO workflow_answers (
    workflow_id,
    answer_definition_id,
    answer_key,
    input_type,
    value_boolean,
    value_text,
    value_number,
    selected_option_id
)
VALUES (
    @workflowId,
    @answerDefinitionId,
    @answerKey,
    @inputType,
    @valueBoolean,
    @valueText,
    @valueNumber,
    @selectedOptionId
)
RETURNING id;";

        const string insertSelectedOptionSql = @"
INSERT INTO workflow_answer_selected_options (workflow_answer_id, answer_option_id)
VALUES (@workflowAnswerId, @answerOptionId)
ON CONFLICT (workflow_answer_id, answer_option_id) DO NOTHING;";

        var persistedAnswers = new List<StoredWorkflowAnswerRecord>();

        foreach (var definition in definitions.Values.OrderBy(item => item.SortOrder).ThenBy(item => item.DefinitionId))
        {
            submittedByDefinitionId.TryGetValue(definition.DefinitionId, out var submitted);
            roleDefaults.TryGetValue(definition.DefinitionId, out var roleDefault);

            bool? valueBoolean = submitted?.ValueBoolean ?? roleDefault?.DefaultValueBoolean;
            var rawText = submitted?.ValueText ?? roleDefault?.DefaultValueText;
            var valueText = string.IsNullOrWhiteSpace(rawText) ? null : rawText.Trim();
            decimal? valueNumber = submitted?.ValueNumber ?? roleDefault?.DefaultValueNumber;

            int? selectedOptionId = submitted?.SelectedOptionId ?? roleDefault?.DefaultSelectedOptionId;
            var selectedOptionIds = submitted?.SelectedOptionIds?.Distinct().ToList()
                ?? (roleDefault?.DefaultSelectedOptionIds.ToList() ?? new List<int>());

            selectedOptionIds = selectedOptionIds
                .Where(optionId => definition.OptionsById.ContainsKey(optionId))
                .Distinct()
                .ToList();

            if (selectedOptionId.HasValue && !definition.OptionsById.ContainsKey(selectedOptionId.Value))
            {
                throw new InvalidOperationException($"Invalid option '{selectedOptionId.Value}' for requirement '{definition.DefinitionId}'.");
            }

            switch (definition.InputType)
            {
                case "boolean":
                    valueText = null;
                    valueNumber = null;
                    selectedOptionId = null;
                    selectedOptionIds.Clear();

                    if (definition.IsRequired && !valueBoolean.HasValue)
                    {
                        throw new InvalidOperationException($"Requirement {definition.DefinitionId} needs a boolean value.");
                    }
                    break;

                case "text":
                    valueBoolean = null;
                    valueNumber = null;
                    selectedOptionId = null;
                    selectedOptionIds.Clear();

                    if (definition.IsRequired && string.IsNullOrWhiteSpace(valueText))
                    {
                        throw new InvalidOperationException($"Requirement {definition.DefinitionId} needs text input.");
                    }
                    break;

                case "select":
                    valueBoolean = null;
                    valueText = null;
                    valueNumber = null;

                    if (!selectedOptionId.HasValue && selectedOptionIds.Count == 1)
                    {
                        selectedOptionId = selectedOptionIds[0];
                    }

                    selectedOptionIds.Clear();
                    if (selectedOptionId.HasValue)
                    {
                        selectedOptionIds.Add(selectedOptionId.Value);
                    }

                    if (definition.IsRequired && !selectedOptionId.HasValue)
                    {
                        throw new InvalidOperationException($"Requirement {definition.DefinitionId} needs a selected option.");
                    }
                    break;

                case "multi_select":
                    valueBoolean = null;
                    valueText = null;
                    valueNumber = null;
                    selectedOptionId = null;

                    if (definition.IsRequired && selectedOptionIds.Count == 0)
                    {
                        throw new InvalidOperationException($"Requirement {definition.DefinitionId} needs one or more selected options.");
                    }
                    break;

                default:
                    throw new InvalidOperationException($"Unsupported input type '{definition.InputType}'.");
            }

            long workflowAnswerId;
            await using (var insertCommand = new NpgsqlCommand(insertSql, connection, transaction))
            {
                insertCommand.Parameters.AddWithValue("workflowId", workflowId);
                insertCommand.Parameters.AddWithValue("answerDefinitionId", definition.DefinitionId);
                insertCommand.Parameters.AddWithValue("answerKey", definition.Key);
                insertCommand.Parameters.AddWithValue("inputType", definition.InputType);
                insertCommand.Parameters.AddWithValue("valueBoolean", (object?)valueBoolean ?? DBNull.Value);
                insertCommand.Parameters.AddWithValue("valueText", (object?)valueText ?? DBNull.Value);
                insertCommand.Parameters.AddWithValue("valueNumber", (object?)valueNumber ?? DBNull.Value);
                insertCommand.Parameters.AddWithValue("selectedOptionId", (object?)selectedOptionId ?? DBNull.Value);

                var scalar = await insertCommand.ExecuteScalarAsync();
                if (scalar is null)
                {
                    throw new InvalidOperationException("Workflow answer could not be stored.");
                }

                workflowAnswerId = (long)scalar;
            }

            if (definition.InputType == "multi_select")
            {
                foreach (var optionId in selectedOptionIds)
                {
                    await using var insertSelectedOptionCommand = new NpgsqlCommand(insertSelectedOptionSql, connection, transaction);
                    insertSelectedOptionCommand.Parameters.AddWithValue("workflowAnswerId", workflowAnswerId);
                    insertSelectedOptionCommand.Parameters.AddWithValue("answerOptionId", optionId);
                    await insertSelectedOptionCommand.ExecuteNonQueryAsync();
                }
            }

            var selectedOptionValue = selectedOptionId.HasValue && definition.OptionsById.TryGetValue(selectedOptionId.Value, out var selectedOption)
                ? selectedOption.OptionValue
                : null;

            var selectedOptionValues = selectedOptionIds
                .Where(optionId => definition.OptionsById.ContainsKey(optionId))
                .Select(optionId => definition.OptionsById[optionId].OptionValue)
                .ToList();

            persistedAnswers.Add(new StoredWorkflowAnswerRecord
            {
                WorkflowAnswerId = workflowAnswerId,
                AnswerDefinitionId = definition.DefinitionId,
                AnswerKey = definition.Key,
                InputType = definition.InputType,
                ValueBoolean = valueBoolean,
                ValueText = valueText,
                ValueNumber = valueNumber,
                SelectedOptionId = selectedOptionId,
                SelectedOptionValue = selectedOptionValue,
                SelectedOptionIds = selectedOptionIds,
                SelectedOptionValues = selectedOptionValues
            });
        }

        return persistedAnswers;
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
                IsRequired = reader.GetBoolean(7),
                SortOrder = reader.GetInt32(8)
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

    private static async Task<int?> LoadResponsibilityIdByKey(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string responsibilityKey)
    {
        const string sql = @"
SELECT id
FROM app_responsibilities
WHERE responsibility_key = @responsibilityKey
  AND is_active = TRUE
LIMIT 1;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("responsibilityKey", responsibilityKey);

        var scalar = await command.ExecuteScalarAsync();
        return scalar is null ? null : (int?)scalar;
    }

    private static async Task<int?> LoadDepartmentLeadResponsibilityId(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int departmentId)
    {
        const string sql = @"
SELECT id
FROM app_responsibilities
WHERE department_id = @departmentId
  AND responsibility_type = 'department_lead'
  AND is_active = TRUE
ORDER BY id
LIMIT 1;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("departmentId", departmentId);

        var scalar = await command.ExecuteScalarAsync();
        return scalar is null ? null : (int?)scalar;
    }

    private static async Task<long?> LoadExplicitResponsibilityOwnerUserId(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int responsibilityId)
    {
        const string sql = @"
SELECT u.id
FROM system_responsibilities sr
JOIN people p ON p.id = sr.responsible_person_id
JOIN app_users u ON u.id = p.app_user_id
WHERE sr.app_responsibility_id = @responsibilityId
  AND u.is_active = TRUE
LIMIT 1;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("responsibilityId", responsibilityId);

        var scalar = await command.ExecuteScalarAsync();
        return scalar is null ? null : (long?)scalar;
    }

    private static async Task<int?> LoadResponsibilityDepartmentOverrideId(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int responsibilityId)
    {
        const string sql = @"
SELECT COALESCE(sr.responsible_department_id, r.department_id)
FROM app_responsibilities r
LEFT JOIN system_responsibilities sr ON sr.app_responsibility_id = r.id
WHERE r.id = @responsibilityId
LIMIT 1;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("responsibilityId", responsibilityId);

        var scalar = await command.ExecuteScalarAsync();
        return scalar is null || scalar is DBNull ? null : (int?)scalar;
    }

    private static async Task<long?> ResolvePrimaryAssigneeUserId(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int responsibilityId,
        int departmentId)
    {
        var explicitOwnerUserId = await LoadExplicitResponsibilityOwnerUserId(connection, transaction, responsibilityId);
        if (explicitOwnerUserId.HasValue)
        {
            return explicitOwnerUserId.Value;
        }

        var effectiveDepartmentId = await LoadResponsibilityDepartmentOverrideId(connection, transaction, responsibilityId)
            ?? departmentId;

        const string sql = @"
SELECT u.id
FROM app_users u
JOIN app_user_responsibilities ur ON ur.app_user_id = u.id
LEFT JOIN app_responsibilities r ON r.id = ur.app_responsibility_id
WHERE u.is_active = TRUE
  AND ur.app_responsibility_id = @responsibilityId
  AND (
      r.department_id = @effectiveDepartmentId
      OR r.department_id IS NULL
      OR u.department_id = @effectiveDepartmentId
      OR u.department_id IS NULL
  )
ORDER BY CASE WHEN u.department_id = @effectiveDepartmentId THEN 0 ELSE 1 END, u.id
LIMIT 1;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("responsibilityId", responsibilityId);
        command.Parameters.AddWithValue("effectiveDepartmentId", effectiveDepartmentId);

        var scalar = await command.ExecuteScalarAsync();
        return scalar is null ? null : (long?)scalar;
    }

    private static async Task<(long? UserId, int? ResponsibilityId)> ResolveDepartmentRequirementSelectionAssignment(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int departmentId)
    {
        const string sql = @"
SELECT
    COALESCE(requirement_user.id, lead_user.id) AS user_id,
    r.id AS responsibility_id
FROM departments d
LEFT JOIN department_settings ds ON ds.department_id = d.id
LEFT JOIN people requirement_person ON requirement_person.id = ds.requirement_approver_person_id
LEFT JOIN app_users requirement_user ON requirement_user.id = requirement_person.app_user_id AND requirement_user.is_active = TRUE
LEFT JOIN people lead_person ON lead_person.id = ds.department_lead_person_id
LEFT JOIN app_users lead_user ON lead_user.id = lead_person.app_user_id AND lead_user.is_active = TRUE
LEFT JOIN app_responsibilities r
    ON r.department_id = d.id
   AND r.responsibility_type = 'department_lead'
   AND r.is_active = TRUE
WHERE d.id = @departmentId
LIMIT 1;";

        await using (var command = new NpgsqlCommand(sql, connection, transaction))
        {
            command.Parameters.AddWithValue("departmentId", departmentId);
            await using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                long? userId = reader.IsDBNull(0) ? null : reader.GetInt64(0);
                int? responsibilityId = reader.IsDBNull(1) ? null : reader.GetInt32(1);
                if (userId.HasValue || responsibilityId.HasValue)
                {
                    return (userId, responsibilityId);
                }
            }
        }

        var fallbackResponsibilityId = await LoadDepartmentLeadResponsibilityId(connection, transaction, departmentId);
        if (!fallbackResponsibilityId.HasValue)
        {
            return (null, null);
        }

        var fallbackUserId = await ResolvePrimaryAssigneeUserId(
            connection,
            transaction,
            fallbackResponsibilityId.Value,
            departmentId);

        return (fallbackUserId, fallbackResponsibilityId.Value);
    }

    private static async Task<(long UserId, string DisplayName, string Email, string IdentityKey, string PreferredPath)?> LoadActiveUserNotificationRecipient(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long userId)
    {
        const string sql = @"
WITH effective_roles AS (
    SELECT r.role_key
    FROM app_user_roles ur
    JOIN app_roles r ON r.id = ur.app_role_id
    WHERE ur.app_user_id = @userId
      AND r.is_active = TRUE
      AND r.role_kind = 'system'
    UNION
    SELECT r.role_key
    FROM app_user_groups ug
    JOIN app_group_roles gr ON gr.app_group_id = ug.app_group_id
    JOIN app_roles r ON r.id = gr.app_role_id
    WHERE ug.app_user_id = @userId
      AND r.is_active = TRUE
      AND r.role_kind = 'system'
)
SELECT
    u.id,
    u.display_name,
    COALESCE(NULLIF(BTRIM(u.notification_email), ''), u.email) AS target_email,
    COALESCE(NULLIF(BTRIM(u.external_key), ''), u.email) AS identity_key,
    EXISTS (
        SELECT 1
        FROM effective_roles er
        WHERE er.role_key IN ('auth_hr', 'auth_admin', 'auth_reader')
    ) AS can_access_workflow_overview,
    EXISTS (
        SELECT 1
        FROM effective_roles er
        WHERE er.role_key = 'auth_manager'
    ) AS can_access_supervisor
FROM app_users u
WHERE u.id = @userId
  AND u.is_active = TRUE
LIMIT 1;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("userId", userId);

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }

        var canAccessWorkflowOverview = reader.GetBoolean(4);
        var canAccessSupervisor = reader.GetBoolean(5);

        var preferredPath = canAccessWorkflowOverview
            ? "/workflows"
            : canAccessSupervisor
                ? "/supervisor"
                : "/tasks/my";

        return (reader.GetInt64(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), preferredPath);
    }

    private static async Task<WorkflowNotificationDispatchTarget> InsertWorkflowNotification(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long workflowId,
        long? workflowTaskId,
        long recipientUserId,
        string targetName,
        string targetEmail,
        string notificationType)
    {
        const string sql = @"
INSERT INTO workflow_notifications (
    workflow_id,
    workflow_task_id,
    recipient_user_id,
    target_email,
    target_name,
    notification_type,
    status
)
VALUES (
    @workflowId,
    @workflowTaskId,
    @recipientUserId,
    @targetEmail,
    @targetName,
    @notificationType,
    'pending'
)
RETURNING id, workflow_task_id, notification_type, target_name, target_email;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("workflowId", workflowId);
        command.Parameters.Add("workflowTaskId", NpgsqlDbType.Bigint).Value = (object?)workflowTaskId ?? DBNull.Value;
        command.Parameters.AddWithValue("recipientUserId", recipientUserId);
        command.Parameters.AddWithValue("targetEmail", targetEmail);
        command.Parameters.AddWithValue("targetName", targetName);
        command.Parameters.AddWithValue("notificationType", notificationType);

        await using var reader = await command.ExecuteReaderAsync();
        await reader.ReadAsync();
        return new WorkflowNotificationDispatchTarget
        {
            NotificationId = reader.GetInt64(0),
            WorkflowTaskId = reader.IsDBNull(1) ? null : reader.GetInt64(1),
            NotificationType = reader.GetString(2),
            RecipientUserId = recipientUserId,
            RecipientIdentityKey = null,
            TargetName = reader.GetString(3),
            TargetEmail = reader.GetString(4),
            TaskTitle = null,
            PreferredPath = null
        };
    }

    private static async Task<List<WorkflowNotificationDispatchTarget>> CreateWorkflowNotifications(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long workflowId,
        int departmentId)
    {
        var recipients = new Dictionary<long, (string DisplayName, string Email, string IdentityKey, string PreferredPath)>();

        var hrResponsibilityId = await LoadResponsibilityIdByKey(connection, transaction, "hr_onboarding");
        if (hrResponsibilityId.HasValue)
        {
            var hrUserId = await ResolvePrimaryAssigneeUserId(connection, transaction, hrResponsibilityId.Value, departmentId);
            if (hrUserId.HasValue)
            {
                var hrRecipient = await LoadActiveUserNotificationRecipient(connection, transaction, hrUserId.Value);
                if (hrRecipient.HasValue)
                {
                    recipients[hrRecipient.Value.UserId] = (
                        hrRecipient.Value.DisplayName,
                        hrRecipient.Value.Email,
                        hrRecipient.Value.IdentityKey,
                        hrRecipient.Value.PreferredPath);
                }
            }
        }

        var departmentSelectionAssignment = await ResolveDepartmentRequirementSelectionAssignment(
            connection,
            transaction,
            departmentId);

        if (departmentSelectionAssignment.UserId.HasValue)
        {
            var departmentRecipient = await LoadActiveUserNotificationRecipient(
                connection,
                transaction,
                departmentSelectionAssignment.UserId.Value);

            if (departmentRecipient.HasValue)
            {
                recipients[departmentRecipient.Value.UserId] = (
                    departmentRecipient.Value.DisplayName,
                    departmentRecipient.Value.Email,
                    departmentRecipient.Value.IdentityKey,
                    departmentRecipient.Value.PreferredPath);
            }
        }

        var targets = new List<WorkflowNotificationDispatchTarget>();
        foreach (var recipient in recipients)
        {
            targets.Add(await InsertWorkflowNotification(
                connection,
                transaction,
                workflowId,
                null,
                recipient.Key,
                recipient.Value.DisplayName,
                recipient.Value.Email,
                "workflow_created"));

            targets[^1] = new WorkflowNotificationDispatchTarget
            {
                NotificationId = targets[^1].NotificationId,
                WorkflowTaskId = null,
                NotificationType = "workflow_created",
                RecipientUserId = recipient.Key,
                RecipientIdentityKey = recipient.Value.IdentityKey,
                TargetName = recipient.Value.DisplayName,
                TargetEmail = recipient.Value.Email,
                TaskTitle = null,
                PreferredPath = recipient.Value.PreferredPath
            };
        }

        return targets;
    }

    private static async Task<List<WorkflowNotificationDispatchTarget>> CreateReadyTaskNotifications(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long workflowId)
    {
        const string recipientSql = @"
SELECT
    DISTINCT ta.assignee_user_id
FROM workflow_tasks wt
JOIN task_assignments ta
    ON ta.workflow_task_id = wt.id
   AND ta.is_primary = TRUE
WHERE wt.workflow_id = @workflowId
  AND wt.status IN ('open', 'ready', 'blocked', 'in_progress')
  AND wt.task_key <> @supervisorTaskKey
  AND ta.assignee_user_id IS NOT NULL
  AND NOT EXISTS (
      SELECT 1
      FROM workflow_notifications n
      WHERE n.workflow_id = @workflowId
        AND n.recipient_user_id = ta.assignee_user_id
        AND n.notification_type = 'task_ready'
  )
ORDER BY ta.assignee_user_id;";

        var recipientUserIds = new List<long>();
        await using (var command = new NpgsqlCommand(recipientSql, connection, transaction))
        {
            command.Parameters.AddWithValue("workflowId", workflowId);
            command.Parameters.AddWithValue("supervisorTaskKey", SupervisorRequirementTaskKey);

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                recipientUserIds.Add(reader.GetInt64(0));
            }
        }

        if (recipientUserIds.Count == 0)
        {
            return [];
        }

        const string taskSql = @"
SELECT
    ta.assignee_user_id,
    wt.id,
    wt.title
FROM workflow_tasks wt
JOIN task_assignments ta
    ON ta.workflow_task_id = wt.id
   AND ta.is_primary = TRUE
WHERE wt.workflow_id = @workflowId
  AND wt.status IN ('open', 'ready', 'blocked', 'in_progress')
  AND wt.task_key <> @supervisorTaskKey
  AND ta.assignee_user_id = ANY(@recipientUserIds)
ORDER BY ta.assignee_user_id, wt.sort_order, wt.id;";

        var taskTitlesByRecipient = recipientUserIds.ToDictionary(
            recipientUserId => recipientUserId,
            _ => new List<(long WorkflowTaskId, string TaskTitle)>());

        await using (var taskCommand = new NpgsqlCommand(taskSql, connection, transaction))
        {
            taskCommand.Parameters.AddWithValue("workflowId", workflowId);
            taskCommand.Parameters.AddWithValue("supervisorTaskKey", SupervisorRequirementTaskKey);
            taskCommand.Parameters.Add("recipientUserIds", NpgsqlDbType.Array | NpgsqlDbType.Bigint).Value = recipientUserIds.ToArray();

            await using var reader = await taskCommand.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var recipientUserId = reader.GetInt64(0);
                if (!taskTitlesByRecipient.TryGetValue(recipientUserId, out var recipientTasks))
                {
                    continue;
                }

                recipientTasks.Add((reader.GetInt64(1), reader.GetString(2)));
            }
        }

        var targets = new List<WorkflowNotificationDispatchTarget>();
        foreach (var recipientUserId in recipientUserIds)
        {
            if (!taskTitlesByRecipient.TryGetValue(recipientUserId, out var recipientTasks) || recipientTasks.Count == 0)
            {
                continue;
            }

            var recipient = await LoadActiveUserNotificationRecipient(connection, transaction, recipientUserId);
            if (!recipient.HasValue)
            {
                continue;
            }

            foreach (var recipientTask in recipientTasks)
            {
                var inserted = await InsertWorkflowNotification(
                    connection,
                    transaction,
                    workflowId,
                    recipientTask.WorkflowTaskId,
                    recipientUserId,
                    recipient.Value.DisplayName,
                    recipient.Value.Email,
                    "task_ready");

                targets.Add(new WorkflowNotificationDispatchTarget
                {
                    NotificationId = inserted.NotificationId,
                    WorkflowTaskId = recipientTask.WorkflowTaskId,
                    NotificationType = "task_ready",
                    RecipientUserId = recipientUserId,
                    RecipientIdentityKey = recipient.Value.IdentityKey,
                    TargetName = recipient.Value.DisplayName,
                    TargetEmail = recipient.Value.Email,
                    TaskTitle = recipientTask.TaskTitle,
                    PreferredPath = recipient.Value.PreferredPath
                });
            }
        }

        return targets;
    }

    private static void ValidateSupervisorSelections(
        IReadOnlyDictionary<string, StoredWorkflowAnswerRecord> answersByKey)
    {
        if (IsTrueAnswer(answersByKey, "ad_user_requested")
            && IsTrueAnswer(answersByKey, "comparison_user_available")
            && !HasTextAnswer(answersByKey, "comparison_user_name"))
        {
            throw new InvalidOperationException("Bitte den Referenzuser angeben.");
        }

        if (IsTrueAnswer(answersByKey, "hardware_requested")
            && !IsBooleanAnswered(answersByKey, "hardware_available"))
        {
            throw new InvalidOperationException("Bitte angeben, ob bereits passende Hardware vorhanden ist.");
        }

        if (IsTrueAnswer(answersByKey, "hardware_requested")
            && !HasSelectedOption(answersByKey, "hardware_type"))
        {
            throw new InvalidOperationException("Bitte die Hardware auswählen.");
        }

        if (IsTrueAnswer(answersByKey, "hardware_requested")
            && IsSelectedOptionValue(answersByKey, "hardware_type", "laptop")
            && !HasSelectedOption(answersByKey, "laptop_vpn_type"))
        {
            throw new InvalidOperationException("Bitte auswählen, ob der Laptop mit VPN oder ohne VPN benötigt wird.");
        }
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
                GetTextAnswer(answersByKey, "comparison_user_name") is { Length: > 0 } comparisonUserName
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

    private static bool IsTrueAnswer(
        IReadOnlyDictionary<string, StoredWorkflowAnswerRecord> answersByKey,
        string answerKey)
    {
        return answersByKey.TryGetValue(answerKey, out var answer) && answer.ValueBoolean == true;
    }

    private static bool IsBooleanAnswered(
        IReadOnlyDictionary<string, StoredWorkflowAnswerRecord> answersByKey,
        string answerKey)
    {
        return answersByKey.TryGetValue(answerKey, out var answer) && answer.ValueBoolean.HasValue;
    }

    private static bool HasTextAnswer(
        IReadOnlyDictionary<string, StoredWorkflowAnswerRecord> answersByKey,
        string answerKey)
    {
        return !string.IsNullOrWhiteSpace(GetTextAnswer(answersByKey, answerKey));
    }

    private static string? GetTextAnswer(
        IReadOnlyDictionary<string, StoredWorkflowAnswerRecord> answersByKey,
        string answerKey)
    {
        if (!answersByKey.TryGetValue(answerKey, out var answer) || string.IsNullOrWhiteSpace(answer.ValueText))
        {
            return null;
        }

        return answer.ValueText.Trim();
    }

    private static bool HasSelectedOption(
        IReadOnlyDictionary<string, StoredWorkflowAnswerRecord> answersByKey,
        string answerKey)
    {
        return answersByKey.TryGetValue(answerKey, out var answer)
               && (answer.SelectedOptionId.HasValue || answer.SelectedOptionIds.Count > 0);
    }

    private static bool IsSelectedOptionValue(
        IReadOnlyDictionary<string, StoredWorkflowAnswerRecord> answersByKey,
        string answerKey,
        string expectedValue)
    {
        if (!answersByKey.TryGetValue(answerKey, out var answer))
        {
            return false;
        }

        var values = new List<string>();
        if (!string.IsNullOrWhiteSpace(answer.SelectedOptionValue))
        {
            values.Add(answer.SelectedOptionValue);
        }

        values.AddRange(answer.SelectedOptionValues.Where(value => !string.IsNullOrWhiteSpace(value)));

        return values.Any(value => string.Equals(value.Trim(), expectedValue, StringComparison.OrdinalIgnoreCase));
    }

    private static string? GetHardwareTypeText(
        IReadOnlyDictionary<string, StoredWorkflowAnswerRecord> answersByKey)
    {
        if (!answersByKey.TryGetValue("hardware_type", out var answer))
        {
            return null;
        }

        var rawValue = !string.IsNullOrWhiteSpace(answer.SelectedOptionValue)
            ? answer.SelectedOptionValue
            : answer.SelectedOptionValues.FirstOrDefault();

        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return null;
        }

        var hardwareType = string.Join(
            " ",
            rawValue
                .Trim()
                .Replace("-", " ")
                .Replace("_", " ")
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(token => char.ToUpperInvariant(token[0]) + token[1..].ToLowerInvariant()));

        if (!string.Equals(rawValue.Trim(), "laptop", StringComparison.OrdinalIgnoreCase))
        {
            return hardwareType;
        }

        if (!answersByKey.TryGetValue("laptop_vpn_type", out var laptopVpnAnswer))
        {
            return hardwareType;
        }

        var vpnValue = !string.IsNullOrWhiteSpace(laptopVpnAnswer.SelectedOptionValue)
            ? laptopVpnAnswer.SelectedOptionValue
            : laptopVpnAnswer.SelectedOptionValues.FirstOrDefault();

        return string.IsNullOrWhiteSpace(vpnValue)
            ? hardwareType
            : $"{hardwareType} ({NormalizeOptionDisplayText(vpnValue)})";
    }

    private static string NormalizeOptionDisplayText(string rawValue)
    {
        return string.Join(
            " ",
            rawValue
                .Trim()
                .Replace("-", " ")
                .Replace("_", " ")
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(token => char.ToUpperInvariant(token[0]) + token[1..].ToLowerInvariant()));
    }

    private static async Task<bool> RoleExists(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        int roleId)
    {
        const string sql = @"
SELECT EXISTS(
    SELECT 1
    FROM app_roles
    WHERE id = @roleId
      AND role_kind = 'position'
      AND is_active = TRUE
);";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("roleId", roleId);
        return (bool)(await command.ExecuteScalarAsync() ?? false);
    }

    private static async Task EnsureValidPositionRole(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int roleId,
        int departmentId)
    {
        const string sql = @"
SELECT r.id
FROM app_roles r
WHERE r.id = @roleId
  AND r.department_id = @departmentId
  AND r.role_kind = 'position'
  AND r.is_active = TRUE;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("roleId", roleId);
        command.Parameters.AddWithValue("departmentId", departmentId);

        var scalar = await command.ExecuteScalarAsync();
        if (scalar is null)
        {
            throw new InvalidOperationException("Role is invalid for the selected department.");
        }
    }

    private static async Task LoadWorkflowRequirements(
        NpgsqlConnection connection,
        long workflowId,
        List<WorkflowRequirementSnapshotDto> requirements)
    {
        const string sql = @"
SELECT
    d.id,
    d.answer_key,
    d.title,
    d.description,
    d.category,
    d.icon_key,
    d.input_type,
    d.is_required,
    d.sort_order,
    o.id,
    o.option_key,
    o.option_value,
    o.option_label,
    o.sort_order,
    a.id,
    a.value_boolean,
    a.value_text,
    a.value_number,
    a.selected_option_id
FROM workflow_answer_definitions d
LEFT JOIN workflow_answer_options o ON o.answer_definition_id = d.id
LEFT JOIN workflow_answers a
    ON a.workflow_id = @workflowId
    AND a.answer_definition_id = d.id
WHERE d.is_active = TRUE
ORDER BY d.sort_order, d.id, o.sort_order, o.id;";

        var requirementById = new Dictionary<int, WorkflowRequirementSnapshotDto>();

        await using (var command = new NpgsqlCommand(sql, connection))
        {
            command.Parameters.AddWithValue("workflowId", workflowId);
            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var definitionId = reader.GetInt32(0);
                if (!requirementById.TryGetValue(definitionId, out var requirement))
                {
                    var workflowRequirementId = reader.IsDBNull(14) ? definitionId : reader.GetInt64(14);
                    int? selectedOptionId = reader.IsDBNull(18) ? null : reader.GetInt32(18);

                    requirement = new WorkflowRequirementSnapshotDto
                    {
                        WorkflowRequirementId = workflowRequirementId,
                        Id = definitionId,
                        Key = reader.GetString(1),
                        Title = reader.GetString(2),
                        Description = reader.GetString(3),
                        Category = reader.GetString(4),
                        IconKey = reader.GetString(5),
                        InputType = reader.GetString(6),
                        IsRequired = reader.GetBoolean(7),
                        SortOrder = reader.GetInt32(8),
                        Options = new List<WorkflowRequirementOptionSnapshotDto>(),
                        Value = new WorkflowRequirementValueDto
                        {
                            ValueBoolean = reader.IsDBNull(15) ? null : reader.GetBoolean(15),
                            ValueText = reader.IsDBNull(16) ? null : reader.GetString(16),
                            ValueNumber = reader.IsDBNull(17) ? null : reader.GetDecimal(17),
                            SelectedOptionId = selectedOptionId,
                            SelectedOptionKey = null,
                            SelectedOptionValue = null,
                            SelectedOptionLabel = null,
                            SelectedOptions = new List<WorkflowRequirementSelectedOptionDto>()
                        }
                    };

                    requirementById.Add(definitionId, requirement);
                    requirements.Add(requirement);
                }

                if (!reader.IsDBNull(9))
                {
                    requirement.Options.Add(new WorkflowRequirementOptionSnapshotDto
                    {
                        Id = reader.GetInt32(9),
                        SourceOptionId = reader.GetInt32(9),
                        Key = reader.GetString(10),
                        Value = reader.GetString(11),
                        Label = reader.GetString(12),
                        SortOrder = reader.GetInt32(13)
                    });
                }
            }
        }

        const string selectedOptionsSql = @"
SELECT
    a.answer_definition_id,
    o.id,
    o.option_key,
    o.option_value,
    o.option_label,
    o.sort_order
FROM workflow_answers a
JOIN workflow_answer_selected_options aso ON aso.workflow_answer_id = a.id
JOIN workflow_answer_options o ON o.id = aso.answer_option_id
WHERE a.workflow_id = @workflowId
ORDER BY a.answer_definition_id, o.sort_order, o.id;";

        await using (var selectedOptionsCommand = new NpgsqlCommand(selectedOptionsSql, connection))
        {
            selectedOptionsCommand.Parameters.AddWithValue("workflowId", workflowId);
            await using var reader = await selectedOptionsCommand.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var definitionId = reader.GetInt32(0);
                if (!requirementById.TryGetValue(definitionId, out var requirement))
                {
                    continue;
                }

                requirement.Value.SelectedOptions.Add(new WorkflowRequirementSelectedOptionDto
                {
                    Id = reader.GetInt32(1),
                    Key = reader.GetString(2),
                    Value = reader.GetString(3),
                    Label = reader.GetString(4),
                    SortOrder = reader.GetInt32(5)
                });
            }
        }

        foreach (var requirement in requirements)
        {
            var selectedOption = requirement.Value.SelectedOptionId.HasValue
                ? requirement.Options.FirstOrDefault(option => option.Id == requirement.Value.SelectedOptionId.Value)
                : null;

            requirement.Value = new WorkflowRequirementValueDto
            {
                ValueBoolean = requirement.Value.ValueBoolean,
                ValueText = requirement.Value.ValueText,
                ValueNumber = requirement.Value.ValueNumber,
                SelectedOptionId = requirement.Value.SelectedOptionId,
                SelectedOptionKey = selectedOption?.Key,
                SelectedOptionValue = selectedOption?.Value,
                SelectedOptionLabel = selectedOption?.Label,
                SelectedOptions = requirement.Value.SelectedOptions
            };
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
                        reader.GetString(2),
                        reader.IsDBNull(15) ? null : reader.GetString(15),
                        reader.IsDBNull(16) ? null : reader.GetString(16)),
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

    private static string? ResolveTaskProcessArea(
        string taskKey,
        string? responsibilityDepartmentName,
        string? responsibilityType)
    {
        if (taskKey.Equals(SupervisorRequirementTaskKey, StringComparison.OrdinalIgnoreCase)
            || string.Equals(responsibilityType, "department_lead", StringComparison.OrdinalIgnoreCase))
        {
            return "Abteilungsleitung";
        }

        if (taskKey.Equals("contract_archived", StringComparison.OrdinalIgnoreCase)
            || taskKey.Equals("kaba_user_created", StringComparison.OrdinalIgnoreCase)
            || taskKey.Equals("document_sent_to_distribution", StringComparison.OrdinalIgnoreCase))
        {
            return "HR";
        }

        if (string.IsNullOrWhiteSpace(responsibilityDepartmentName))
        {
            return null;
        }

        return responsibilityDepartmentName.Trim().ToUpperInvariant() switch
        {
            "HR" => "HR",
            "IT" => "IT",
            "QS" => "QS",
            "AV" => "AV",
            "QMB" => "QMB",
            _ => null
        };
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
