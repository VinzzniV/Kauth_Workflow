using Npgsql;
using Xunit;

namespace API.Tests;

[Collection(PostgresWorkflowRepositoryIntegrationCollection.Name)]
public sealed class PostgresWorkflowRepositoryProcessTypeIntegrationTests
{
    private const string DefaultTestConnectionString = "Host=localhost;Port=26432;Database=appdb;Username=app;Password=app_pw";

    [Fact]
    [Trait("Category", "Integration")]
    public async Task UpdateProcessType_RejectsActivation_WhenNoActiveRequirementsExist()
    {
        var connectionString = GetTestConnectionString();
        var processType = await CreateTemporaryProcessTypeAsync(connectionString);
        await InsertTaskTemplateAsync(connectionString, processType.Id, $"pt_task_{processType.Suffix}");

        try
        {
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                WithRepositoryConnectionStringAsync(
                    connectionString,
                    repository => repository.UpdateProcessType(
                        processType.Id,
                        new AdminProcessTypeUpdateRequest { IsActive = true })));

            Assert.Equal("Keine aktiven Anforderungen konfiguriert.", error.Message);
        }
        finally
        {
            await CleanupTemporaryProcessTypeAsync(connectionString, processType.Id, processType.Suffix);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task UpdateProcessType_AllowsActivation_WhenConfigurationIsComplete()
    {
        var connectionString = GetTestConnectionString();
        var processType = await CreateTemporaryProcessTypeAsync(connectionString);
        await InsertAnswerDefinitionAsync(connectionString, processType.Id, $"pt_answer_{processType.Suffix}");
        await InsertTaskTemplateAsync(connectionString, processType.Id, $"pt_task_{processType.Suffix}");

        try
        {
            var updated = await WithRepositoryConnectionStringAsync(
                connectionString,
                repository => repository.UpdateProcessType(
                    processType.Id,
                    new AdminProcessTypeUpdateRequest { IsActive = true }));

            Assert.NotNull(updated);
            Assert.True(updated!.IsActive);
            Assert.True(updated.CanActivate);
            Assert.Null(updated.ActivationBlockedReason);
        }
        finally
        {
            await CleanupTemporaryProcessTypeAsync(connectionString, processType.Id, processType.Suffix);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task UpdateProcessType_RejectsActivation_WhenApprovalTaskIsMissing()
    {
        var connectionString = GetTestConnectionString();
        var processType = await CreateTemporaryProcessTypeAsync(connectionString, requiresSupervisorStep: true, approvalTaskTemplateKey: $"approval_missing_{Guid.NewGuid():N}");
        await InsertAnswerDefinitionAsync(connectionString, processType.Id, $"pt_answer_{processType.Suffix}");
        await InsertTaskTemplateAsync(connectionString, processType.Id, $"pt_task_{processType.Suffix}");

        try
        {
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                WithRepositoryConnectionStringAsync(
                    connectionString,
                    repository => repository.UpdateProcessType(
                        processType.Id,
                        new AdminProcessTypeUpdateRequest { IsActive = true })));

            Assert.Equal("Konfigurierter Freigabe-Task fehlt oder ist inaktiv.", error.Message);
        }
        finally
        {
            await CleanupTemporaryProcessTypeAsync(connectionString, processType.Id, processType.Suffix);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetWorkflowByUid_LoadsOnlyRequirementsForWorkflowProcessType()
    {
        var connectionString = GetTestConnectionString();
        var primaryProcessType = await CreateTemporaryProcessTypeAsync(connectionString, isActive: true);
        var foreignProcessType = await CreateTemporaryProcessTypeAsync(connectionString, isActive: true);
        var primaryAnswerKey = $"pt_answer_primary_{primaryProcessType.Suffix}";
        var foreignAnswerKey = $"pt_answer_foreign_{foreignProcessType.Suffix}";
        await InsertAnswerDefinitionAsync(connectionString, primaryProcessType.Id, primaryAnswerKey);
        await InsertAnswerDefinitionAsync(connectionString, foreignProcessType.Id, foreignAnswerKey);
        var workflow = await InsertWorkflowAsync(connectionString, primaryProcessType.Id, primaryProcessType.Suffix);

        try
        {
            var loaded = await WithRepositoryConnectionStringAsync(
                connectionString,
                repository => repository.GetWorkflowByUid(workflow.Uid));

            Assert.NotNull(loaded);
            Assert.Contains(loaded!.Requirements, requirement => requirement.Key == primaryAnswerKey);
            Assert.DoesNotContain(loaded.Requirements, requirement => requirement.Key == foreignAnswerKey);
        }
        finally
        {
            await CleanupWorkflowAsync(connectionString, workflow.Id);
            await CleanupTemporaryProcessTypeAsync(connectionString, primaryProcessType.Id, primaryProcessType.Suffix);
            await CleanupTemporaryProcessTypeAsync(connectionString, foreignProcessType.Id, foreignProcessType.Suffix);
        }
    }

    private static string GetTestConnectionString()
    {
        return Environment.GetEnvironmentVariable("ONBOARDING_TEST_CONNECTION_STRING")
               ?? DefaultTestConnectionString;
    }

    private static async Task<T> WithRepositoryConnectionStringAsync<T>(
        string connectionString,
        Func<PostgresWorkflowRepository, Task<T>> action)
    {
        var previousConnectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING");
        Environment.SetEnvironmentVariable("CONNECTION_STRING", connectionString);

        try
        {
            var repository = new PostgresWorkflowRepository();
            return await action(repository);
        }
        finally
        {
            Environment.SetEnvironmentVariable("CONNECTION_STRING", previousConnectionString);
        }
    }

    private static async Task<TemporaryProcessType> CreateTemporaryProcessTypeAsync(
        string connectionString,
        bool requiresSupervisorStep = false,
        string? approvalTaskTemplateKey = null,
        bool isActive = false)
    {
        var suffix = Guid.NewGuid().ToString("N");

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            INSERT INTO process_types (
                key,
                name,
                description,
                requires_supervisor_step,
                approval_task_template_key,
                requires_target_person,
                is_active,
                sort_order
            )
            VALUES (
                @key,
                @name,
                @description,
                @requiresSupervisorStep,
                @approvalTaskTemplateKey,
                FALSE,
                @isActive,
                9999
            )
            RETURNING id;
            """,
            connection);
        command.Parameters.AddWithValue("key", $"integration_process_{suffix}");
        command.Parameters.AddWithValue("name", $"Integration Process {suffix}");
        command.Parameters.AddWithValue("description", "Integration test process type");
        command.Parameters.AddWithValue("requiresSupervisorStep", requiresSupervisorStep);
        command.Parameters.AddWithValue("isActive", isActive);
        command.Parameters.AddWithValue(
            "approvalTaskTemplateKey",
            string.IsNullOrWhiteSpace(approvalTaskTemplateKey) ? DBNull.Value : approvalTaskTemplateKey);

        var id = (int)(await command.ExecuteScalarAsync()
            ?? throw new InvalidOperationException("Temporary process type could not be created."));

        return new TemporaryProcessType
        {
            Id = id,
            Suffix = suffix,
        };
    }

    private static async Task InsertAnswerDefinitionAsync(string connectionString, int processTypeId, string answerKey)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            INSERT INTO workflow_answer_definitions (
                process_type_id,
                answer_key,
                title,
                category,
                description,
                icon_key,
                input_type,
                is_required,
                sort_order,
                is_active
            )
            VALUES (
                @processTypeId,
                @answerKey,
                'Integration Requirement',
                'general',
                'Activation test requirement',
                'berechtigungen',
                'boolean',
                FALSE,
                10,
                TRUE
            );
            """,
            connection);
        command.Parameters.AddWithValue("processTypeId", processTypeId);
        command.Parameters.AddWithValue("answerKey", answerKey);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<TemporaryWorkflow> InsertWorkflowAsync(string connectionString, int processTypeId, string suffix)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        int roleId;
        int departmentId;

        await using (var contextCommand = new NpgsqlCommand(
            """
            SELECT r.id, r.department_id
            FROM app_roles r
            WHERE r.role_kind = 'position'
              AND r.is_active = TRUE
            ORDER BY r.id
            LIMIT 1;
            """,
            connection))
        {
            await using var reader = await contextCommand.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
            {
                throw new InvalidOperationException("No active position role exists for integration test workflow creation.");
            }

            roleId = reader.GetInt32(0);
            departmentId = reader.GetInt32(1);
        }

        var employeeNumber = 700000 + Math.Abs(suffix.GetHashCode(StringComparison.Ordinal)) % 100000;
        var badgeNumber = employeeNumber + 100000;

        await using var workflowCommand = new NpgsqlCommand(
            """
            INSERT INTO workflows (
                process_type_id,
                department_id,
                position_role_id,
                created_by_user_id,
                first_name,
                last_name,
                employee_number,
                badge_number,
                status
            )
            VALUES (
                @processTypeId,
                @departmentId,
                @roleId,
                NULL,
                'Integration',
                @lastName,
                @employeeNumber,
                @badgeNumber,
                'waiting_for_supervisor'
            )
            RETURNING id, uid;
            """,
            connection);
        workflowCommand.Parameters.AddWithValue("processTypeId", processTypeId);
        workflowCommand.Parameters.AddWithValue("departmentId", departmentId);
        workflowCommand.Parameters.AddWithValue("roleId", roleId);
        workflowCommand.Parameters.AddWithValue("lastName", $"Workflow {suffix}");
        workflowCommand.Parameters.AddWithValue("employeeNumber", employeeNumber);
        workflowCommand.Parameters.AddWithValue("badgeNumber", badgeNumber);

        await using var workflowReader = await workflowCommand.ExecuteReaderAsync();
        if (!await workflowReader.ReadAsync())
        {
            throw new InvalidOperationException("Temporary workflow could not be created.");
        }

        return new TemporaryWorkflow
        {
            Id = workflowReader.GetInt64(0),
            Uid = workflowReader.GetGuid(1)
        };
    }

    private static async Task InsertTaskTemplateAsync(string connectionString, int processTypeId, string templateKey)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            INSERT INTO task_templates (
                process_type_id,
                template_key,
                title,
                category,
                description,
                icon_key,
                is_department_phase_task,
                is_required,
                sort_order,
                is_active
            )
            VALUES (
                @processTypeId,
                @templateKey,
                'Integration Task',
                'general',
                'Activation test task',
                'berechtigungen',
                TRUE,
                TRUE,
                10,
                TRUE
            );
            """,
            connection);
        command.Parameters.AddWithValue("processTypeId", processTypeId);
        command.Parameters.AddWithValue("templateKey", templateKey);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task CleanupTemporaryProcessTypeAsync(string connectionString, int processTypeId, string suffix)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            DELETE FROM task_templates
            WHERE process_type_id = @processTypeId;

            DELETE FROM workflow_answer_definitions
            WHERE process_type_id = @processTypeId;

            DELETE FROM process_types
            WHERE id = @processTypeId
              AND key = @processTypeKey;
            """,
            connection);
        command.Parameters.AddWithValue("processTypeId", processTypeId);
        command.Parameters.AddWithValue("processTypeKey", $"integration_process_{suffix}");
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

    private sealed class TemporaryProcessType
    {
        public required int Id { get; init; }
        public required string Suffix { get; init; }
    }

    private sealed class TemporaryWorkflow
    {
        public required long Id { get; init; }
        public required Guid Uid { get; init; }
    }
}
