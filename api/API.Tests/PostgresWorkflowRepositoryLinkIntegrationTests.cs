using Npgsql;
using Xunit;

namespace API.Tests;

[Collection(PostgresWorkflowRepositoryIntegrationCollection.Name)]
public sealed class PostgresWorkflowRepositoryLinkIntegrationTests
{
    private const string DefaultTestConnectionString = "Host=localhost;Port=25432;Database=appdb;Username=app;Password=app_pw";

    [Fact]
    [Trait("Category", "Integration")]
    public async Task DeleteWorkflowLink_RequiresWorkflowPerspectiveMembership()
    {
        var connectionString = GetTestConnectionString();
        await EnsureWorkflowLinkSchemaAsync(connectionString);
        var departmentId = await LoadDepartmentIdAsync(connectionString, "IT");
        var roleId = await LoadRoleIdAsync(connectionString, "position_developer");
        var actorUserId = await DirectorySyncedTestUserHelper.EnsureUserAsync(
            connectionString,
            "integration-admin@kauth.local",
            "Integration Admin");

        var sourceWorkflow = await CreateWorkflowAsync(connectionString, departmentId, roleId, "onboarding");
        var targetWorkflow = await CreateWorkflowAsync(connectionString, departmentId, roleId, "onboarding");
        var unrelatedWorkflow = await CreateWorkflowAsync(connectionString, departmentId, roleId, "onboarding");
        var linkId = await InsertWorkflowLinkAsync(connectionString, sourceWorkflow.WorkflowId, targetWorkflow.WorkflowId, actorUserId);

        try
        {
            var deleted = await WithRepositoryConnectionStringAsync(
                connectionString,
                repository => repository.DeleteWorkflowLink(unrelatedWorkflow.WorkflowUid, linkId, actorUserId));

            Assert.False(deleted);
            Assert.True(await WorkflowLinkExistsAsync(connectionString, linkId));
        }
        finally
        {
            await CleanupWorkflowLinkAsync(connectionString, linkId);
            await CleanupWorkflowAsync(connectionString, sourceWorkflow.WorkflowId);
            await CleanupWorkflowAsync(connectionString, targetWorkflow.WorkflowId);
            await CleanupWorkflowAsync(connectionString, unrelatedWorkflow.WorkflowId);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task CreateWorkflowLink_DerivedFrom_CopiesSelectedOptionByOptionValue()
    {
        var connectionString = GetTestConnectionString();
        await EnsureWorkflowLinkSchemaAsync(connectionString);
        var departmentId = await LoadDepartmentIdAsync(connectionString, "IT");
        var roleId = await LoadRoleIdAsync(connectionString, "position_developer");
        var actorUserId = await DirectorySyncedTestUserHelper.EnsureUserAsync(
            connectionString,
            "integration-admin@kauth.local",
            "Integration Admin");

        var sourceProcessType = await CreateTemporaryProcessTypeAsync(connectionString, "source");
        var targetProcessType = await CreateTemporaryProcessTypeAsync(connectionString, "target");

        var sourceAnswer = await InsertSelectAnswerDefinitionWithOptionAsync(
            connectionString,
            sourceProcessType.ProcessTypeId,
            $"src_select_{sourceProcessType.Suffix}",
            "shared_value");
        var targetAnswer = await InsertSelectAnswerDefinitionWithOptionAsync(
            connectionString,
            targetProcessType.ProcessTypeId,
            $"tgt_select_{targetProcessType.Suffix}",
            "shared_value");

        var sourceWorkflow = await CreateWorkflowAsync(connectionString, departmentId, roleId, sourceProcessType.ProcessTypeKey);
        var targetWorkflow = await CreateWorkflowAsync(connectionString, departmentId, roleId, targetProcessType.ProcessTypeKey);

        await InsertWorkflowAnswerAsync(
            connectionString,
            sourceWorkflow.WorkflowId,
            sourceAnswer.AnswerDefinitionId,
            sourceAnswer.AnswerKey,
            sourceAnswer.OptionId);
        await InsertDerivationRuleAsync(
            connectionString,
            sourceProcessType.ProcessTypeId,
            targetProcessType.ProcessTypeId,
            sourceAnswer.AnswerKey,
            targetAnswer.AnswerKey,
            "copy_selected_option");

        try
        {
            var link = await WithRepositoryConnectionStringAsync(
                connectionString,
                repository => repository.CreateWorkflowLink(
                    targetWorkflow.WorkflowUid,
                    new CreateWorkflowLinkRequest
                    {
                        SourceWorkflowUid = sourceWorkflow.WorkflowUid,
                        LinkType = "derived_from"
                    },
                    actorUserId));

            Assert.NotNull(link);

            var targetSelectedOptionId = await LoadSelectedOptionIdAsync(connectionString, targetWorkflow.WorkflowId, targetAnswer.AnswerKey);
            Assert.Equal(targetAnswer.OptionId, targetSelectedOptionId);
        }
        finally
        {
            await CleanupWorkflowAsync(connectionString, sourceWorkflow.WorkflowId);
            await CleanupWorkflowAsync(connectionString, targetWorkflow.WorkflowId);
            await CleanupTemporaryProcessTypeAsync(connectionString, sourceProcessType);
            await CleanupTemporaryProcessTypeAsync(connectionString, targetProcessType);
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
            return await action(new PostgresWorkflowRepository());
        }
        finally
        {
            Environment.SetEnvironmentVariable("CONNECTION_STRING", previousConnectionString);
        }
    }

    private static async Task EnsureWorkflowLinkSchemaAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            CREATE TABLE IF NOT EXISTS workflow_links (
                id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                source_workflow_id BIGINT NOT NULL REFERENCES workflows(id) ON DELETE CASCADE,
                target_workflow_id BIGINT NOT NULL REFERENCES workflows(id) ON DELETE CASCADE,
                link_type VARCHAR(40) NOT NULL CHECK (link_type IN ('derived_from', 'supersedes', 'related')),
                created_by_user_id BIGINT REFERENCES app_users(id) ON DELETE SET NULL,
                notes TEXT,
                created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                UNIQUE (source_workflow_id, target_workflow_id, link_type),
                CHECK (source_workflow_id <> target_workflow_id)
            );

            CREATE INDEX IF NOT EXISTS idx_workflow_links_source ON workflow_links(source_workflow_id);
            CREATE INDEX IF NOT EXISTS idx_workflow_links_target ON workflow_links(target_workflow_id);

            CREATE TABLE IF NOT EXISTS workflow_answer_derivation_rules (
                id INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                source_process_type_id INTEGER NOT NULL REFERENCES process_types(id) ON DELETE CASCADE,
                target_process_type_id INTEGER NOT NULL REFERENCES process_types(id) ON DELETE CASCADE,
                source_answer_key VARCHAR(120) NOT NULL REFERENCES workflow_answer_definitions(answer_key) ON UPDATE CASCADE ON DELETE CASCADE,
                target_answer_key VARCHAR(120) NOT NULL REFERENCES workflow_answer_definitions(answer_key) ON UPDATE CASCADE ON DELETE CASCADE,
                derivation_kind VARCHAR(40) NOT NULL CHECK (derivation_kind IN ('copy_boolean', 'copy_text', 'copy_number', 'copy_selected_option')),
                is_active BOOLEAN NOT NULL DEFAULT TRUE,
                sort_order INTEGER NOT NULL DEFAULT 0,
                created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                UNIQUE (source_answer_key, target_answer_key),
                CHECK (source_process_type_id <> target_process_type_id)
            );

            CREATE INDEX IF NOT EXISTS idx_derivation_rules_source_process
                ON workflow_answer_derivation_rules(source_process_type_id);
            CREATE INDEX IF NOT EXISTS idx_derivation_rules_target_process
                ON workflow_answer_derivation_rules(target_process_type_id);
            """,
            connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<int> LoadDepartmentIdAsync(string connectionString, string departmentName)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            SELECT id
            FROM departments
            WHERE name = @name
            LIMIT 1;
            """,
            connection);
        command.Parameters.AddWithValue("name", departmentName);
        return (int)(await command.ExecuteScalarAsync()
            ?? throw new InvalidOperationException($"Department '{departmentName}' not found."));
    }

