using Npgsql;
using Xunit;

namespace API.Tests;

[Collection(PostgresWorkflowRepositoryIntegrationCollection.Name)]
public sealed class PostgresWorkflowRepositoryTaskNarrowingIntegrationTests
{
    private const string DefaultTestConnectionString = "Host=localhost;Port=26432;Database=appdb;Username=app;Password=app_pw";

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetTasksForUserNarrowed_ReturnsOnlyMatchingPrimaryAssignmentsOnNonTerminalWorkflows()
    {
        var connectionString = GetTestConnectionString();
        var previousConnectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING");
        Environment.SetEnvironmentVariable("CONNECTION_STRING", connectionString);

        var fixture = await CreateNarrowingFixtureAsync(connectionString);
        try
        {
            var repository = new PostgresWorkflowRepository();

            var allTasks = await repository.GetTasksForUser(fixture.AssigneeUserId, new[] { fixture.MatchingResponsibilityId });
            // Sanity: unfiltered call returns at least our 4 fixture tasks (terminal-workflow task,
            // matching-responsibility task on active workflow, foreign-responsibility task,
            // user-assigned task). May include unrelated tasks from seed data — assert by id.
            Assert.Contains(allTasks, t => t.Task.Id == fixture.MatchingResponsibilityTaskId);
            Assert.Contains(allTasks, t => t.Task.Id == fixture.TerminalWorkflowTaskId);
            Assert.Contains(allTasks, t => t.Task.Id == fixture.ForeignResponsibilityTaskId);
            Assert.Contains(allTasks, t => t.Task.Id == fixture.UserAssignedTaskId);

            var narrowed = await repository.GetTasksForUserNarrowed(
                fixture.AssigneeUserId,
                new[] { fixture.MatchingResponsibilityId });

            // Narrowed result must:
            //  - INCLUDE matching-responsibility task on active workflow
            //  - INCLUDE direct-user assignment task
            //  - EXCLUDE task on terminal (completed) workflow even though responsibility matches
            //  - EXCLUDE task whose primary assignment is a non-matching responsibility
            Assert.Contains(narrowed, t => t.Task.Id == fixture.MatchingResponsibilityTaskId);
            Assert.Contains(narrowed, t => t.Task.Id == fixture.UserAssignedTaskId);
            Assert.DoesNotContain(narrowed, t => t.Task.Id == fixture.TerminalWorkflowTaskId);
            Assert.DoesNotContain(narrowed, t => t.Task.Id == fixture.ForeignResponsibilityTaskId);
        }
        finally
        {
            Environment.SetEnvironmentVariable("CONNECTION_STRING", previousConnectionString);
            await CleanupNarrowingFixtureAsync(connectionString, fixture);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetTasksForUserNarrowed_EmptyResponsibilityIds_StillMatchesUserAssignments()
    {
        var connectionString = GetTestConnectionString();
        var previousConnectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING");
        Environment.SetEnvironmentVariable("CONNECTION_STRING", connectionString);

        var fixture = await CreateNarrowingFixtureAsync(connectionString);
        try
        {
            var repository = new PostgresWorkflowRepository();

            var narrowed = await repository.GetTasksForUserNarrowed(fixture.AssigneeUserId, Array.Empty<int>());

            // With empty responsibility list, only direct-user assignment matches.
            Assert.Contains(narrowed, t => t.Task.Id == fixture.UserAssignedTaskId);
            Assert.DoesNotContain(narrowed, t => t.Task.Id == fixture.MatchingResponsibilityTaskId);
            Assert.DoesNotContain(narrowed, t => t.Task.Id == fixture.ForeignResponsibilityTaskId);
            Assert.DoesNotContain(narrowed, t => t.Task.Id == fixture.TerminalWorkflowTaskId);
        }
        finally
        {
            Environment.SetEnvironmentVariable("CONNECTION_STRING", previousConnectionString);
            await CleanupNarrowingFixtureAsync(connectionString, fixture);
        }
    }

    private static string GetTestConnectionString()
    {
        return Environment.GetEnvironmentVariable("ONBOARDING_TEST_CONNECTION_STRING")
               ?? DefaultTestConnectionString;
    }

    private static async Task<NarrowingFixture> CreateNarrowingFixtureAsync(string connectionString)
    {
        var suffix = Guid.NewGuid().ToString("N");
        var employeeNumber = Math.Abs(suffix[..8].GetHashCode());
        var badgeNumber = Math.Abs(suffix[8..16].GetHashCode());

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        int departmentId;
        await using (var cmd = new NpgsqlCommand(
                         "INSERT INTO departments (name) VALUES (@name) RETURNING id;",
                         connection,
                         transaction))
        {
            cmd.Parameters.AddWithValue("name", $"Narrowing {suffix}");
            departmentId = (int)(await cmd.ExecuteScalarAsync()
                ?? throw new InvalidOperationException("Department not created."));
        }

        int roleId;
        await using (var cmd = new NpgsqlCommand(
                         """
                         INSERT INTO app_roles (department_id, role_key, name, role_kind, is_active)
                         VALUES (@dept, @key, @name, 'position', TRUE)
                         RETURNING id;
                         """,
                         connection,
                         transaction))
        {
            cmd.Parameters.AddWithValue("dept", departmentId);
            cmd.Parameters.AddWithValue("key", $"narrowing_role_{suffix}");
            cmd.Parameters.AddWithValue("name", $"Narrowing Role {suffix}");
            roleId = (int)(await cmd.ExecuteScalarAsync()
                ?? throw new InvalidOperationException("Role not created."));
        }

        int matchingResponsibilityId;
        int foreignResponsibilityId;
        await using (var cmd = new NpgsqlCommand(
                         """
                         INSERT INTO app_responsibilities (department_id, responsibility_key, system_key, name, responsibility_type, description, is_active)
                         VALUES (@dept, @key, NULL, @name, 'department_lead', 'narrowing test', TRUE)
                         RETURNING id;
                         """,
                         connection,
                         transaction))
        {
            cmd.Parameters.AddWithValue("dept", departmentId);
            cmd.Parameters.AddWithValue("key", $"narrowing_match_{suffix}");
            cmd.Parameters.AddWithValue("name", $"Narrowing Match {suffix}");
            matchingResponsibilityId = (int)(await cmd.ExecuteScalarAsync()
                ?? throw new InvalidOperationException("Matching responsibility not created."));
        }

        await using (var cmd = new NpgsqlCommand(
                         """
                         INSERT INTO app_responsibilities (department_id, responsibility_key, system_key, name, responsibility_type, description, is_active)
                         VALUES (@dept, @key, NULL, @name, 'department_lead', 'narrowing test', TRUE)
                         RETURNING id;
                         """,
                         connection,
                         transaction))
        {
            cmd.Parameters.AddWithValue("dept", departmentId);
            cmd.Parameters.AddWithValue("key", $"narrowing_foreign_{suffix}");
            cmd.Parameters.AddWithValue("name", $"Narrowing Foreign {suffix}");
            foreignResponsibilityId = (int)(await cmd.ExecuteScalarAsync()
                ?? throw new InvalidOperationException("Foreign responsibility not created."));
        }

        long assigneeUserId;
        await using (var cmd = new NpgsqlCommand(
                         """
                         INSERT INTO app_users (department_id, display_name, email, is_active)
                         VALUES (@dept, @name, @email, TRUE)
                         RETURNING id;
                         """,
                         connection,
                         transaction))
        {
            cmd.Parameters.AddWithValue("dept", departmentId);
            cmd.Parameters.AddWithValue("name", $"Narrowing Assignee {suffix}");
            cmd.Parameters.AddWithValue("email", $"narrowing.{suffix}@kauth.local");
            assigneeUserId = (long)(await cmd.ExecuteScalarAsync()
                ?? throw new InvalidOperationException("User not created."));
        }

        var activeWorkflowId = await InsertWorkflowAsync(
            connection, transaction, departmentId, roleId, suffix + "_act", employeeNumber, badgeNumber, "waiting_for_department");
        var terminalWorkflowId = await InsertWorkflowAsync(
            connection, transaction, departmentId, roleId, suffix + "_done", employeeNumber + 1, badgeNumber + 1, "completed");

        var matchingResponsibilityTaskId = await InsertTaskAsync(connection, transaction, activeWorkflowId, $"match_resp_{suffix}", "ready", 10);
        var foreignResponsibilityTaskId = await InsertTaskAsync(connection, transaction, activeWorkflowId, $"foreign_resp_{suffix}", "ready", 20);
        var userAssignedTaskId = await InsertTaskAsync(connection, transaction, activeWorkflowId, $"user_assigned_{suffix}", "ready", 30);
        var terminalWorkflowTaskId = await InsertTaskAsync(connection, transaction, terminalWorkflowId, $"terminal_{suffix}", "done", 10);

        await InsertResponsibilityAssignmentAsync(connection, transaction, matchingResponsibilityTaskId, matchingResponsibilityId);
        await InsertResponsibilityAssignmentAsync(connection, transaction, foreignResponsibilityTaskId, foreignResponsibilityId);
        await InsertUserAssignmentAsync(connection, transaction, userAssignedTaskId, assigneeUserId);
        await InsertResponsibilityAssignmentAsync(connection, transaction, terminalWorkflowTaskId, matchingResponsibilityId);

        await transaction.CommitAsync();

        return new NarrowingFixture
        {
            DepartmentId = departmentId,
            RoleId = roleId,
            MatchingResponsibilityId = matchingResponsibilityId,
            ForeignResponsibilityId = foreignResponsibilityId,
            AssigneeUserId = assigneeUserId,
            ActiveWorkflowId = activeWorkflowId,
            TerminalWorkflowId = terminalWorkflowId,
            MatchingResponsibilityTaskId = matchingResponsibilityTaskId,
            ForeignResponsibilityTaskId = foreignResponsibilityTaskId,
            UserAssignedTaskId = userAssignedTaskId,
            TerminalWorkflowTaskId = terminalWorkflowTaskId
        };
    }

    private static async Task<long> InsertWorkflowAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int departmentId,
        int roleId,
        string lastName,
        int employeeNumber,
        int badgeNumber,
        string status)
    {
        await using var cmd = new NpgsqlCommand(
            """
            INSERT INTO workflows (
                workflow_definition_id,
                department_id,
                position_role_id,
                first_name,
                last_name,
                employee_number,
                badge_number,
                status,
                completed_at
            )
            VALUES (
                (SELECT id FROM workflow_definitions WHERE definition_key = 'onboarding'),
                @dept, @role, 'Narrowing', @last, @emp, @badge, @status,
                CASE WHEN @status = 'completed' THEN NOW() ELSE NULL END
            )
            RETURNING id;
            """,
            connection,
            transaction);
        cmd.Parameters.AddWithValue("dept", departmentId);
        cmd.Parameters.AddWithValue("role", roleId);
        cmd.Parameters.AddWithValue("last", lastName);
        cmd.Parameters.AddWithValue("emp", employeeNumber);
        cmd.Parameters.AddWithValue("badge", badgeNumber);
        cmd.Parameters.AddWithValue("status", status);
        return (long)(await cmd.ExecuteScalarAsync()
            ?? throw new InvalidOperationException("Workflow not created."));
    }

    private static async Task<long> InsertTaskAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long workflowId,
        string taskKey,
        string status,
        int sortOrder)
    {
        await using var cmd = new NpgsqlCommand(
            """
            INSERT INTO workflow_tasks (
                workflow_id, task_key, title, category, description, icon_key,
                status, is_required, sort_order, ready_at
            )
            VALUES (
                @wf, @key, @title, 'narrowing', 'narrowing test task', 'narrowing',
                @status, TRUE, @sort,
                CASE WHEN @status = 'ready' THEN NOW() ELSE NULL END
            )
            RETURNING id;
            """,
            connection,
            transaction);
        cmd.Parameters.AddWithValue("wf", workflowId);
        cmd.Parameters.AddWithValue("key", taskKey);
        cmd.Parameters.AddWithValue("title", $"Task {taskKey}");
        cmd.Parameters.AddWithValue("status", status);
        cmd.Parameters.AddWithValue("sort", sortOrder);
        return (long)(await cmd.ExecuteScalarAsync()
            ?? throw new InvalidOperationException("Task not created."));
    }

    private static async Task InsertResponsibilityAssignmentAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long taskId,
        int responsibilityId)
    {
        await using var cmd = new NpgsqlCommand(
            """
            INSERT INTO task_assignments (
                workflow_task_id, assignment_type, is_primary, assigned_at, assignee_responsibility_id
            )
            VALUES (@task, 'responsibility', TRUE, NOW(), @resp);
            """,
            connection,
            transaction);
        cmd.Parameters.AddWithValue("task", taskId);
        cmd.Parameters.AddWithValue("resp", responsibilityId);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task InsertUserAssignmentAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long taskId,
        long userId)
    {
        await using var cmd = new NpgsqlCommand(
            """
            INSERT INTO task_assignments (
                workflow_task_id, assignment_type, is_primary, assigned_at, assignee_user_id
            )
            VALUES (@task, 'user', TRUE, NOW(), @user);
            """,
            connection,
            transaction);
        cmd.Parameters.AddWithValue("task", taskId);
        cmd.Parameters.AddWithValue("user", userId);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task CleanupNarrowingFixtureAsync(string connectionString, NarrowingFixture fixture)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        await using (var cmd = new NpgsqlCommand(
                         "DELETE FROM workflows WHERE id = ANY(@ids);",
                         connection,
                         transaction))
        {
            cmd.Parameters.AddWithValue("ids", new[] { fixture.ActiveWorkflowId, fixture.TerminalWorkflowId });
            await cmd.ExecuteNonQueryAsync();
        }

        await using (var cmd = new NpgsqlCommand(
                         "DELETE FROM app_responsibilities WHERE id = ANY(@ids);",
                         connection,
                         transaction))
        {
            cmd.Parameters.AddWithValue("ids", new[] { fixture.MatchingResponsibilityId, fixture.ForeignResponsibilityId });
            await cmd.ExecuteNonQueryAsync();
        }

        await using (var cmd = new NpgsqlCommand(
                         "DELETE FROM app_users WHERE id = @id;",
                         connection,
                         transaction))
        {
            cmd.Parameters.AddWithValue("id", fixture.AssigneeUserId);
            await cmd.ExecuteNonQueryAsync();
        }

        await using (var cmd = new NpgsqlCommand(
                         "DELETE FROM app_roles WHERE id = @id;",
                         connection,
                         transaction))
        {
            cmd.Parameters.AddWithValue("id", fixture.RoleId);
            await cmd.ExecuteNonQueryAsync();
        }

        await using (var cmd = new NpgsqlCommand(
                         "DELETE FROM departments WHERE id = @id;",
                         connection,
                         transaction))
        {
            cmd.Parameters.AddWithValue("id", fixture.DepartmentId);
            await cmd.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();
    }

    private sealed class NarrowingFixture
    {
        public required int DepartmentId { get; init; }
        public required int RoleId { get; init; }
        public required int MatchingResponsibilityId { get; init; }
        public required int ForeignResponsibilityId { get; init; }
        public required long AssigneeUserId { get; init; }
        public required long ActiveWorkflowId { get; init; }
        public required long TerminalWorkflowId { get; init; }
        public required long MatchingResponsibilityTaskId { get; init; }
        public required long ForeignResponsibilityTaskId { get; init; }
        public required long UserAssignedTaskId { get; init; }
        public required long TerminalWorkflowTaskId { get; init; }
    }
}
