using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Xunit;

namespace API.Tests;

[Collection(PostgresWorkflowRepositoryIntegrationCollection.Name)]
public sealed class PostgresWorkflowRepositoryAutomationIntegrationTests
{
    private const string DefaultTestConnectionString = "Host=localhost;Port=25432;Database=appdb;Username=app;Password=app_pw";

    [Fact]
    [Trait("Category", "Integration")]
    public async Task AutomationNode_ProcessesSequentialActions_AndCompletesWorkflow()
    {
        var connectionString = GetTestConnectionString();
        if (!await EnsureAutomationLayerAsync(connectionString))
        {
            return;
        }

        var previousConnectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING");
        Environment.SetEnvironmentVariable("CONNECTION_STRING", connectionString);

        WorkflowDefinitionSummaryDto? definition = null;
        WorkflowDefinitionRuntimeDetailDto? runtime = null;

        try
        {
            var repository = new PostgresWorkflowRepository();
            var automationService = CreateAutomationService(repository);
            var createContext = await LoadOnboardingCreateContextAsync(connectionString);

            definition = await repository.CreateAdminWorkflowDefinition(new CreateWorkflowDefinitionRequest
            {
                Key = $"automation_success_{Guid.NewGuid():N}",
                Name = "Automation Success",
                Description = "Sequential automation integration test"
            });

            var version = await repository.CreateAdminWorkflowDefinitionVersion(
                definition.Id,
                new CreateWorkflowDefinitionVersionRequest
                {
                    Name = "Draft",
                    Description = "Sequential automation draft"
                });

            Assert.NotNull(version);

            var updatedVersion = await repository.ReplaceAdminWorkflowDefinitionVersion(
                version!.Id,
                new ReplaceWorkflowDefinitionVersionRequest
                {
                    Name = "Draft",
                    Description = "Sequential automation draft",
                    PrimaryLegacyProcessTypeKey = "onboarding",
                    Nodes =
                    [
                        CreateNode("start", "start"),
                        CreateNode(
                            "auto",
                            "automation",
                            actions:
                            [
                                CreateAction("CreateAdUser", 10, """{"employeeNumber":{"source":"workflow","property":"employeeNumber"}}"""),
                                CreateAction("AssignGroups", 20, """{"groups":{"source":"static","value":["grp-engineering"]}}""")
                            ]),
                        CreateNode("end", "end")
                    ],
                    Edges =
                    [
                        CreateEdge("start", "auto", 0),
                        CreateEdge("auto", "end", 0)
                    ]
                });

            Assert.NotNull(updatedVersion);

            var published = await repository.PublishWorkflowDefinitionVersion(version.Id);
            Assert.NotNull(published);
            Assert.True(published!.CanPublish);

            runtime = await repository.CreateWorkflowDefinitionInstance(
                new CreateWorkflowDefinitionInstanceRequest
                {
                    WorkflowDefinitionKey = definition.Key,
                    DepartmentId = createContext.DepartmentId,
                    RoleId = createContext.RoleId,
                    FirstName = "Ada",
                    LastName = "Lovelace",
                    EmployeeNumber = 123456,
                    BadgeNumber = 654321
                },
                createContext.ActorUserId);

            var jobsAfterStart = await repository.GetAutomationJobs(runtime.WorkflowUid);
            Assert.Single(jobsAfterStart);
            Assert.Equal("pending", jobsAfterStart[0].Status);
            Assert.Equal(10, jobsAfterStart[0].ExecutionOrder);

            Assert.True(await automationService.TryProcessNextPendingJobAsync());

            var jobsAfterFirstRun = await repository.GetAutomationJobs(runtime.WorkflowUid);
            Assert.Equal(2, jobsAfterFirstRun.Count);
            Assert.Contains(jobsAfterFirstRun, job => job.ExecutionOrder == 10 && job.Status == "succeeded");
            Assert.Contains(jobsAfterFirstRun, job => job.ExecutionOrder == 20 && job.Status == "pending");

            Assert.True(await automationService.TryProcessNextPendingJobAsync());

            var jobsAfterSecondRun = await repository.GetAutomationJobs(runtime.WorkflowUid);
            Assert.Equal(2, jobsAfterSecondRun.Count);
            Assert.All(jobsAfterSecondRun, job => Assert.Equal("succeeded", job.Status));

            var reloadedRuntime = await repository.GetWorkflowDefinitionRuntimeDetail(runtime.WorkflowUid);
            Assert.NotNull(reloadedRuntime);
            Assert.Equal("completed", reloadedRuntime!.CurrentRuntimeStatus);
            Assert.Contains(reloadedRuntime.NodeInstances, node => node.NodeKey == "auto" && node.Status == "done");
            Assert.Contains(reloadedRuntime.NodeInstances, node => node.NodeKey == "end" && node.Status == "done");
        }
        finally
        {
            Environment.SetEnvironmentVariable("CONNECTION_STRING", previousConnectionString);

            if (runtime is not null)
            {
                await CleanupWorkflowAsync(connectionString, runtime.WorkflowId);
            }

            if (definition is not null)
            {
                await CleanupWorkflowDefinitionAsync(connectionString, definition.Id);
            }
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task AutomationNode_FailingNonIdempotentAction_FailsWorkflow()
    {
        var connectionString = GetTestConnectionString();
        if (!await EnsureAutomationLayerAsync(connectionString))
        {
            return;
        }

        var previousConnectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING");
        Environment.SetEnvironmentVariable("CONNECTION_STRING", connectionString);

        WorkflowDefinitionSummaryDto? definition = null;
        WorkflowDefinitionRuntimeDetailDto? runtime = null;

        try
        {
            var repository = new PostgresWorkflowRepository();
            var automationService = CreateAutomationService(repository);
            var createContext = await LoadOnboardingCreateContextAsync(connectionString);

            definition = await repository.CreateAdminWorkflowDefinition(new CreateWorkflowDefinitionRequest
            {
                Key = $"automation_failure_{Guid.NewGuid():N}",
                Name = "Automation Failure",
                Description = "Final automation failure integration test"
            });

            var version = await repository.CreateAdminWorkflowDefinitionVersion(
                definition.Id,
                new CreateWorkflowDefinitionVersionRequest
                {
                    Name = "Draft",
                    Description = "Failure automation draft"
                });

            Assert.NotNull(version);

            var updatedVersion = await repository.ReplaceAdminWorkflowDefinitionVersion(
                version!.Id,
                new ReplaceWorkflowDefinitionVersionRequest
                {
                    Name = "Draft",
                    Description = "Failure automation draft",
                    PrimaryLegacyProcessTypeKey = "onboarding",
                    Nodes =
                    [
                        CreateNode("start", "start"),
                        CreateNode(
                            "auto",
                            "automation",
                            actions:
                            [
                                CreateAction("CreateErpEmployee", 10, """{"simulateFailure":{"source":"static","value":true}}""")
                            ]),
                        CreateNode("end", "end")
                    ],
                    Edges =
                    [
                        CreateEdge("start", "auto", 0),
                        CreateEdge("auto", "end", 0)
                    ]
                });

            Assert.NotNull(updatedVersion);
            Assert.NotNull(await repository.PublishWorkflowDefinitionVersion(version.Id));

            runtime = await repository.CreateWorkflowDefinitionInstance(
                new CreateWorkflowDefinitionInstanceRequest
                {
                    WorkflowDefinitionKey = definition.Key,
                    DepartmentId = createContext.DepartmentId,
                    RoleId = createContext.RoleId,
                    FirstName = "Ada",
                    LastName = "Lovelace",
                    EmployeeNumber = 223456,
                    BadgeNumber = 754321
                },
                createContext.ActorUserId);

            Assert.True(await automationService.TryProcessNextPendingJobAsync());

            var jobs = await repository.GetAutomationJobs(runtime.WorkflowUid);
            var job = Assert.Single(jobs);
            Assert.Equal("failed", job.Status);
            Assert.Single(job.Attempts);
            Assert.Equal("failed", job.Attempts[0].Status);

            var reloadedRuntime = await repository.GetWorkflowDefinitionRuntimeDetail(runtime.WorkflowUid);
            Assert.NotNull(reloadedRuntime);
            Assert.Equal("failed", reloadedRuntime!.CurrentRuntimeStatus);
            Assert.Contains(reloadedRuntime.NodeInstances, node => node.NodeKey == "auto" && node.Status == "failed");
        }
        finally
        {
            Environment.SetEnvironmentVariable("CONNECTION_STRING", previousConnectionString);

            if (runtime is not null)
            {
                await CleanupWorkflowAsync(connectionString, runtime.WorkflowId);
            }

            if (definition is not null)
            {
                await CleanupWorkflowDefinitionAsync(connectionString, definition.Id);
            }
        }
    }

    private static WorkflowAutomationService CreateAutomationService(PostgresWorkflowRepository repository)
    {
        return new WorkflowAutomationService(
            repository,
            new WorkflowAutomationHandlerRegistry(
            [
                new CreateAdUserAutomationHandler(),
                new CreateMailboxAutomationHandler(),
                new AssignGroupsAutomationHandler(),
                new CreateErpEmployeeAutomationHandler(),
                new SendWelcomeMailAutomationHandler()
            ]),
            NullLogger<WorkflowAutomationService>.Instance);
    }

    private static string GetTestConnectionString()
    {
        return Environment.GetEnvironmentVariable("ONBOARDING_TEST_CONNECTION_STRING")
               ?? DefaultTestConnectionString;
    }

    private static async Task<bool> EnsureAutomationLayerAsync(string connectionString)
    {
        try
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();

            foreach (var migration in new[] { "db/41_workflow_definition_layer.sql", "db/42_workflow_runtime_layer.sql", "db/45_automation_layer.sql" })
            {
                var migrationSql = await File.ReadAllTextAsync(FindRepositoryFile(migration.Replace('/', Path.DirectorySeparatorChar)));
                await using var command = new NpgsqlCommand(migrationSql, connection);
                await command.ExecuteNonQueryAsync();
            }

            return true;
        }
        catch (NpgsqlException ex)
        {
            _ = ex;
            return false;
        }
    }

    private static async Task<(long ActorUserId, int DepartmentId, int RoleId)> LoadOnboardingCreateContextAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        int departmentId;
        int roleId;
        await using (var command = new NpgsqlCommand(
                         """
                         SELECT d.id, r.id
                         FROM app_roles r
                         JOIN departments d ON d.id = r.department_id
                         JOIN process_types pt ON pt.key = 'onboarding'
                         WHERE r.role_kind = 'position'
                           AND r.is_active = TRUE
                           AND pt.is_active = TRUE
                         ORDER BY r.id
                         LIMIT 1;
                         """,
                         connection,
                         transaction))
        await using (var reader = await command.ExecuteReaderAsync())
        {
            if (!await reader.ReadAsync())
            {
                throw new InvalidOperationException("No active seeded position role was found for automation runtime tests.");
            }

            departmentId = reader.GetInt32(0);
            roleId = reader.GetInt32(1);
        }

        long actorUserId;
        var suffix = Guid.NewGuid().ToString("N")[..12];
        await using (var command = new NpgsqlCommand(
                         """
                         INSERT INTO app_users (department_id, display_name, email, is_active)
                         VALUES (@departmentId, @displayName, @email, TRUE)
                         RETURNING id;
                         """,
                         connection,
                         transaction))
        {
            command.Parameters.AddWithValue("departmentId", departmentId);
            command.Parameters.AddWithValue("displayName", $"Automation Actor {suffix}");
            command.Parameters.AddWithValue("email", $"automation.actor.{suffix}@example.test");
            actorUserId = (long)(await command.ExecuteScalarAsync() ?? throw new InvalidOperationException("Actor user could not be created."));
        }

        await transaction.CommitAsync();
        return (actorUserId, departmentId, roleId);
    }

