using Npgsql;

namespace API;

internal sealed partial class PostgresWorkflowRepository
{
    private static readonly HashSet<string> AllowedAdminTaskTemplateConditionOperators = new(StringComparer.OrdinalIgnoreCase)
    {
        "eq",
        "neq",
        "is_true",
        "is_false",
        "is_null",
        "is_not_null"
    };

    public async Task<List<AdminTaskTemplateConditionDto>> GetAdminTaskTemplateConditions(int templateId)
    {
        if (templateId <= 0)
        {
            throw new InvalidOperationException("templateId must be greater than zero.");
        }

        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();

        var template = await GetAdminTaskTemplateById(connection, null, templateId);
        if (template is null)
        {
            throw new InvalidOperationException("Task template not found.");
        }

        const string sql = @"
SELECT
    id,
    workflow_node_task_spec_id,
    answer_key,
    operator,
    expected_value_text,
    expected_value_boolean,
    expected_value_number
FROM workflow_node_task_spec_conditions
WHERE workflow_node_task_spec_id = @templateId
ORDER BY id;";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("templateId", (long)templateId);
        await using var reader = await command.ExecuteReaderAsync();

        var conditions = new List<AdminTaskTemplateConditionDto>();
        while (await reader.ReadAsync())
        {
            conditions.Add(MapAdminTaskTemplateCondition(reader));
        }

        return conditions;
    }

    public async Task<AdminTaskTemplateConditionDto> CreateAdminTaskTemplateCondition(
        int templateId,
        AdminTaskTemplateConditionCreateRequest request)
    {
        if (templateId <= 0)
        {
            throw new InvalidOperationException("templateId must be greater than zero.");
        }

        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();

        var template = await GetAdminTaskTemplateById(connection, null, templateId);
        if (template is null)
        {
            throw new InvalidOperationException("Task template not found.");
        }

        ValidateAdminTaskTemplateConditionRequest(request);
        await EnsureAnswerKeyBelongsToProcessType(connection, null, request.AnswerKey!, template.WorkflowDefinitionId);

        const string sql = @"
INSERT INTO workflow_node_task_spec_conditions (
    workflow_node_task_spec_id,
    answer_key,
    operator,
    expected_value_text,
    expected_value_boolean,
    expected_value_number
)
VALUES (
    @templateId,
    @answerKey,
    @operator,
    @expectedValueText,
    @expectedValueBoolean,
    @expectedValueNumber
)
RETURNING id;";

        await using var command = new NpgsqlCommand(sql, connection);
        BindAdminTaskTemplateConditionParameters(command, templateId, request);

        var createdId = await command.ExecuteScalarAsync();
        if (createdId is not long conditionId)
        {
            throw new InvalidOperationException("Task spec condition could not be created.");
        }

        return await GetAdminTaskTemplateConditionById(connection, null, templateId, conditionId)
            ?? throw new InvalidOperationException("Task spec condition could not be loaded after creation.");
    }

    public async Task<bool> DeleteAdminTaskTemplateCondition(int templateId, long conditionId)
    {
        if (templateId <= 0)
        {
            throw new InvalidOperationException("templateId must be greater than zero.");
        }

        if (conditionId <= 0)
        {
            throw new InvalidOperationException("conditionId must be greater than zero.");
        }

        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();

        const string sql = @"
DELETE FROM workflow_node_task_spec_conditions
WHERE id = @conditionId
  AND workflow_node_task_spec_id = @templateId;";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("conditionId", conditionId);
        command.Parameters.AddWithValue("templateId", (long)templateId);
        return await command.ExecuteNonQueryAsync() > 0;
    }

    private static AdminTaskTemplateConditionDto MapAdminTaskTemplateCondition(NpgsqlDataReader reader)
    {
        // LA5: condition_group ist nicht mehr im Schema (immer 1 in real data) — Hardcode.
        return new AdminTaskTemplateConditionDto
        {
            Id = reader.GetInt64(0),
            TaskTemplateId = checked((int)reader.GetInt64(1)),
            ConditionGroup = 1,
            AnswerKey = reader.GetString(2),
            Operator = reader.GetString(3),
            ExpectedValueText = reader.IsDBNull(4) ? null : reader.GetString(4),
            ExpectedValueBoolean = reader.IsDBNull(5) ? null : reader.GetBoolean(5),
            ExpectedValueNumber = reader.IsDBNull(6) ? null : reader.GetDecimal(6)
        };
    }

    private static void ValidateAdminTaskTemplateConditionRequest(AdminTaskTemplateConditionCreateRequest request)
    {
        if (request.ConditionGroup <= 0)
        {
            throw new InvalidOperationException("conditionGroup must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(request.AnswerKey))
        {
            throw new InvalidOperationException("Der Answer-Key ist erforderlich.");
        }

        var normalizedOperator = NormalizeAdminTaskTemplateConditionOperator(request.Operator);
        if (!AllowedAdminTaskTemplateConditionOperators.Contains(normalizedOperator))
        {
            throw new InvalidOperationException("Der Operator ist ungültig.");
        }
    }

    private static void BindAdminTaskTemplateConditionParameters(
        NpgsqlCommand command,
        int templateId,
        AdminTaskTemplateConditionCreateRequest request)
    {
        command.Parameters.AddWithValue("templateId", (long)templateId);
        command.Parameters.AddWithValue("answerKey", request.AnswerKey!.Trim());
        command.Parameters.AddWithValue("operator", NormalizeAdminTaskTemplateConditionOperator(request.Operator));
        command.Parameters.AddWithValue("expectedValueText", (object?)NormalizeNullableText(request.ExpectedValueText) ?? DBNull.Value);
        command.Parameters.AddWithValue("expectedValueBoolean", (object?)request.ExpectedValueBoolean ?? DBNull.Value);
        command.Parameters.AddWithValue("expectedValueNumber", (object?)request.ExpectedValueNumber ?? DBNull.Value);
    }

    private static string NormalizeAdminTaskTemplateConditionOperator(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();
    }

    private static async Task EnsureAnswerKeyBelongsToProcessType(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        string answerKey,
        int processTypeId)
    {
        const string sql = @"
SELECT EXISTS(
    SELECT 1
    FROM workflow_answer_definitions
    WHERE answer_key = @answerKey
      AND workflow_definition_id = @processTypeId
);";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("answerKey", answerKey.Trim());
        command.Parameters.AddWithValue("processTypeId", processTypeId);
        var exists = (bool)(await command.ExecuteScalarAsync() ?? false);
        if (!exists)
        {
            throw new InvalidOperationException("Der Answer-Key ist für diesen Prozesstyp ungültig.");
        }
    }

    private static async Task<AdminTaskTemplateConditionDto?> GetAdminTaskTemplateConditionById(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        int templateId,
        long conditionId)
    {
        const string sql = @"
SELECT
    id,
    workflow_node_task_spec_id,
    answer_key,
    operator,
    expected_value_text,
    expected_value_boolean,
    expected_value_number
FROM workflow_node_task_spec_conditions
WHERE id = @conditionId
  AND workflow_node_task_spec_id = @templateId
LIMIT 1;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("conditionId", conditionId);
        command.Parameters.AddWithValue("templateId", (long)templateId);
        await using var reader = await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            return null;
        }

        return MapAdminTaskTemplateCondition(reader);
    }
}
