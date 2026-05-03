using Npgsql;
using NpgsqlTypes;

namespace API;

internal sealed partial class PostgresWorkflowRepository
{
    private static readonly HashSet<string> AllowedAdminAnswerInputTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "boolean",
        "text",
        "select",
        "multi_select"
    };

    public async Task<List<AdminAnswerDefinitionDto>> GetAdminAnswerDefinitions(int workflowDefinitionId)
    {
        if (workflowDefinitionId <= 0)
        {
            throw new InvalidOperationException("workflowDefinitionId must be greater than zero.");
        }

        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();

        var processTypeId = await EnsureProcessTypeExists(connection, null, workflowDefinitionId);

        const string sql = @"
SELECT
    d.id,
    d.workflow_definition_id,
    d.answer_key,
    d.title,
    d.category,
    d.description,
    d.icon_key,
    d.input_type,
    d.is_required,
    d.sort_order,
    d.is_active
FROM workflow_answer_definitions d
WHERE d.workflow_definition_id = @processTypeId
ORDER BY d.sort_order, d.title, d.id;";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("processTypeId", processTypeId);
        await using var reader = await command.ExecuteReaderAsync();

        var definitions = new List<AdminAnswerDefinitionDto>();
        while (await reader.ReadAsync())
        {
            definitions.Add(MapAdminAnswerDefinition(reader));
        }

        return definitions;
    }

    public async Task<AdminAnswerDefinitionDto> CreateAdminAnswerDefinition(AdminAnswerDefinitionUpsertRequest request)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();

        ValidateAdminAnswerDefinitionRequest(request);
        var normalizedProcessTypeId = await EnsureProcessTypeExists(connection, null, request.WorkflowDefinitionId);
        await EnsureAnswerKeyAvailable(connection, null, request.AnswerKey!, null);

        const string sql = @"
