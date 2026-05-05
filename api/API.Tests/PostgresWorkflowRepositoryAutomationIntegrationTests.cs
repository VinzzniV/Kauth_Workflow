using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Xunit;

namespace API.Tests;

[Collection(PostgresWorkflowRepositoryIntegrationCollection.Name)]
public sealed class PostgresWorkflowRepositoryAutomationIntegrationTests
{
    private const string DefaultTestConnectionString = "Host=localhost;Port=26432;Database=appdb;Username=app;Password=app_pw";

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
        WorkflowTargetPersonDto? targetPerson = null;

        try
        {
            var repository = new PostgresWorkflowRepository();
            var runtimeRepository = new PostgresWorkflowRuntimeRepository();
            var lifecycleService = new WorkflowLifecycleService(repository, repository, new PostgresWorkflowAuditWriteOperations(), new PostgresWorkflowStatusCalculationService(), new PostgresWorkflowNotificationDispatchOperations());
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

            var published = await runtimeRepository.PublishWorkflowDefinitionVersion(version.Id);
            Assert.NotNull(published);
            Assert.True(published!.CanPublish);
            targetPerson = await CreateTargetPersonAsync(repository, createContext, "Ada", "Lovelace", 123456, 654321);

            runtime = await lifecycleService.CreateWorkflowInstanceAsync(
                new CreateWorkflowDefinitionInstanceRequest
                {
                    WorkflowDefinitionKey = definition.Key,
                    DepartmentId = createContext.DepartmentId,
                    RoleId = createContext.RoleId,
                    TargetPersonId = targetPerson.PersonId,
                    FirstName = "Ada",
                    LastName = "Lovelace",
                    EmployeeNumber = 123456,
                    BadgeNumber = 654321
                },
                createContext.ActorUserId);

            var jobsAfterStart = await new PostgresWorkflowAutomationReadRepository().GetAutomationJobs(runtime.WorkflowUid);
            Assert.Single(jobsAfterStart);
            Assert.Equal("pending", jobsAfterStart[0].Status);
            Assert.Equal(10, jobsAfterStart[0].ExecutionOrder);

            Assert.True(await automationService.TryProcessNextPendingJobAsync());

            var jobsAfterFirstRun = await new PostgresWorkflowAutomationReadRepository().GetAutomationJobs(runtime.WorkflowUid);
            Assert.Equal(2, jobsAfterFirstRun.Count);
            Assert.Contains(jobsAfterFirstRun, job => job.ExecutionOrder == 10 && job.Status == "succeeded");
            Assert.Contains(jobsAfterFirstRun, job => job.ExecutionOrder == 20 && job.Status == "pending");

            Assert.True(await automationService.TryProcessNextPendingJobAsync());

            var jobsAfterSecondRun = await new PostgresWorkflowAutomationReadRepository().GetAutomationJobs(runtime.WorkflowUid);
            Assert.Equal(2, jobsAfterSecondRun.Count);
            Assert.All(jobsAfterSecondRun, job => Assert.Equal("succeeded", job.Status));

            var reloadedRuntime = await runtimeRepository.GetWorkflowDefinitionRuntimeDetail(runtime.WorkflowUid);
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

            if (targetPerson is not null)
            {
                await CleanupPersonAsync(connectionString, targetPerson.PersonId);
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
        WorkflowTargetPersonDto? targetPerson = null;

        try
        {
            var repository = new PostgresWorkflowRepository();
            var runtimeRepository = new PostgresWorkflowRuntimeRepository();
            var lifecycleService = new WorkflowLifecycleService(repository, repository, new PostgresWorkflowAuditWriteOperations(), new PostgresWorkflowStatusCalculationService(), new PostgresWorkflowNotificationDispatchOperations());
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
            Assert.NotNull(await runtimeRepository.PublishWorkflowDefinitionVersion(version.Id));
            targetPerson = await CreateTargetPersonAsync(repository, createContext, "Ada", "Lovelace", 223456, 754321);

            runtime = await lifecycleService.CreateWorkflowInstanceAsync(
                new CreateWorkflowDefinitionInstanceRequest
                {
                    WorkflowDefinitionKey = definition.Key,
                    DepartmentId = createContext.DepartmentId,
                    RoleId = createContext.RoleId,
                    TargetPersonId = targetPerson.PersonId,
                    FirstName = "Ada",
                    LastName = "Lovelace",
                    EmployeeNumber = 223456,
                    BadgeNumber = 754321
                },
                createContext.ActorUserId);

            Assert.True(await automationService.TryProcessNextPendingJobAsync());

            var jobs = await new PostgresWorkflowAutomationReadRepository().GetAutomationJobs(runtime.WorkflowUid);
            var job = Assert.Single(jobs);
            Assert.Equal("failed", job.Status);
            Assert.Single(job.Attempts);
            Assert.Equal("failed", job.Attempts[0].Status);

            var reloadedRuntime = await runtimeRepository.GetWorkflowDefinitionRuntimeDetail(runtime.WorkflowUid);
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

            if (targetPerson is not null)
            {
                await CleanupPersonAsync(connectionString, targetPerson.PersonId);
            }

            if (definition is not null)
            {
                await CleanupWorkflowDefinitionAsync(connectionString, definition.Id);
            }
        }
    }

    private static Task<WorkflowTargetPersonDto> CreateTargetPersonAsync(
        PostgresWorkflowRepository repository,
        (long ActorUserId, int DepartmentId, int RoleId) createContext,
        string firstName,
        string lastName,
        int employeeNumber,
        int badgeNumber)
    {
        return repository.CreatePerson(
            new CreatePersonRequest
            {
                FirstName = firstName,
                LastName = lastName,
                EmployeeNumber = employeeNumber,
                BadgeNumber = badgeNumber,
                DepartmentId = createContext.DepartmentId,
                RoleId = createContext.RoleId
            },
            createContext.ActorUserId);
    }

    private static WorkflowAutomationService CreateAutomationService(PostgresWorkflowRepository repository)
    {
        var lifecycleService = new WorkflowLifecycleService(
            repository,
            repository,
            new PostgresWorkflowAuditWriteOperations(),
            new PostgresWorkflowStatusCalculationService(),
            new PostgresWorkflowNotificationDispatchOperations());
        return new WorkflowAutomationService(
            repository,
            new PostgresWorkflowAutomationReadRepository(),
            new WorkflowAutomationHandlerRegistry(
            [
                new CreateAdUserAutomationHandler(),
                new CreateMailboxAutomationHandler(),
                new AssignGroupsAutomationHandler(),
                new CreateErpEmployeeAutomationHandler(),
                new SendWelcomeMailAutomationHandler()
            ]),
            new StubSystemEventLogService(),
            lifecycleService,
            new WorkflowAutomationRetrySettings(),
            NullLogger<WorkflowAutomationService>.Instance);
    }

    private sealed class StubSystemEventLogService : ISystemEventLogService
    {
        public Task<IReadOnlyList<AdminSystemLogEntryDto>> GetAdminLogsAsync(
            SystemEventLogQuery query,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<AdminSystemLogEntryDto>>([]);

        public Task<AdminSystemLogSummaryDto> GetAdminLogSummaryAsync(
            SystemEventLogQuery query,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new AdminSystemLogSummaryDto
            {
                TotalCount = 0,
                InfoCount = 0,
                WarningCount = 0,
                ErrorCount = 0,
                Sources = []
            });

        public Task WriteAsync(SystemEventLogWriteModel model, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
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

            await using var command = new NpgsqlCommand(
                """
                SELECT to_regclass('public.workflow_definitions') IS NOT NULL
                   AND to_regclass('public.workflow_definition_versions') IS NOT NULL
                   AND to_regclass('public.action_definitions') IS NOT NULL
                   AND to_regclass('public.workflow_node_actions') IS NOT NULL
                   AND to_regclass('public.automation_jobs') IS NOT NULL;
                """,
                connection);
            return (bool?)await command.ExecuteScalarAsync() == true;
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
                         WHERE r.role_kind = 'position'
                           AND r.is_active = TRUE
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

    private static async Task CleanupPersonAsync(string connectionString, long personId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            DELETE FROM people
            WHERE id = @personId;
            """,
            connection);
        command.Parameters.AddWithValue("personId", personId);
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
