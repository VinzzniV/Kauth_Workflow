using Npgsql;
using Xunit;

namespace API.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PostgresWorkflowRepositoryIntegrationCollection
{
    public const string Name = "PostgresWorkflowRepositoryIntegration";
}

[Collection(PostgresWorkflowRepositoryIntegrationCollection.Name)]
public sealed class PostgresWorkflowRepositoryConcurrencyTests
{
    private const string DefaultTestConnectionString = "Host=localhost;Port=23456;Database=appdb;Username=app;Password=app_pw";
    private const long TestActorUserId = 1;

    [Fact]
    [Trait("Category", "Integration")]
    public async Task UpdateTaskStatus_LocksWorkflowBeforeTaskRow()
    {
        var connectionString = GetTestConnectionString();
        var testData = await CreateIsolatedWorkflowAsync(
            connectionString,
            ("concurrency_probe_task", "ready", 10));

        var previousConnectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING");
        Environment.SetEnvironmentVariable("CONNECTION_STRING", connectionString);

        try
        {
            await using var workflowLockConnection = new NpgsqlConnection(connectionString);
            await workflowLockConnection.OpenAsync();
            await using var workflowLockTransaction = await workflowLockConnection.BeginTransactionAsync();

            await using (var workflowLockCommand = new NpgsqlCommand(
                             """
                             SELECT id
                             FROM workflows
                             WHERE id = @workflowId
                             FOR UPDATE;
                             """,
                             workflowLockConnection,
                             workflowLockTransaction))
            {
                workflowLockCommand.Parameters.AddWithValue("workflowId", testData.WorkflowId);
                await workflowLockCommand.ExecuteScalarAsync();
            }

            var repository = new PostgresWorkflowRepository();
            var updateTask = repository.UpdateTaskStatus(testData.TaskIds[0], "in_progress", TestActorUserId);

            await Task.Delay(TimeSpan.FromMilliseconds(300));

            Assert.False(updateTask.IsCompleted);
            Assert.True(
                await CanLockTaskRowNowAsync(connectionString, testData.TaskIds[0]),
                "The task row should still be lockable while the workflow row is held, proving workflow-first locking.");

            await workflowLockTransaction.CommitAsync();

            var updatedTask = await updateTask.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.NotNull(updatedTask);
            Assert.Equal("in_progress", updatedTask!.Task.Status);
            Assert.Equal("in_progress", updatedTask.Workflow.WorkflowStatus);
        }
        finally
        {
            Environment.SetEnvironmentVariable("CONNECTION_STRING", previousConnectionString);
            await CleanupIsolatedWorkflowAsync(connectionString, testData);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task UpdateTaskStatus_ParallelUpdatesInSameWorkflowCompleteWithoutDeadlock()
    {
        var connectionString = GetTestConnectionString();
        var testData = await CreateIsolatedWorkflowAsync(
            connectionString,
            ("parallel_task_a", "ready", 10),
            ("parallel_task_b", "ready", 20));

        var previousConnectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING");
        Environment.SetEnvironmentVariable("CONNECTION_STRING", connectionString);

        try
        {
            var repository = new PostgresWorkflowRepository();

            var firstUpdateTask = repository.UpdateTaskStatus(testData.TaskIds[0], "in_progress", TestActorUserId);
            var secondUpdateTask = repository.UpdateTaskStatus(testData.TaskIds[1], "in_progress", TestActorUserId);

            await Task.WhenAll(firstUpdateTask, secondUpdateTask).WaitAsync(TimeSpan.FromSeconds(10));

            var firstUpdatedTask = await repository.GetTaskById(testData.TaskIds[0]);
            var secondUpdatedTask = await repository.GetTaskById(testData.TaskIds[1]);

            Assert.NotNull(firstUpdatedTask);
            Assert.NotNull(secondUpdatedTask);
            Assert.Equal("in_progress", firstUpdatedTask!.Task.Status);
            Assert.Equal("in_progress", secondUpdatedTask!.Task.Status);
            Assert.Equal("in_progress", firstUpdatedTask.Workflow.WorkflowStatus);
            Assert.Equal("in_progress", secondUpdatedTask.Workflow.WorkflowStatus);
        }
        finally
        {
            Environment.SetEnvironmentVariable("CONNECTION_STRING", previousConnectionString);
            await CleanupIsolatedWorkflowAsync(connectionString, testData);
        }
    }

    private static string GetTestConnectionString()
    {
        return Environment.GetEnvironmentVariable("ONBOARDING_TEST_CONNECTION_STRING")
               ?? DefaultTestConnectionString;
    }

    private static async Task<TestWorkflowData> CreateIsolatedWorkflowAsync(
        string connectionString,
        params (string TaskKey, string Status, int SortOrder)[] tasks)
    {
        var suffix = Guid.NewGuid().ToString("N");
        var employeeNumber = Math.Abs(suffix[..8].GetHashCode());
        var badgeNumber = Math.Abs(suffix[8..16].GetHashCode());

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        int departmentId;
        await using (var departmentCommand = new NpgsqlCommand(
                         """
                         INSERT INTO departments (name)
                         VALUES (@name)
                         RETURNING id;
                         """,
                         connection,
                         transaction))
        {
            departmentCommand.Parameters.AddWithValue("name", $"Integration {suffix}");
            departmentId = (int)(await departmentCommand.ExecuteScalarAsync()
                ?? throw new InvalidOperationException("Department could not be created."));
        }

        int roleId;
        await using (var roleCommand = new NpgsqlCommand(
                         """
                         INSERT INTO app_roles (department_id, role_key, name, role_kind, is_active)
                         VALUES (@departmentId, @roleKey, @name, 'position', TRUE)
                         RETURNING id;
                         """,
                         connection,
                         transaction))
        {
            roleCommand.Parameters.AddWithValue("departmentId", departmentId);
            roleCommand.Parameters.AddWithValue("roleKey", $"integration_role_{suffix}");
            roleCommand.Parameters.AddWithValue("name", $"Integration Role {suffix}");
            roleId = (int)(await roleCommand.ExecuteScalarAsync()
                ?? throw new InvalidOperationException("Role could not be created."));
        }

        long workflowId;
        await using (var workflowCommand = new NpgsqlCommand(
                         """
                         INSERT INTO workflows (
                             department_id,
                             onboarding_role_id,
                             first_name,
                             last_name,
                             employee_number,
                             badge_number,
                             status
                         )
                         VALUES (
                             @departmentId,
                             @roleId,
                             'Integration',
                             @lastName,
                             @employeeNumber,
                             @badgeNumber,
                             'waiting_for_department'
                         )
                         RETURNING id;
                         """,
                         connection,
                         transaction))
        {
            workflowCommand.Parameters.AddWithValue("departmentId", departmentId);
            workflowCommand.Parameters.AddWithValue("roleId", roleId);
            workflowCommand.Parameters.AddWithValue("lastName", suffix);
            workflowCommand.Parameters.AddWithValue("employeeNumber", employeeNumber);
            workflowCommand.Parameters.AddWithValue("badgeNumber", badgeNumber);
            workflowId = (long)(await workflowCommand.ExecuteScalarAsync()
                ?? throw new InvalidOperationException("Workflow could not be created."));
        }

        var taskIds = new List<long>();
        foreach (var (taskKey, status, sortOrder) in tasks)
        {
            await using var taskCommand = new NpgsqlCommand(
                """
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
                RETURNING id;
                """,
                connection,
                transaction);

            taskCommand.Parameters.AddWithValue("workflowId", workflowId);
            taskCommand.Parameters.AddWithValue("taskKey", $"{taskKey}_{suffix}");
            taskCommand.Parameters.AddWithValue("title", $"Task {taskKey}");
            taskCommand.Parameters.AddWithValue("description", $"Integration task {taskKey}");
            taskCommand.Parameters.AddWithValue("status", status);
            taskCommand.Parameters.AddWithValue("sortOrder", sortOrder);

            taskIds.Add((long)(await taskCommand.ExecuteScalarAsync()
                ?? throw new InvalidOperationException("Workflow task could not be created.")));
        }

        await transaction.CommitAsync();

        return new TestWorkflowData
        {
            DepartmentId = departmentId,
            RoleId = roleId,
            WorkflowId = workflowId,
            TaskIds = taskIds
        };
    }

    private static async Task CleanupIsolatedWorkflowAsync(
        string connectionString,
        TestWorkflowData testData)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        await using (var deleteWorkflowCommand = new NpgsqlCommand(
                         "DELETE FROM workflows WHERE id = @workflowId;",
                         connection,
                         transaction))
        {
            deleteWorkflowCommand.Parameters.AddWithValue("workflowId", testData.WorkflowId);
            await deleteWorkflowCommand.ExecuteNonQueryAsync();
        }

        await using (var deleteRoleCommand = new NpgsqlCommand(
                         "DELETE FROM app_roles WHERE id = @roleId;",
                         connection,
                         transaction))
        {
            deleteRoleCommand.Parameters.AddWithValue("roleId", testData.RoleId);
            await deleteRoleCommand.ExecuteNonQueryAsync();
        }

        await using (var deleteDepartmentCommand = new NpgsqlCommand(
                         "DELETE FROM departments WHERE id = @departmentId;",
                         connection,
                         transaction))
        {
            deleteDepartmentCommand.Parameters.AddWithValue("departmentId", testData.DepartmentId);
            await deleteDepartmentCommand.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();
    }

    private static async Task<bool> CanLockTaskRowNowAsync(string connectionString, long taskId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            await using var command = new NpgsqlCommand(
                """
                SELECT id
                FROM workflow_tasks
                WHERE id = @taskId
                FOR UPDATE NOWAIT;
                """,
                connection,
                transaction);

            command.Parameters.AddWithValue("taskId", taskId);
            await command.ExecuteScalarAsync();
            await transaction.RollbackAsync();
            return true;
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.LockNotAvailable)
        {
            await transaction.RollbackAsync();
            return false;
        }
    }

    private sealed class TestWorkflowData
    {
        public required int DepartmentId { get; init; }
        public required int RoleId { get; init; }
        public required long WorkflowId { get; init; }
        public required List<long> TaskIds { get; init; }
    }
}
