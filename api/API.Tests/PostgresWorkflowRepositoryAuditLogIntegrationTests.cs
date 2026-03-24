using Npgsql;
using Xunit;

namespace API.Tests;

[Collection(PostgresWorkflowRepositoryIntegrationCollection.Name)]
public sealed class PostgresWorkflowRepositoryAuditLogIntegrationTests
{
    private const string DefaultTestConnectionString = "Host=localhost;Port=23456;Database=appdb;Username=app;Password=app_pw";

    [Fact]
    [Trait("Category", "Integration")]
    public async Task UpdateTaskAssignment_WritesReadableAssigneeLabels()
    {
        var connectionString = GetTestConnectionString();
        var departmentId = await LoadDepartmentIdAsync(connectionString, "IT");
        var actorUserId = await LoadUserIdAsync(connectionString, "admin.demo@demo.local");
        var assigneeUserId = await LoadUserIdAsync(connectionString, "vinzent.niederwieser@demo.local");
        var initialResponsibilityId = await LoadResponsibilityIdAsync(connectionString, "it_ad");
        var nextResponsibilityId = await LoadResponsibilityIdAsync(connectionString, "it_hardware");
        var temporaryRoleId = await CreateTemporaryPositionRoleAsync(connectionString, departmentId);
        var workflow = await CreateManualWorkflowAsync(
            connectionString,
            departmentId,
            temporaryRoleId,
            "waiting_for_department",
            actorUserId,
            temporaryRoleId);

        try
        {
            var taskId = await CreateTaskAsync(connectionString, workflow.WorkflowId, "audit_assignment_task", "ready", 10);
            await InsertPrimaryResponsibilityAssignmentAsync(connectionString, taskId, initialResponsibilityId);

            await WithRepositoryConnectionStringAsync(connectionString, async repository =>
            {
                await repository.UpdateTaskAssignment(
                    taskId,
                    new TaskAssignRequest
                    {
                        AssigneeUserId = assigneeUserId
                    },
                    actorUserId);

                await repository.UpdateTaskAssignment(
                    taskId,
                    new TaskAssignRequest
                    {
                        AssigneeResponsibilityId = nextResponsibilityId
                    },
                    actorUserId);

                var assignmentEntries = (await repository.GetWorkflowAuditLog(workflow.WorkflowUid, 10, 0))
                    .Where(entry => entry.EventType == "task_assigned")
                    .Take(2)
                    .ToList();

                Assert.Equal(2, assignmentEntries.Count);
                Assert.Equal("Vinzent Niederwieser", assignmentEntries[0].OldValue);
                Assert.Equal("IT - Hardware", assignmentEntries[0].NewValue);
                Assert.Equal("IT - AD", assignmentEntries[1].OldValue);
                Assert.Equal("Vinzent Niederwieser", assignmentEntries[1].NewValue);
            });
        }
        finally
        {
            await CleanupManualWorkflowAsync(connectionString, workflow);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task AddTaskComment_WritesNormalizedCommentTextToAuditDetail()
    {
        var connectionString = GetTestConnectionString();
        var departmentId = await LoadDepartmentIdAsync(connectionString, "IT");
        var actorUserId = await LoadUserIdAsync(connectionString, "admin.demo@demo.local");
        var temporaryRoleId = await CreateTemporaryPositionRoleAsync(connectionString, departmentId);
        var workflow = await CreateManualWorkflowAsync(
            connectionString,
            departmentId,
            temporaryRoleId,
            "waiting_for_department",
            actorUserId,
            temporaryRoleId);

        try
        {
            var taskId = await CreateTaskAsync(connectionString, workflow.WorkflowId, "audit_comment_task", "ready", 10);

            await WithRepositoryConnectionStringAsync(connectionString, async repository =>
            {
                await repository.AddTaskComment(taskId, "  Audit-Kommentar  ", actorUserId);

                var commentEntry = (await repository.GetWorkflowAuditLog(workflow.WorkflowUid, 10, 0))
                    .Single(entry => entry.EventType == "task_comment_added");

                Assert.Equal("Audit-Kommentar", commentEntry.Detail);
                Assert.DoesNotContain("Task:", commentEntry.Detail, StringComparison.Ordinal);
            });
        }
        finally
        {
            await CleanupManualWorkflowAsync(connectionString, workflow);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetWorkflowAuditLog_AppliesLimitAndOffsetInDescendingOrder()
    {
        var connectionString = GetTestConnectionString();
        var departmentId = await LoadDepartmentIdAsync(connectionString, "IT");
        var actorUserId = await LoadUserIdAsync(connectionString, "admin.demo@demo.local");
        var temporaryRoleId = await CreateTemporaryPositionRoleAsync(connectionString, departmentId);
        var workflow = await CreateManualWorkflowAsync(
            connectionString,
            departmentId,
            temporaryRoleId,
            "draft",
            actorUserId,
            temporaryRoleId);

        try
        {
            var sharedCreatedAt = new DateTime(2026, 3, 24, 10, 0, 0, DateTimeKind.Utc);
            var olderCreatedAt = sharedCreatedAt.AddHours(-1);

            await InsertAuditLogRowAsync(connectionString, workflow.WorkflowId, actorUserId, "custom_event_a", "A", sharedCreatedAt);
            await InsertAuditLogRowAsync(connectionString, workflow.WorkflowId, actorUserId, "custom_event_b", "B", sharedCreatedAt);
            await InsertAuditLogRowAsync(connectionString, workflow.WorkflowId, actorUserId, "custom_event_c", "C", olderCreatedAt);

            await WithRepositoryConnectionStringAsync(connectionString, async repository =>
            {
                var allEntries = await repository.GetWorkflowAuditLog(workflow.WorkflowUid, 3, 0);
                Assert.Equal(new[] { "B", "A", "C" }, allEntries.Select(entry => entry.Detail).ToArray());

                var pagedEntries = await repository.GetWorkflowAuditLog(workflow.WorkflowUid, 2, 1);
                Assert.Equal(new[] { "A", "C" }, pagedEntries.Select(entry => entry.Detail).ToArray());
            });
        }
        finally
        {
            await CleanupManualWorkflowAsync(connectionString, workflow);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task CreateWorkflow_WritesTasksGeneratedEvent()
    {
        var connectionString = GetTestConnectionString();
        var departmentId = await LoadDepartmentIdAsync(connectionString, "IT");
        var roleId = await LoadRoleIdAsync(connectionString, "position_developer");
        var actorUserId = await LoadUserIdAsync(connectionString, "laura.romankewicz@demo.local");

        await WithRepositoryConnectionStringAsync(connectionString, async repository =>
        {
            var creation = await repository.CreateWorkflow(
                CreateWorkflowRequest(departmentId, roleId),
                actorUserId);

            try
            {
                var tasksGeneratedEntries = (await repository.GetWorkflowAuditLog(creation.Uid, 20, 0))
                    .Where(entry => entry.EventType == "tasks_generated")
                    .ToList();

                Assert.Single(tasksGeneratedEntries);
                Assert.EndsWith("Aufgabe(n) initial erstellt", tasksGeneratedEntries[0].Detail, StringComparison.Ordinal);
            }
            finally
            {
                await DeleteWorkflowAsync(connectionString, creation.WorkflowId);
            }
        });
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task CompleteSupervisorStep_WritesTasksGeneratedEventForPostSupervisorTasks()
    {
        var connectionString = GetTestConnectionString();
        var departmentId = await LoadDepartmentIdAsync(connectionString, "IT");
        var roleId = await LoadRoleIdAsync(connectionString, "position_developer");
        var createdByUserId = await LoadUserIdAsync(connectionString, "laura.romankewicz@demo.local");
        var actorUserId = await LoadUserIdAsync(connectionString, "tobias.lueck@demo.local");

        await WithRepositoryConnectionStringAsync(connectionString, async repository =>
        {
            var creation = await repository.CreateWorkflow(
                CreateWorkflowRequest(departmentId, roleId),
                createdByUserId);

            try
            {
                var completedWorkflow = await repository.CompleteSupervisorStep(
                    creation.Uid,
                    await BuildPositiveSupervisorSelectionsAsync(connectionString),
                    actorUserId);

                Assert.NotNull(completedWorkflow);

                var tasksGeneratedEntries = (await repository.GetWorkflowAuditLog(creation.Uid, 20, 0))
                    .Where(entry => entry.EventType == "tasks_generated")
                    .ToList();

                Assert.Equal(2, tasksGeneratedEntries.Count);
                Assert.Contains(
                    tasksGeneratedEntries,
                    entry => entry.Detail?.EndsWith("Aufgabe(n) initial erstellt", StringComparison.Ordinal) == true);
                Assert.Contains(
                    tasksGeneratedEntries,
                    entry => entry.Detail?.EndsWith("Aufgabe(n) nach Anforderungsauswahl erstellt", StringComparison.Ordinal) == true);
            }
            finally
            {
                await DeleteWorkflowAsync(connectionString, creation.WorkflowId);
            }
        });
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task CompleteSupervisorStep_DoesNotWriteTasksGeneratedEventWhenNoPostSupervisorTasksAreCreated()
    {
        var connectionString = GetTestConnectionString();
        var departmentId = await LoadDepartmentIdAsync(connectionString, "IT");
        var roleId = await LoadRoleIdAsync(connectionString, "position_developer");
        var createdByUserId = await LoadUserIdAsync(connectionString, "laura.romankewicz@demo.local");
        var actorUserId = await LoadUserIdAsync(connectionString, "tobias.lueck@demo.local");

        await WithRepositoryConnectionStringAsync(connectionString, async repository =>
        {
            var creation = await repository.CreateWorkflow(
                CreateWorkflowRequest(departmentId, roleId),
                createdByUserId);

            try
            {
                var completedWorkflow = await repository.CompleteSupervisorStep(
                    creation.Uid,
                    await BuildZeroTaskSupervisorSelectionsAsync(connectionString),
                    actorUserId);

                Assert.NotNull(completedWorkflow);

                var tasksGeneratedEntries = (await repository.GetWorkflowAuditLog(creation.Uid, 20, 0))
                    .Where(entry => entry.EventType == "tasks_generated")
                    .ToList();

                Assert.Single(tasksGeneratedEntries);
                Assert.DoesNotContain(
                    tasksGeneratedEntries,
                    entry => entry.Detail?.EndsWith("Aufgabe(n) nach Anforderungsauswahl erstellt", StringComparison.Ordinal) == true);
            }
            finally
            {
                await DeleteWorkflowAsync(connectionString, creation.WorkflowId);
            }
        });
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task CompleteSupervisorStep_LegacyBackfill_WritesInitialAndPostSupervisorTasksGeneratedEvents()
    {
        var connectionString = GetTestConnectionString();
        var departmentId = await LoadDepartmentIdAsync(connectionString, "IT");
        var roleId = await LoadRoleIdAsync(connectionString, "position_developer");
        var createdByUserId = await LoadUserIdAsync(connectionString, "laura.romankewicz@demo.local");
        var actorUserId = await LoadUserIdAsync(connectionString, "tobias.lueck@demo.local");
        var workflow = await CreateManualWorkflowAsync(
            connectionString,
            departmentId,
            roleId,
            "waiting_for_supervisor",
            createdByUserId);

        try
        {
            await WithRepositoryConnectionStringAsync(connectionString, async repository =>
            {
                var completedWorkflow = await repository.CompleteSupervisorStep(
                    workflow.WorkflowUid,
                    await BuildPositiveSupervisorSelectionsAsync(connectionString),
                    actorUserId);

                Assert.NotNull(completedWorkflow);

                var tasksGeneratedEntries = (await repository.GetWorkflowAuditLog(workflow.WorkflowUid, 20, 0))
                    .Where(entry => entry.EventType == "tasks_generated")
                    .ToList();

                Assert.Equal(2, tasksGeneratedEntries.Count);
                Assert.Contains(
                    tasksGeneratedEntries,
                    entry => entry.Detail?.EndsWith("Aufgabe(n) initial erstellt", StringComparison.Ordinal) == true);
                Assert.Contains(
                    tasksGeneratedEntries,
                    entry => entry.Detail?.EndsWith("Aufgabe(n) nach Anforderungsauswahl erstellt", StringComparison.Ordinal) == true);
            });
        }
        finally
        {
            await DeleteWorkflowAsync(connectionString, workflow.WorkflowId);
        }
    }

    private static string GetTestConnectionString()
    {
        return Environment.GetEnvironmentVariable("ONBOARDING_TEST_CONNECTION_STRING")
               ?? DefaultTestConnectionString;
    }

    private static CreateWorkflowRequest CreateWorkflowRequest(int departmentId, int roleId)
    {
        var suffix = Guid.NewGuid().ToString("N");

        return new CreateWorkflowRequest
        {
            DepartmentId = departmentId,
            RoleId = roleId,
            FirstName = "Audit",
            LastName = $"Workflow-{suffix[..6]}",
            EmployeeNumber = Math.Abs(suffix[..8].GetHashCode()),
            BadgeNumber = Math.Abs(suffix[8..16].GetHashCode()),
            DeadlineDate = null
        };
    }

    private static async Task<List<RequirementSelectionInputDto>> BuildPositiveSupervisorSelectionsAsync(string connectionString)
    {
        var adUserRequestedId = await LoadAnswerDefinitionIdAsync(connectionString, "ad_user_requested");
        var mailboxRequestedId = await LoadAnswerDefinitionIdAsync(connectionString, "mailbox_requested");
        var hardwareRequestedId = await LoadAnswerDefinitionIdAsync(connectionString, "hardware_requested");
        var hardwareAvailableId = await LoadAnswerDefinitionIdAsync(connectionString, "hardware_available");
        var hardwareTypeId = await LoadAnswerDefinitionIdAsync(connectionString, "hardware_type");
        var laptopVpnTypeId = await LoadAnswerDefinitionIdAsync(connectionString, "laptop_vpn_type");
        var laptopOptionId = await LoadAnswerOptionIdAsync(connectionString, "hardware_type", "laptop");
        var withVpnOptionId = await LoadAnswerOptionIdAsync(connectionString, "laptop_vpn_type", "with_vpn");

        return new List<RequirementSelectionInputDto>
        {
            new() { RequirementId = adUserRequestedId, ValueBoolean = true },
            new() { RequirementId = mailboxRequestedId, ValueBoolean = true },
            new() { RequirementId = hardwareRequestedId, ValueBoolean = true },
            new() { RequirementId = hardwareAvailableId, ValueBoolean = false },
            new() { RequirementId = hardwareTypeId, SelectedOptionId = laptopOptionId },
            new() { RequirementId = laptopVpnTypeId, SelectedOptionId = withVpnOptionId }
        };
    }

    private static async Task<List<RequirementSelectionInputDto>> BuildZeroTaskSupervisorSelectionsAsync(string connectionString)
    {
        return new List<RequirementSelectionInputDto>
        {
            new() { RequirementId = await LoadAnswerDefinitionIdAsync(connectionString, "ad_user_requested"), ValueBoolean = false },
            new() { RequirementId = await LoadAnswerDefinitionIdAsync(connectionString, "mailbox_requested"), ValueBoolean = false },
            new() { RequirementId = await LoadAnswerDefinitionIdAsync(connectionString, "hardware_requested"), ValueBoolean = false }
        };
    }

    private static async Task<ManualWorkflowData> CreateManualWorkflowAsync(
        string connectionString,
        int departmentId,
        int roleId,
        string status,
        long createdByUserId,
        int? temporaryRoleId = null)
    {
        var suffix = Guid.NewGuid().ToString("N");

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        const string sql = @"
INSERT INTO workflows (
    department_id,
    onboarding_role_id,
    created_by_user_id,
    first_name,
    last_name,
    employee_number,
    badge_number,
    status
)
VALUES (
    @departmentId,
    @roleId,
    @createdByUserId,
    'Audit',
    @lastName,
    @employeeNumber,
    @badgeNumber,
    @status
)
RETURNING id, uid;";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("departmentId", departmentId);
        command.Parameters.AddWithValue("roleId", roleId);
        command.Parameters.AddWithValue("createdByUserId", createdByUserId);
        command.Parameters.AddWithValue("lastName", $"Audit-{suffix[..6]}");
        command.Parameters.AddWithValue("employeeNumber", Math.Abs(suffix[..8].GetHashCode()));
        command.Parameters.AddWithValue("badgeNumber", Math.Abs(suffix[8..16].GetHashCode()));
        command.Parameters.AddWithValue("status", status);

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            throw new InvalidOperationException("Manual workflow could not be created.");
        }

        return new ManualWorkflowData
        {
            WorkflowId = reader.GetInt64(0),
            WorkflowUid = reader.GetGuid(1),
            TemporaryRoleId = temporaryRoleId
        };
    }

    private static async Task<int> CreateTemporaryPositionRoleAsync(string connectionString, int departmentId)
    {
        var suffix = Guid.NewGuid().ToString("N");

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        const string sql = @"
INSERT INTO app_roles (department_id, role_key, name, role_kind, is_active)
VALUES (@departmentId, @roleKey, @name, 'position', TRUE)
RETURNING id;";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("departmentId", departmentId);
        command.Parameters.AddWithValue("roleKey", $"integration_audit_role_{suffix}");
        command.Parameters.AddWithValue("name", $"Integration Audit Role {suffix[..6]}");

        var scalar = await command.ExecuteScalarAsync();
        return scalar is int roleId
            ? roleId
            : throw new InvalidOperationException("Temporary role could not be created.");
    }

    private static async Task<long> CreateTaskAsync(
        string connectionString,
        long workflowId,
        string taskKey,
        string status,
        int sortOrder)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        const string sql = @"
INSERT INTO workflow_tasks (
    workflow_id,
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
    @taskKey,
    @title,
    'integration',
    @description,
    'integration',
    @status,
    TRUE,
    @sortOrder,
    CASE WHEN @status = 'ready' THEN NOW() ELSE NULL END
)
RETURNING id;";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("workflowId", workflowId);
        command.Parameters.AddWithValue("taskKey", taskKey);
        command.Parameters.AddWithValue("title", $"Task {taskKey}");
        command.Parameters.AddWithValue("description", $"Integration task {taskKey}");
        command.Parameters.AddWithValue("status", status);
        command.Parameters.AddWithValue("sortOrder", sortOrder);

        var scalar = await command.ExecuteScalarAsync();
        return scalar is long taskId
            ? taskId
            : throw new InvalidOperationException("Task could not be created.");
    }

    private static async Task InsertPrimaryResponsibilityAssignmentAsync(
        string connectionString,
        long taskId,
        int responsibilityId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        const string sql = @"
INSERT INTO task_assignments (
    workflow_task_id,
    assignee_responsibility_id,
    assignment_type,
    is_primary
)
VALUES (
    @taskId,
    @responsibilityId,
    'responsibility',
    TRUE
);";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("taskId", taskId);
        command.Parameters.AddWithValue("responsibilityId", responsibilityId);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task InsertAuditLogRowAsync(
        string connectionString,
        long workflowId,
        long actorUserId,
        string eventType,
        string detail,
        DateTime createdAt)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        const string sql = @"
INSERT INTO workflow_audit_log (
    workflow_id,
    actor_user_id,
    event_type,
    detail,
    created_at
)
VALUES (
    @workflowId,
    @actorUserId,
    @eventType,
    @detail,
    @createdAt
);";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("workflowId", workflowId);
        command.Parameters.AddWithValue("actorUserId", actorUserId);
        command.Parameters.AddWithValue("eventType", eventType);
        command.Parameters.AddWithValue("detail", detail);
        command.Parameters.AddWithValue("createdAt", createdAt);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<int> LoadDepartmentIdAsync(string connectionString, string departmentName)
    {
        return await LoadIntAsync(
            connectionString,
            "SELECT id FROM departments WHERE name = @value LIMIT 1;",
            departmentName);
    }

    private static async Task<int> LoadRoleIdAsync(string connectionString, string roleKey)
    {
        return await LoadIntAsync(
            connectionString,
            "SELECT id FROM app_roles WHERE role_key = @value LIMIT 1;",
            roleKey);
    }

    private static async Task<int> LoadResponsibilityIdAsync(string connectionString, string responsibilityKey)
    {
        return await LoadIntAsync(
            connectionString,
            "SELECT id FROM app_responsibilities WHERE responsibility_key = @value LIMIT 1;",
            responsibilityKey);
    }

    private static async Task<long> LoadUserIdAsync(string connectionString, string email)
    {
        return await LoadLongAsync(
            connectionString,
            "SELECT id FROM app_users WHERE email = @value LIMIT 1;",
            email);
    }

    private static async Task<int> LoadAnswerDefinitionIdAsync(string connectionString, string answerKey)
    {
        return await LoadIntAsync(
            connectionString,
            "SELECT id FROM workflow_answer_definitions WHERE answer_key = @value LIMIT 1;",
            answerKey);
    }

    private static async Task<int> LoadAnswerOptionIdAsync(
        string connectionString,
        string answerKey,
        string optionKey)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        const string sql = @"
SELECT o.id
FROM workflow_answer_options o
JOIN workflow_answer_definitions d ON d.id = o.answer_definition_id
WHERE d.answer_key = @answerKey
  AND o.option_key = @optionKey
LIMIT 1;";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("answerKey", answerKey);
        command.Parameters.AddWithValue("optionKey", optionKey);

        var scalar = await command.ExecuteScalarAsync();
        return scalar is int optionId
            ? optionId
            : throw new InvalidOperationException($"Answer option '{answerKey}:{optionKey}' could not be loaded.");
    }

    private static async Task<int> LoadIntAsync(string connectionString, string sql, string value)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("value", value);

        var scalar = await command.ExecuteScalarAsync();
        return scalar is int result
            ? result
            : throw new InvalidOperationException($"Integer lookup failed for value '{value}'.");
    }

    private static async Task<long> LoadLongAsync(string connectionString, string sql, string value)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("value", value);

        var scalar = await command.ExecuteScalarAsync();
        return scalar is long result
            ? result
            : throw new InvalidOperationException($"Long lookup failed for value '{value}'.");
    }

    private static async Task DeleteWorkflowAsync(string connectionString, long workflowId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            "DELETE FROM workflows WHERE id = @workflowId;",
            connection);
        command.Parameters.AddWithValue("workflowId", workflowId);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task DeleteRoleAsync(string connectionString, int roleId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            "DELETE FROM app_roles WHERE id = @roleId;",
            connection);
        command.Parameters.AddWithValue("roleId", roleId);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task CleanupManualWorkflowAsync(string connectionString, ManualWorkflowData workflow)
    {
        await DeleteWorkflowAsync(connectionString, workflow.WorkflowId);

        if (workflow.TemporaryRoleId.HasValue)
        {
            await DeleteRoleAsync(connectionString, workflow.TemporaryRoleId.Value);
        }
    }

    private static async Task WithRepositoryConnectionStringAsync(
        string connectionString,
        Func<PostgresWorkflowRepository, Task> action)
    {
        var previousConnectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING");
        Environment.SetEnvironmentVariable("CONNECTION_STRING", connectionString);

        try
        {
            await action(new PostgresWorkflowRepository());
        }
        finally
        {
            Environment.SetEnvironmentVariable("CONNECTION_STRING", previousConnectionString);
        }
    }

    private sealed class ManualWorkflowData
    {
        public required long WorkflowId { get; init; }
        public required Guid WorkflowUid { get; init; }
        public int? TemporaryRoleId { get; init; }
    }
}