    private static async Task<int> LoadRoleIdAsync(string connectionString, string roleKey)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            SELECT id
            FROM app_roles
            WHERE role_key = @roleKey
            LIMIT 1;
            """,
            connection);
        command.Parameters.AddWithValue("roleKey", roleKey);
        return (int)(await command.ExecuteScalarAsync()
            ?? throw new InvalidOperationException($"Role '{roleKey}' not found."));
    }

    private static async Task<TemporaryProcessType> CreateTemporaryProcessTypeAsync(string connectionString, string prefix)
    {
        var suffix = Guid.NewGuid().ToString("N");
        var processTypeKey = $"it_{prefix}_{suffix}";

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
                'Integration link test process type',
                FALSE,
                NULL,
                FALSE,
                TRUE,
                9999
            )
            RETURNING id;
            """,
            connection);
        command.Parameters.AddWithValue("key", processTypeKey);
        command.Parameters.AddWithValue("name", $"Integration {prefix} {suffix}");

        var processTypeId = (int)(await command.ExecuteScalarAsync()
            ?? throw new InvalidOperationException("Temporary process type could not be created."));

        return new TemporaryProcessType
        {
            ProcessTypeId = processTypeId,
            ProcessTypeKey = processTypeKey,
            Suffix = suffix
        };
    }

    private static async Task<SelectAnswerDefinition> InsertSelectAnswerDefinitionWithOptionAsync(
        string connectionString,
        int processTypeId,
        string answerKey,
        string optionValue)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        int answerDefinitionId;
        await using (var definitionCommand = new NpgsqlCommand(
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
                             'Integration Select',
                             'general',
                             'Integration select answer',
                             'identitat',
                             'select',
                             FALSE,
                             10,
                             TRUE
                         )
                         RETURNING id;
                         """,
                         connection,
                         transaction))
        {
            definitionCommand.Parameters.AddWithValue("processTypeId", processTypeId);
            definitionCommand.Parameters.AddWithValue("answerKey", answerKey);
            answerDefinitionId = (int)(await definitionCommand.ExecuteScalarAsync()
                ?? throw new InvalidOperationException("Answer definition could not be created."));
        }

        int optionId;
        await using (var optionCommand = new NpgsqlCommand(
                         """
                         INSERT INTO workflow_answer_options (
                             answer_definition_id,
                             option_key,
                             option_value,
                             option_label,
                             sort_order
                         )
                         VALUES (
                             @answerDefinitionId,
                             @optionKey,
                             @optionValue,
                             @optionLabel,
                             10
                         )
                         RETURNING id;
                         """,
                         connection,
                         transaction))
        {
            optionCommand.Parameters.AddWithValue("answerDefinitionId", answerDefinitionId);
            optionCommand.Parameters.AddWithValue("optionKey", $"opt_{answerKey}");
            optionCommand.Parameters.AddWithValue("optionValue", optionValue);
            optionCommand.Parameters.AddWithValue("optionLabel", optionValue);
            optionId = (int)(await optionCommand.ExecuteScalarAsync()
                ?? throw new InvalidOperationException("Answer option could not be created."));
        }

        await transaction.CommitAsync();

        return new SelectAnswerDefinition
        {
            AnswerDefinitionId = answerDefinitionId,
            AnswerKey = answerKey,
            OptionId = optionId
        };
    }

    private static async Task<TestWorkflow> CreateWorkflowAsync(
        string connectionString,
        int departmentId,
        int roleId,
        string processTypeKey)
    {
        var suffix = Guid.NewGuid().ToString("N");
        var employeeNumber = Math.Abs(suffix[..8].GetHashCode());
        var badgeNumber = Math.Abs(suffix[8..16].GetHashCode());

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO workflows (
                process_type_id,
                department_id,
                position_role_id,
                first_name,
                last_name,
                employee_number,
                badge_number,
                status
            )
            VALUES (
                (SELECT id FROM process_types WHERE key = @processTypeKey),
                @departmentId,
                @roleId,
                'Integration',
                @lastName,
                @employeeNumber,
                @badgeNumber,
                'waiting_for_department'
            )
            RETURNING id, uid;
            """,
            connection);
        command.Parameters.AddWithValue("processTypeKey", processTypeKey);
        command.Parameters.AddWithValue("departmentId", departmentId);
        command.Parameters.AddWithValue("roleId", roleId);
        command.Parameters.AddWithValue("lastName", suffix);
        command.Parameters.AddWithValue("employeeNumber", employeeNumber);
        command.Parameters.AddWithValue("badgeNumber", badgeNumber);

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            throw new InvalidOperationException("Workflow could not be created.");
        }

        return new TestWorkflow
        {
            WorkflowId = reader.GetInt64(0),
            WorkflowUid = reader.GetGuid(1)
        };
    }

    private static async Task InsertWorkflowAnswerAsync(
        string connectionString,
        long workflowId,
        int answerDefinitionId,
        string answerKey,
        int selectedOptionId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO workflow_answers (
                workflow_id,
                answer_definition_id,
                answer_key,
                input_type,
                selected_option_id
            )
            VALUES (
                @workflowId,
                @answerDefinitionId,
                @answerKey,
                'select',
                @selectedOptionId
            );
            """,
            connection);
        command.Parameters.AddWithValue("workflowId", workflowId);
        command.Parameters.AddWithValue("answerDefinitionId", answerDefinitionId);
        command.Parameters.AddWithValue("answerKey", answerKey);
        command.Parameters.AddWithValue("selectedOptionId", selectedOptionId);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task InsertDerivationRuleAsync(
        string connectionString,
        int sourceProcessTypeId,
        int targetProcessTypeId,
        string sourceAnswerKey,
        string targetAnswerKey,
        string derivationKind)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO workflow_answer_derivation_rules (
                source_process_type_id,
                target_process_type_id,
                source_answer_key,
                target_answer_key,
                derivation_kind,
                is_active,
                sort_order
            )
            VALUES (
                @sourceProcessTypeId,
                @targetProcessTypeId,
                @sourceAnswerKey,
                @targetAnswerKey,
                @derivationKind,
                TRUE,
                10
            );
            """,
            connection);
        command.Parameters.AddWithValue("sourceProcessTypeId", sourceProcessTypeId);
        command.Parameters.AddWithValue("targetProcessTypeId", targetProcessTypeId);
        command.Parameters.AddWithValue("sourceAnswerKey", sourceAnswerKey);
        command.Parameters.AddWithValue("targetAnswerKey", targetAnswerKey);
        command.Parameters.AddWithValue("derivationKind", derivationKind);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<long> InsertWorkflowLinkAsync(
        string connectionString,
        long sourceWorkflowId,
        long targetWorkflowId,
        long actorUserId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO workflow_links (
                source_workflow_id,
                target_workflow_id,
                link_type,
                created_by_user_id
            )
            VALUES (
                @sourceWorkflowId,
                @targetWorkflowId,
                'related',
                @actorUserId
            )
            RETURNING id;
            """,
            connection);
        command.Parameters.AddWithValue("sourceWorkflowId", sourceWorkflowId);
        command.Parameters.AddWithValue("targetWorkflowId", targetWorkflowId);
        command.Parameters.AddWithValue("actorUserId", actorUserId);
        return (long)(await command.ExecuteScalarAsync()
            ?? throw new InvalidOperationException("Workflow link could not be created."));
    }

    private static async Task<bool> WorkflowLinkExistsAsync(string connectionString, long linkId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            SELECT EXISTS(
                SELECT 1
                FROM workflow_links
                WHERE id = @linkId
            );
            """,
            connection);
        command.Parameters.AddWithValue("linkId", linkId);
        return (bool)(await command.ExecuteScalarAsync() ?? false);
    }

    private static async Task<int?> LoadSelectedOptionIdAsync(string connectionString, long workflowId, string answerKey)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            SELECT selected_option_id
            FROM workflow_answers
            WHERE workflow_id = @workflowId
              AND answer_key = @answerKey
            LIMIT 1;
            """,
            connection);
        command.Parameters.AddWithValue("workflowId", workflowId);
        command.Parameters.AddWithValue("answerKey", answerKey);

        var scalar = await command.ExecuteScalarAsync();
        return scalar is int selectedOptionId ? selectedOptionId : null;
    }

    private static async Task CleanupWorkflowLinkAsync(string connectionString, long linkId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            DELETE FROM workflow_links
            WHERE id = @linkId;
            """,
            connection);
        command.Parameters.AddWithValue("linkId", linkId);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task CleanupWorkflowAsync(string connectionString, long workflowId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            DELETE FROM workflow_audit_log
            WHERE workflow_id = @workflowId;

            DELETE FROM workflow_answers
            WHERE workflow_id = @workflowId;

            DELETE FROM workflow_tasks
            WHERE workflow_id = @workflowId;

            DELETE FROM workflow_links
            WHERE source_workflow_id = @workflowId
               OR target_workflow_id = @workflowId;

            DELETE FROM workflows
            WHERE id = @workflowId;
            """,
            connection);
        command.Parameters.AddWithValue("workflowId", workflowId);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task CleanupTemporaryProcessTypeAsync(string connectionString, TemporaryProcessType processType)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            DELETE FROM workflow_answer_derivation_rules
            WHERE source_process_type_id = @processTypeId
               OR target_process_type_id = @processTypeId;

            DELETE FROM workflow_answer_options
            WHERE answer_definition_id IN (
                SELECT id
                FROM workflow_answer_definitions
                WHERE process_type_id = @processTypeId
            );

            DELETE FROM workflow_answer_definitions
            WHERE process_type_id = @processTypeId;

            DELETE FROM process_types
            WHERE id = @processTypeId
              AND key = @processTypeKey;
            """,
            connection);
        command.Parameters.AddWithValue("processTypeId", processType.ProcessTypeId);
        command.Parameters.AddWithValue("processTypeKey", processType.ProcessTypeKey);
        await command.ExecuteNonQueryAsync();
    }

    private sealed class TemporaryProcessType
    {
        public required int ProcessTypeId { get; init; }
        public required string ProcessTypeKey { get; init; }
        public required string Suffix { get; init; }
    }

    private sealed class SelectAnswerDefinition
    {
        public required int AnswerDefinitionId { get; init; }
        public required string AnswerKey { get; init; }
        public required int OptionId { get; init; }
    }

    private sealed class TestWorkflow
    {
        public required long WorkflowId { get; init; }
        public required Guid WorkflowUid { get; init; }
    }
}
