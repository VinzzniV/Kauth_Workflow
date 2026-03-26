using Npgsql;

namespace API;

internal sealed partial class PostgresWorkflowRepository
{
    public async Task<List<AdminRoleAnswerDefaultDto>> GetAdminRoleAnswerDefaults(int processTypeId)
    {
        if (processTypeId <= 0)
        {
            throw new InvalidOperationException("processTypeId must be greater than zero.");
        }

        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();

        await EnsureProcessTypeExists(connection, null, processTypeId);

        const string sql = @"
SELECT
    ard.process_type_id,
    ard.app_role_id,
    d.answer_key,
    ard.default_value_text,
    ard.default_value_boolean
FROM app_role_answer_defaults ard
JOIN workflow_answer_definitions d ON d.id = ard.answer_definition_id
WHERE ard.process_type_id = @processTypeId
ORDER BY ard.app_role_id, d.answer_key;";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("processTypeId", processTypeId);
        await using var reader = await command.ExecuteReaderAsync();

        var defaults = new List<AdminRoleAnswerDefaultDto>();
        while (await reader.ReadAsync())
        {
            defaults.Add(new AdminRoleAnswerDefaultDto
            {
                ProcessTypeId = reader.GetInt32(0),
                AppRoleId = reader.GetInt32(1),
                AnswerKey = reader.GetString(2),
                DefaultValueText = reader.IsDBNull(3) ? null : reader.GetString(3),
                DefaultValueBoolean = reader.IsDBNull(4) ? null : reader.GetBoolean(4)
            });
        }

        return defaults;
    }

    public async Task<List<AdminRoleAnswerDefaultDto>> UpsertAdminRoleAnswerDefaults(AdminRoleAnswerDefaultsBulkUpsertRequest request)
    {
        ValidateAdminRoleAnswerDefaultsBulkUpsertRequest(request);

        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        await EnsureProcessTypeExists(connection, transaction, request.ProcessTypeId);

        var normalizedItems = request.Items
            .Select(item => new
            {
                AppRoleId = item.AppRoleId,
                AnswerKey = item.AnswerKey!.Trim(),
                DefaultValueText = NormalizeOptionalText(item.DefaultValueText),
                item.DefaultValueBoolean
            })
            .ToList();

        var distinctRoleIds = normalizedItems
            .Select(item => item.AppRoleId)
            .Distinct()
            .ToList();
        await EnsureAppRolesExist(connection, transaction, distinctRoleIds);

        var answerKeyToDefinitionId = await ResolveAnswerDefinitionIdsByKey(
            connection,
            transaction,
            request.ProcessTypeId,
            normalizedItems.Select(item => item.AnswerKey).Distinct().ToList());

        const string sql = @"
INSERT INTO app_role_answer_defaults (
    process_type_id,
    app_role_id,
    answer_definition_id,
    is_recommended,
    is_default,
    default_value_boolean,
    default_value_text,
    sort_order
)
VALUES (
    @processTypeId,
    @appRoleId,
    @answerDefinitionId,
    FALSE,
    FALSE,
    @defaultValueBoolean,
    @defaultValueText,
    0
)
ON CONFLICT (app_role_id, answer_definition_id)
DO UPDATE SET
    process_type_id = EXCLUDED.process_type_id,
    default_value_boolean = EXCLUDED.default_value_boolean,
    default_value_text = EXCLUDED.default_value_text;";

        foreach (var item in normalizedItems)
        {
            await using var command = new NpgsqlCommand(sql, connection, transaction);
            command.Parameters.AddWithValue("processTypeId", request.ProcessTypeId);
            command.Parameters.AddWithValue("appRoleId", item.AppRoleId);
            command.Parameters.AddWithValue("answerDefinitionId", answerKeyToDefinitionId[item.AnswerKey]);
            command.Parameters.AddWithValue("defaultValueBoolean", (object?)item.DefaultValueBoolean ?? DBNull.Value);
            command.Parameters.AddWithValue("defaultValueText", (object?)item.DefaultValueText ?? DBNull.Value);
            await command.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();
        return await GetAdminRoleAnswerDefaults(request.ProcessTypeId);
    }

    private static void ValidateAdminRoleAnswerDefaultsBulkUpsertRequest(AdminRoleAnswerDefaultsBulkUpsertRequest request)
    {
        if (request.ProcessTypeId <= 0)
        {
            throw new InvalidOperationException("processTypeId must be greater than zero.");
        }

        if (request.Items is null)
        {
            throw new InvalidOperationException("Items are required.");
        }

        foreach (var item in request.Items)
        {
            if (item.AppRoleId <= 0)
            {
                throw new InvalidOperationException("appRoleId must be greater than zero.");
            }

            if (string.IsNullOrWhiteSpace(item.AnswerKey))
            {
                throw new InvalidOperationException("Der Answer-Key ist erforderlich.");
            }
        }
    }

    private static async Task EnsureAppRolesExist(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        IReadOnlyCollection<int> appRoleIds)
    {
        if (appRoleIds.Count == 0)
        {
            return;
        }

        const string sql = @"
SELECT id
FROM app_roles
WHERE id = ANY(@appRoleIds);";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("appRoleIds", appRoleIds.ToArray());
        await using var reader = await command.ExecuteReaderAsync();

        var existingIds = new HashSet<int>();
        while (await reader.ReadAsync())
        {
            existingIds.Add(reader.GetInt32(0));
        }

        if (existingIds.Count != appRoleIds.Count)
        {
            throw new InvalidOperationException("Eine oder mehrere App-Rollen sind ungültig.");
        }
    }

    private static async Task<Dictionary<string, int>> ResolveAnswerDefinitionIdsByKey(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int processTypeId,
        IReadOnlyCollection<string> answerKeys)
    {
        const string sql = @"
SELECT answer_key, id
FROM workflow_answer_definitions
WHERE process_type_id = @processTypeId
  AND answer_key = ANY(@answerKeys);";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("processTypeId", processTypeId);
        command.Parameters.AddWithValue("answerKeys", answerKeys.ToArray());
        await using var reader = await command.ExecuteReaderAsync();

        var idsByKey = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        while (await reader.ReadAsync())
        {
            idsByKey[reader.GetString(0)] = reader.GetInt32(1);
        }

        if (idsByKey.Count != answerKeys.Count)
        {
            throw new InvalidOperationException("Ein oder mehrere Answer-Keys sind für diesen Prozesstyp ungültig.");
        }

        return idsByKey;
    }
}