INSERT INTO workflow_answer_definitions (
    workflow_definition_id,
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
    @title,
    @category,
    @description,
    @iconKey,
    @inputType,
    @isRequired,
    @sortOrder,
    @isActive
)
RETURNING id;";

        await using var command = new NpgsqlCommand(sql, connection);
        BindAdminAnswerDefinitionParameters(command, request, normalizedProcessTypeId);

        var createdId = await command.ExecuteScalarAsync();
        if (createdId is not int definitionId)
        {
            throw new InvalidOperationException("Answer definition could not be created.");
        }

        return await GetAdminAnswerDefinitionById(connection, null, definitionId)
            ?? throw new InvalidOperationException("Answer definition could not be loaded after creation.");
    }

    public async Task<AdminAnswerDefinitionDto?> UpdateAdminAnswerDefinition(
        int definitionId,
        AdminAnswerDefinitionUpsertRequest request)
    {
        if (definitionId <= 0)
        {
            throw new InvalidOperationException("definitionId must be greater than zero.");
        }

        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();

        ValidateAdminAnswerDefinitionRequest(request);
        var normalizedProcessTypeId = await EnsureProcessTypeExists(connection, null, request.WorkflowDefinitionId);
        await EnsureAnswerKeyAvailable(connection, null, request.AnswerKey!, definitionId);

        const string sql = @"
UPDATE workflow_answer_definitions
SET
    workflow_definition_id = @processTypeId,
    answer_key = @answerKey,
    title = @title,
    category = @category,
    description = @description,
    icon_key = @iconKey,
    input_type = @inputType,
    is_required = @isRequired,
    sort_order = @sortOrder,
    is_active = @isActive
WHERE id = @definitionId
RETURNING id;";

        await using var command = new NpgsqlCommand(sql, connection);
        BindAdminAnswerDefinitionParameters(command, request, normalizedProcessTypeId);
        command.Parameters.AddWithValue("definitionId", definitionId);

        var updatedId = await command.ExecuteScalarAsync();
        if (updatedId is null)
        {
            return null;
        }

        return await GetAdminAnswerDefinitionById(connection, null, definitionId);
    }

    public async Task<bool> DeleteAdminAnswerDefinition(int definitionId)
    {
        if (definitionId <= 0)
        {
            throw new InvalidOperationException("definitionId must be greater than zero.");
        }

        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();

        var existingDefinition = await GetAdminAnswerDefinitionById(connection, null, definitionId);
        if (existingDefinition is null)
        {
            return false;
        }

        if (await AnswerDefinitionHasTaskTemplateConditionReferences(connection, null, existingDefinition.AnswerKey))
        {
            throw new InvalidOperationException(
                "Die Answer Definition wird in Task-Template-Bedingungen verwendet und kann nicht gelöscht werden.");
        }

        if (await AnswerDefinitionHasWorkflowAnswerReferences(connection, null, definitionId, existingDefinition.AnswerKey))
        {
            throw new InvalidOperationException(
                "Die Answer Definition wird bereits von Workflow-Antworten referenziert und kann nicht gelöscht werden.");
        }

        const string sql = @"
DELETE FROM workflow_answer_definitions
WHERE id = @definitionId;";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("definitionId", definitionId);
        return await command.ExecuteNonQueryAsync() > 0;
    }

    private static AdminAnswerDefinitionDto MapAdminAnswerDefinition(NpgsqlDataReader reader)
    {
        return new AdminAnswerDefinitionDto
        {
            Id = reader.GetInt32(0),
            WorkflowDefinitionId = reader.GetInt32(1),
            AnswerKey = reader.GetString(2),
            Title = reader.GetString(3),
            Category = reader.GetString(4),
            Description = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
            IconKey = reader.IsDBNull(6) ? null : reader.GetString(6),
            InputType = reader.GetString(7),
            IsRequired = reader.GetBoolean(8),
            SortOrder = reader.GetInt32(9),
            IsActive = reader.GetBoolean(10)
        };
    }

    private static void ValidateAdminAnswerDefinitionRequest(AdminAnswerDefinitionUpsertRequest request)
    {
        if (request.WorkflowDefinitionId <= 0)
        {
            throw new InvalidOperationException("processTypeId must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(request.AnswerKey))
        {
            throw new InvalidOperationException("Der Answer-Key ist erforderlich.");
        }

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            throw new InvalidOperationException("Der Titel ist erforderlich.");
        }

        var normalizedInputType = NormalizeAdminAnswerInputType(request.InputType);
        if (!AllowedAdminAnswerInputTypes.Contains(normalizedInputType))
        {
            throw new InvalidOperationException("Der Input-Typ ist ungültig.");
        }
    }

    private static void BindAdminAnswerDefinitionParameters(
        NpgsqlCommand command,
        AdminAnswerDefinitionUpsertRequest request,
        int normalizedProcessTypeId)
    {
        command.Parameters.AddWithValue("processTypeId", normalizedProcessTypeId);
        command.Parameters.AddWithValue("answerKey", request.AnswerKey!.Trim());
        command.Parameters.AddWithValue("title", request.Title!.Trim());
        command.Parameters.AddWithValue("category", string.IsNullOrWhiteSpace(request.Category) ? "general" : request.Category.Trim());
        command.Parameters.AddWithValue("description", request.Description?.Trim() ?? string.Empty);
        command.Parameters.AddWithValue("iconKey", (object?)NormalizeOptionalText(request.IconKey) ?? "berechtigungen");
        command.Parameters.AddWithValue("inputType", NormalizeAdminAnswerInputType(request.InputType));
        command.Parameters.AddWithValue("isRequired", request.IsRequired);
        command.Parameters.AddWithValue("sortOrder", request.SortOrder);
        command.Parameters.AddWithValue("isActive", request.IsActive);
    }

    private static string NormalizeAdminAnswerInputType(string? inputType)
    {
        return string.IsNullOrWhiteSpace(inputType) ? string.Empty : inputType.Trim().ToLowerInvariant();
    }

    private static async Task EnsureAnswerKeyAvailable(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        string answerKey,
        int? excludedDefinitionId)
    {
        const string sql = @"
SELECT id
FROM workflow_answer_definitions
WHERE answer_key = @answerKey
  AND (@excludedDefinitionId IS NULL OR id <> @excludedDefinitionId)
LIMIT 1;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("answerKey", answerKey.Trim());
        command.Parameters.Add("excludedDefinitionId", NpgsqlDbType.Integer).Value =
            (object?)excludedDefinitionId ?? DBNull.Value;

        if (await command.ExecuteScalarAsync() is not null)
        {
            throw new InvalidOperationException("Eine Answer Definition mit diesem Key existiert bereits.");
        }
    }

    private static async Task<bool> AnswerDefinitionHasTaskTemplateConditionReferences(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        string answerKey)
    {
        const string sql = @"
SELECT EXISTS(
    SELECT 1
    FROM workflow_node_task_spec_conditions
    WHERE answer_key = @answerKey
);";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("answerKey", answerKey);
        return (bool)(await command.ExecuteScalarAsync() ?? false);
    }

    private static async Task<bool> AnswerDefinitionHasWorkflowAnswerReferences(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        int definitionId,
        string answerKey)
    {
        const string sql = @"
SELECT EXISTS(
    SELECT 1
    FROM workflow_answers
    WHERE answer_definition_id = @definitionId
       OR answer_key = @answerKey
);";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("definitionId", definitionId);
        command.Parameters.AddWithValue("answerKey", answerKey);
        return (bool)(await command.ExecuteScalarAsync() ?? false);
    }

    private static async Task<AdminAnswerDefinitionDto?> GetAdminAnswerDefinitionById(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        int definitionId)
    {
        const string sql = @"
SELECT
    d.id,
    d.workflow_definition_id,
    d.answer_key,
    d.title,
    d.category,
    d.description,
    d.icon_key,
    d.input_type,
    d.is_required,
    d.sort_order,
    d.is_active
FROM workflow_answer_definitions d
WHERE d.id = @definitionId
LIMIT 1;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("definitionId", definitionId);
        await using var reader = await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            return null;
        }

        return MapAdminAnswerDefinition(reader);
    }
}
