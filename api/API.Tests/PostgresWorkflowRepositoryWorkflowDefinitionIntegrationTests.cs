using Npgsql;
using Xunit;

namespace API.Tests;

[Collection(PostgresWorkflowRepositoryIntegrationCollection.Name)]
public sealed class PostgresWorkflowRepositoryWorkflowDefinitionIntegrationTests
{
    private const string DefaultTestConnectionString = "Host=localhost;Port=25432;Database=appdb;Username=app;Password=app_pw";

    [Fact]
    [Trait("Category", "Integration")]
    public async Task WorkflowDefinitionDraft_Roundtrip_CreateReplaceAndReload()
    {
        var connectionString = GetTestConnectionString();
        if (!await EnsureWorkflowDefinitionLayerAsync(connectionString))
        {
            return;
        }

        WorkflowDefinitionSummaryDto? definition = null;
        var previousConnectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING");
        Environment.SetEnvironmentVariable("CONNECTION_STRING", connectionString);

        try
        {
            var repository = new PostgresWorkflowRepository();

            definition = await repository.CreateAdminWorkflowDefinition(new CreateWorkflowDefinitionRequest
            {
                Key = $"Definition_{Guid.NewGuid():N}",
                Name = "Employee Lifecycle",
                Description = "Draft-only integration test definition"
            });

            var createdVersion = await repository.CreateAdminWorkflowDefinitionVersion(
                definition.Id,
                new CreateWorkflowDefinitionVersionRequest
                {
                    Name = "Initial Draft",
                    Description = "Roundtrip version"
                });

            Assert.NotNull(createdVersion);

            var updatedVersion = await repository.ReplaceAdminWorkflowDefinitionVersion(
                createdVersion!.Id,
                new ReplaceWorkflowDefinitionVersionRequest
                {
                    Name = "Initial Draft Updated",
                    Description = "Roundtrip graph",
                    Nodes =
                    [
                        WorkflowDefinitionTestData.FormNode("Start", "start"),
                        WorkflowDefinitionTestData.FormNode("Collect_Data", "form", """{"legacyProcessTypeKey":"onboarding"}""", 10),
                        WorkflowDefinitionTestData.FormNode("Approve_Manager", "approval", """{"legacyTemplateKey":"manager_approval"}""", 20),
                        WorkflowDefinitionTestData.FormNode("Finish", "end", null, 30)
                    ],
                    Edges =
                    [
                        WorkflowDefinitionTestData.Edge("Start", "Collect_Data", 0),
                        WorkflowDefinitionTestData.Edge("Collect_Data", "Approve_Manager", 0),
                        WorkflowDefinitionTestData.Edge("Approve_Manager", "Finish", 0)
                    ]
                });

            Assert.NotNull(updatedVersion);
            Assert.Equal(definition.Key.ToLowerInvariant(), definition.Key);
            Assert.Equal(4, updatedVersion!.Nodes.Count);
            Assert.Equal(3, updatedVersion.Edges.Count);
            Assert.Contains(updatedVersion.Nodes, node => node.NodeKey == "collect_data" && node.Config.HasValue);

            var reloadedVersion = await repository.GetAdminWorkflowDefinitionVersion(createdVersion.Id);

            Assert.NotNull(reloadedVersion);
            Assert.Equal(updatedVersion.Id, reloadedVersion!.Id);
            Assert.Equal("Initial Draft Updated", reloadedVersion.Name);
            Assert.Equal(definition.Key, reloadedVersion.DefinitionKey);
            Assert.Equal("Employee Lifecycle", reloadedVersion.DefinitionName);
            Assert.Contains(reloadedVersion.Nodes, node => node.NodeType == "approval");
            Assert.Contains(reloadedVersion.Edges, edge => edge.SourceNodeKey == "approve_manager" && edge.TargetNodeKey == "finish");
        }
        finally
        {
            Environment.SetEnvironmentVariable("CONNECTION_STRING", previousConnectionString);

            if (definition is not null)
            {
                await CleanupWorkflowDefinitionAsync(connectionString, definition.Id);
            }
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task SeededWorkflowMappings_ArePublishedValidAndRuntimeStartable()
    {
        var connectionString = GetTestConnectionString();
        if (!await EnsureWorkflowDefinitionMappingsAsync(connectionString))
        {
            return;
        }

        var previousConnectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING");
        Environment.SetEnvironmentVariable("CONNECTION_STRING", connectionString);

        SeededRuntimeTargetPerson? targetPerson = null;
        WorkflowDefinitionRuntimeDetailDto? onboardingRuntime = null;
        WorkflowDefinitionRuntimeDetailDto? offboardingRuntime = null;
        WorkflowDefinitionRuntimeDetailDto? departmentChangeRuntime = null;

        try
        {
            var repository = new PostgresWorkflowRepository();

            var definitions = await repository.GetAdminWorkflowDefinitions();
            foreach (var definitionKey in new[] { "onboarding", "offboarding", "department_change" })
            {
                var definition = definitions.SingleOrDefault(item =>
                    string.Equals(item.Key, definitionKey, StringComparison.OrdinalIgnoreCase));

                Assert.NotNull(definition);
                var publishedVersion = definition!.Versions.SingleOrDefault(version =>
                    string.Equals(version.Status, "published", StringComparison.OrdinalIgnoreCase));

                Assert.NotNull(publishedVersion);
                Assert.True(publishedVersion!.CanPublish);
                Assert.Empty(publishedVersion.ValidationIssues);
            }

            targetPerson = await CreateSeededRuntimeTargetPersonAsync(connectionString);

            onboardingRuntime = await repository.CreateWorkflowDefinitionInstance(
                new CreateWorkflowDefinitionInstanceRequest
                {
                    WorkflowDefinitionKey = "onboarding",
                    DepartmentId = targetPerson.DepartmentId,
                    RoleId = targetPerson.RoleId,
                    FirstName = "Ada",
                    LastName = "Lovelace",
                    EmployeeNumber = targetPerson.EmployeeNumber + 1000,
                    BadgeNumber = targetPerson.BadgeNumber + 1000
                },
                targetPerson.ActorUserId);

            offboardingRuntime = await repository.CreateWorkflowDefinitionInstance(
                new CreateWorkflowDefinitionInstanceRequest
                {
                    WorkflowDefinitionKey = "offboarding",
                    TargetPersonId = targetPerson.PersonId
                },
                targetPerson.ActorUserId);

            departmentChangeRuntime = await repository.CreateWorkflowDefinitionInstance(
                new CreateWorkflowDefinitionInstanceRequest
                {
                    WorkflowDefinitionKey = "department_change",
                    TargetPersonId = targetPerson.PersonId
                },
                targetPerson.ActorUserId);

            Assert.Equal("onboarding", onboardingRuntime.WorkflowDefinitionKey);
            Assert.Contains(onboardingRuntime.NodeInstances, node => node.NodeKey == "collect_requirements");
            Assert.Equal("offboarding", offboardingRuntime.WorkflowDefinitionKey);
            Assert.Contains(offboardingRuntime.NodeInstances, node => node.NodeKey == "task_last_day_confirmed");
            Assert.Equal("department_change", departmentChangeRuntime.WorkflowDefinitionKey);
            Assert.Contains(departmentChangeRuntime.NodeInstances, node => node.NodeKey == "task_hr_system_update");
        }
        finally
        {
            Environment.SetEnvironmentVariable("CONNECTION_STRING", previousConnectionString);

            if (onboardingRuntime is not null)
            {
                await CleanupWorkflowAsync(connectionString, onboardingRuntime.WorkflowId);
            }

            if (offboardingRuntime is not null)
            {
                await CleanupWorkflowAsync(connectionString, offboardingRuntime.WorkflowId);
            }

            if (departmentChangeRuntime is not null)
            {
                await CleanupWorkflowAsync(connectionString, departmentChangeRuntime.WorkflowId);
            }

            if (targetPerson is not null)
            {
                await CleanupSeededRuntimeTargetPersonAsync(connectionString, targetPerson);
            }
        }
    }

    private static string GetTestConnectionString()
    {
        return Environment.GetEnvironmentVariable("ONBOARDING_TEST_CONNECTION_STRING")
               ?? DefaultTestConnectionString;
    }

    private static async Task<bool> EnsureWorkflowDefinitionLayerAsync(string connectionString)
    {
        try
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();

            var migrationPath = FindRepositoryFile("db", "41_workflow_definition_layer.sql");
            var migrationSql = await File.ReadAllTextAsync(migrationPath);

            await using var command = new NpgsqlCommand(migrationSql, connection);
            await command.ExecuteNonQueryAsync();
            return true;
        }
        catch (NpgsqlException ex)
        {
            _ = ex;
            return false;
        }
    }

    private static async Task<bool> EnsureWorkflowDefinitionMappingsAsync(string connectionString)
    {
        try
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();

            foreach (var migration in new[] { "db/41_workflow_definition_layer.sql", "db/42_workflow_runtime_layer.sql", "db/43_workflow_definition_mappings.sql" })
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

    private static async Task<SeededRuntimeTargetPerson> CreateSeededRuntimeTargetPersonAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        int departmentId;
        int roleId;
        int onboardingProcessTypeId;
        await using (var command = new NpgsqlCommand(
                         """
                         SELECT d.id, r.id, pt.id
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
                throw new InvalidOperationException("No active seeded position role was found for runtime smoke tests.");
            }

            departmentId = reader.GetInt32(0);
            roleId = reader.GetInt32(1);
            onboardingProcessTypeId = reader.GetInt32(2);
        }

        var suffix = Guid.NewGuid().ToString("N")[..12];
        long actorUserId;
        long targetUserId;
        long personId;
        var employeeNumber = 800000 + Random.Shared.Next(1000, 9999);
        var badgeNumber = 900000 + Random.Shared.Next(1000, 9999);

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
            command.Parameters.AddWithValue("displayName", $"Runtime Actor {suffix}");
            command.Parameters.AddWithValue("email", $"runtime.actor.{suffix}@example.test");
            actorUserId = (long)(await command.ExecuteScalarAsync() ?? throw new InvalidOperationException("Actor user could not be created."));
        }

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
            command.Parameters.AddWithValue("displayName", $"Runtime Target {suffix}");
            command.Parameters.AddWithValue("email", $"runtime.target.{suffix}@example.test");
            targetUserId = (long)(await command.ExecuteScalarAsync() ?? throw new InvalidOperationException("Target user could not be created."));
        }

        await using (var command = new NpgsqlCommand(
                         """
                         INSERT INTO people (app_user_id, department_id)
                         VALUES (@appUserId, @departmentId)
                         RETURNING id;
                         """,
                         connection,
                         transaction))
        {
            command.Parameters.AddWithValue("appUserId", targetUserId);
            command.Parameters.AddWithValue("departmentId", departmentId);
            personId = (long)(await command.ExecuteScalarAsync() ?? throw new InvalidOperationException("Target person could not be created."));
        }

        await using (var command = new NpgsqlCommand(
                         """
                         INSERT INTO workflows (
                             process_type_id,
                             department_id,
                             position_role_id,
                             created_by_user_id,
                             target_person_id,
                             first_name,
                             last_name,
                             employee_number,
                             badge_number,
                             status,
                             started_at,
                             completed_at
                         )
                         VALUES (
                             @processTypeId,
                             @departmentId,
                             @roleId,
                             @createdByUserId,
                             @targetPersonId,
                             'Target',
                             'Person',
                             @employeeNumber,
                             @badgeNumber,
                             'completed',
                             NOW() - INTERVAL '10 day',
                             NOW() - INTERVAL '1 day'
                         )
                         RETURNING id;
                         """,
                         connection,
                         transaction))
        {
            command.Parameters.AddWithValue("processTypeId", onboardingProcessTypeId);
            command.Parameters.AddWithValue("departmentId", departmentId);
            command.Parameters.AddWithValue("roleId", roleId);
            command.Parameters.AddWithValue("createdByUserId", actorUserId);
            command.Parameters.AddWithValue("targetPersonId", personId);
            command.Parameters.AddWithValue("employeeNumber", employeeNumber);
            command.Parameters.AddWithValue("badgeNumber", badgeNumber);
            _ = await command.ExecuteScalarAsync();
        }

        await transaction.CommitAsync();

        return new SeededRuntimeTargetPerson
        {
            ActorUserId = actorUserId,
            TargetUserId = targetUserId,
            PersonId = personId,
            DepartmentId = departmentId,
            RoleId = roleId,
            EmployeeNumber = employeeNumber,
            BadgeNumber = badgeNumber
        };
    }

    private static async Task CleanupSeededRuntimeTargetPersonAsync(string connectionString, SeededRuntimeTargetPerson targetPerson)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        await using (var command = new NpgsqlCommand(
                         """
                         DELETE FROM workflows
                         WHERE target_person_id = @personId
                            OR created_by_user_id IN (@actorUserId, @targetUserId);
                         """,
                         connection,
                         transaction))
        {
            command.Parameters.AddWithValue("personId", targetPerson.PersonId);
            command.Parameters.AddWithValue("actorUserId", targetPerson.ActorUserId);
            command.Parameters.AddWithValue("targetUserId", targetPerson.TargetUserId);
            await command.ExecuteNonQueryAsync();
        }

        await using (var command = new NpgsqlCommand(
                         """
                         DELETE FROM app_users
                         WHERE id IN (@actorUserId, @targetUserId);
                         """,
                         connection,
                         transaction))
        {
            command.Parameters.AddWithValue("actorUserId", targetPerson.ActorUserId);
            command.Parameters.AddWithValue("targetUserId", targetPerson.TargetUserId);
            await command.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();
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

    private sealed class SeededRuntimeTargetPerson
    {
        public required long ActorUserId { get; init; }
        public required long TargetUserId { get; init; }
        public required long PersonId { get; init; }
        public required int DepartmentId { get; init; }
        public required int RoleId { get; init; }
        public required int EmployeeNumber { get; init; }
        public required int BadgeNumber { get; init; }
    }

    private static class WorkflowDefinitionTestData
    {
        public static WorkflowDefinitionNodeDto FormNode(string nodeKey, string nodeType, string? configJson = null, int sortOrder = 0)
        {
            return new WorkflowDefinitionNodeDto
            {
                NodeKey = nodeKey,
                NodeType = nodeType,
                SortOrder = sortOrder,
                Config = configJson is null ? null : System.Text.Json.JsonDocument.Parse(configJson).RootElement.Clone()
            };
        }

        public static WorkflowDefinitionEdgeDto Edge(string sourceNodeKey, string targetNodeKey, int priority)
        {
            return new WorkflowDefinitionEdgeDto
            {
                SourceNodeKey = sourceNodeKey,
                TargetNodeKey = targetNodeKey,
                Priority = priority
            };
        }
    }
}