    private static async Task CleanupWorkflowDefinitionAsync(string connectionString, int definitionId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            DELETE FROM workflow_definitions
            WHERE id = @definitionId;
            """,
            connection);
        command.Parameters.AddWithValue("definitionId", definitionId);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task CleanupWorkflowAsync(string connectionString, long workflowId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            DELETE FROM workflows
            WHERE id = @workflowId;
            """,
            connection);
        command.Parameters.AddWithValue("workflowId", workflowId);
        await command.ExecuteNonQueryAsync();
    }

    private static string FindRepositoryFile(params string[] segments)
    {
        var currentDirectory = new DirectoryInfo(AppContext.BaseDirectory);
        while (currentDirectory is not null)
        {
            var candidate = Path.Combine([currentDirectory.FullName, .. segments]);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            currentDirectory = currentDirectory.Parent;
        }

        throw new InvalidOperationException($"Repository file '{Path.Combine(segments)}' could not be found.");
    }

    private static WorkflowDefinitionNodeDto CreateNode(
        string nodeKey,
        string nodeType,
        string? configJson = null,
        params WorkflowNodeActionDto[] actions)
    {
        return new WorkflowDefinitionNodeDto
        {
            NodeKey = nodeKey,
            NodeType = nodeType,
            SortOrder = 0,
            Config = configJson is null ? null : System.Text.Json.JsonDocument.Parse(configJson).RootElement.Clone(),
            Actions = actions.ToList()
        };
    }

    private static WorkflowNodeActionDto CreateAction(
        string actionKey,
        int executionOrder,
        string? inputMappingJson = null)
    {
        return new WorkflowNodeActionDto
        {
            ActionKey = actionKey,
            ExecutionOrder = executionOrder,
            OnErrorBehavior = "fail_workflow",
            InputMapping = inputMappingJson is null ? null : System.Text.Json.JsonDocument.Parse(inputMappingJson).RootElement.Clone()
        };
    }

    private static WorkflowDefinitionEdgeDto CreateEdge(
        string sourceNodeKey,
        string targetNodeKey,
        int priority)
    {
        return new WorkflowDefinitionEdgeDto
        {
            SourceNodeKey = sourceNodeKey,
            TargetNodeKey = targetNodeKey,
            Priority = priority
        };
    }
}
