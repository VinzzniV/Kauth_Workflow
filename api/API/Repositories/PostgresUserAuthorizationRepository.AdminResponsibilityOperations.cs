using Npgsql;
using NpgsqlTypes;

namespace API;

internal sealed partial class PostgresUserAuthorizationRepository
{
    public async Task<AdminResponsibilityOwnerDto> CreateResponsibility(
        string responsibilityName,
        int? departmentId,
        CancellationToken cancellationToken = default)
    {
        var normalizedResponsibilityName = NormalizeRequired(
            responsibilityName,
            "Responsibility name is required.");
        var normalizedDepartmentId = NormalizeNullableDepartmentId(departmentId);

        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        if (normalizedDepartmentId.HasValue
            && !await DepartmentExists(connection, transaction, normalizedDepartmentId.Value, cancellationToken))
        {
            throw new InvalidOperationException("Selected department is invalid.");
        }

        var responsibilityKey = await GenerateUniqueResponsibilityKey(
            connection,
            transaction,
            normalizedResponsibilityName,
            cancellationToken);
        var systemKey = await GenerateUniqueSystemKey(
            connection,
            transaction,
            normalizedResponsibilityName,
            cancellationToken);

        const string sql = """
INSERT INTO app_responsibilities (
    department_id,
    responsibility_key,
    system_key,
    name,
    responsibility_type,
    description,
    is_active
)
VALUES (
    @departmentId,
    @responsibilityKey,
    @systemKey,
    @name,
    'application',
    NULL,
    TRUE
)
RETURNING id;
""";

        int responsibilityId;
        await using (var command = new NpgsqlCommand(sql, connection, transaction))
        {
            var departmentParameter = command.Parameters.Add("departmentId", NpgsqlDbType.Integer);
            departmentParameter.Value = (object?)normalizedDepartmentId ?? DBNull.Value;
            command.Parameters.AddWithValue("responsibilityKey", responsibilityKey);
            command.Parameters.AddWithValue("systemKey", systemKey);
            command.Parameters.AddWithValue("name", normalizedResponsibilityName);

            var scalar = await command.ExecuteScalarAsync(cancellationToken);
            if (scalar is null)
            {
                throw new InvalidOperationException("Responsibility could not be created.");
            }

            responsibilityId = (int)scalar;
        }

        await transaction.CommitAsync(cancellationToken);
        var responsibilities = await LoadAdminResponsibilityOwners(connection, null, responsibilityId, cancellationToken);
        return responsibilities.Single();
    }

    public async Task<bool> DeleteResponsibility(int responsibilityId, CancellationToken cancellationToken = default)
    {
        if (responsibilityId <= 0)
        {
            throw new InvalidOperationException("responsibilityId must be greater than zero.");
        }

        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        if (!await ResponsibilityExists(connection, transaction, responsibilityId, cancellationToken))
        {
            return false;
        }

        await EnsureResponsibilityDeletionAllowed(connection, transaction, responsibilityId, cancellationToken);

        const string sql = """
DELETE FROM app_responsibilities
WHERE id = @responsibilityId;
""";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("responsibilityId", responsibilityId);
        await command.ExecuteNonQueryAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<AdminResponsibilityOwnerDto?> UpdateResponsibilityOwner(
        int responsibilityId,
        long? appUserId,
        int? departmentId,
        CancellationToken cancellationToken = default)
    {
        if (responsibilityId <= 0)
        {
            throw new InvalidOperationException("responsibilityId must be greater than zero.");
        }

        var normalizedUserId = NormalizeNullableUserId(appUserId);
        var normalizedDepartmentId = NormalizeNullableDepartmentId(departmentId);

        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        if (!await ResponsibilityExists(connection, transaction, responsibilityId, cancellationToken))
        {
            return null;
        }

        if (normalizedUserId.HasValue)
        {
            await EnsureActiveUserIdsExist(connection, transaction, new[] { normalizedUserId.Value }, cancellationToken);
        }

        if (normalizedDepartmentId.HasValue && !await DepartmentExists(connection, transaction, normalizedDepartmentId.Value, cancellationToken))
        {
            throw new InvalidOperationException("Selected department is invalid.");
        }

        var personId = normalizedUserId.HasValue
            ? await UpsertPersonRecord(connection, transaction, normalizedUserId.Value, null, cancellationToken)
            : (long?)null;

        var effectiveDepartmentId = normalizedDepartmentId
            ?? await LoadResponsibilityOwningDepartmentId(connection, transaction, responsibilityId, cancellationToken);

        if (!personId.HasValue && !effectiveDepartmentId.HasValue)
        {
            throw new InvalidOperationException("A responsibility must be assigned to a person or department.");
        }

        const string upsertSql = @"
INSERT INTO system_responsibilities (
    system_key,
    app_responsibility_id,
    responsible_person_id,
    responsible_department_id,
    updated_at
)
SELECT
    COALESCE(existing.system_key, r.system_key),
    r.id,
    @personId,
    @departmentId,
    NOW()
FROM app_responsibilities r
LEFT JOIN system_responsibilities existing ON existing.app_responsibility_id = r.id
        WHERE r.id = @responsibilityId
          AND COALESCE(existing.system_key, r.system_key) IS NOT NULL
ON CONFLICT (system_key) DO UPDATE
SET
    app_responsibility_id = EXCLUDED.app_responsibility_id,
    responsible_person_id = EXCLUDED.responsible_person_id,
    responsible_department_id = EXCLUDED.responsible_department_id,
    updated_at = NOW();";

        await using (var upsertCommand = new NpgsqlCommand(upsertSql, connection, transaction))
        {
            upsertCommand.Parameters.AddWithValue("responsibilityId", responsibilityId);
            var personParameter = upsertCommand.Parameters.Add("personId", NpgsqlDbType.Bigint);
            personParameter.Value = (object?)personId ?? DBNull.Value;
            var departmentParameter = upsertCommand.Parameters.Add("departmentId", NpgsqlDbType.Integer);
            departmentParameter.Value = (object?)effectiveDepartmentId ?? DBNull.Value;
            var affectedRows = await upsertCommand.ExecuteNonQueryAsync(cancellationToken);
            if (affectedRows == 0)
            {
                throw new InvalidOperationException("Responsibility is not linked to a system key.");
            }
        }

        await ReassignOpenTasksByResponsibilityAsync(connection, transaction, responsibilityId, normalizedUserId, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        var assignments = await LoadAdminResponsibilityOwners(connection, null, responsibilityId, cancellationToken);
        return assignments.FirstOrDefault();
    }

    private static async Task ReassignOpenTasksByResponsibilityAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int responsibilityId,
        long? newUserId,
        CancellationToken cancellationToken)
    {
        if (newUserId.HasValue)
        {
            const string reassignWorkflowTasksSql = @"
UPDATE task_assignments ta
SET
    assignee_user_id = @newUserId,
    assignee_responsibility_id = NULL,
    assignment_type = 'user'
FROM workflow_tasks wt
JOIN workflows w ON w.id = wt.workflow_id
WHERE ta.workflow_task_id = wt.id
  AND ta.is_primary = TRUE
  AND (
    (ta.assignment_type = 'responsibility' AND ta.assignee_responsibility_id = @responsibilityId)
    OR (
      ta.assignment_type = 'user'
      AND EXISTS (
        SELECT 1 FROM workflow_node_task_specs spec
        WHERE spec.id = wt.workflow_node_task_spec_id
          AND spec.default_responsibility_id = @responsibilityId
      )
    )
  )
  AND wt.status NOT IN ('done', 'cancelled')
  AND w.status NOT IN ('completed', 'cancelled')";

            await using (var cmd = new NpgsqlCommand(reassignWorkflowTasksSql, connection, transaction))
            {
                cmd.Parameters.Add("newUserId", NpgsqlDbType.Bigint).Value = newUserId.Value;
                cmd.Parameters.AddWithValue("responsibilityId", responsibilityId);
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            const string reassignRotationTasksSql = @"
UPDATE rotation_task_assignments rta
SET
    assignee_user_id = @newUserId,
    assignee_responsibility_id = NULL,
    assignment_type = 'user'
FROM rotation_generated_tasks rgt
WHERE rta.rotation_generated_task_id = rgt.id
  AND rta.is_primary = TRUE
  AND rta.assignment_type = 'responsibility'
  AND rta.assignee_responsibility_id = @responsibilityId
  AND rgt.status NOT IN ('completed', 'cancelled', 'failed')";

            await using (var cmd = new NpgsqlCommand(reassignRotationTasksSql, connection, transaction))
            {
                cmd.Parameters.Add("newUserId", NpgsqlDbType.Bigint).Value = newUserId.Value;
                cmd.Parameters.AddWithValue("responsibilityId", responsibilityId);
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }
        }
        else
        {
            // No explicit person — revert user-pinned workflow tasks back to responsibility lookup
            const string revertWorkflowTasksSql = @"
UPDATE task_assignments ta
SET
    assignee_user_id = NULL,
    assignee_responsibility_id = @responsibilityId,
    assignment_type = 'responsibility'
FROM workflow_tasks wt
JOIN workflows w ON w.id = wt.workflow_id
JOIN workflow_node_task_specs spec ON spec.id = wt.workflow_node_task_spec_id
WHERE ta.workflow_task_id = wt.id
  AND ta.is_primary = TRUE
  AND ta.assignment_type = 'user'
  AND spec.default_responsibility_id = @responsibilityId
  AND wt.status NOT IN ('done', 'cancelled')
  AND w.status NOT IN ('completed', 'cancelled')";

            await using var cmd = new NpgsqlCommand(revertWorkflowTasksSql, connection, transaction);
            cmd.Parameters.AddWithValue("responsibilityId", responsibilityId);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task<bool> ResponsibilityExists(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int responsibilityId,
        CancellationToken cancellationToken)
    {
        const string sql = @"
SELECT EXISTS(
    SELECT 1
    FROM app_responsibilities
    WHERE id = @responsibilityId
      AND is_active = TRUE
      AND responsibility_type = 'application'
);";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("responsibilityId", responsibilityId);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false);
    }

    private static async Task EnsureResponsibilityDeletionAllowed(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int responsibilityId,
        CancellationToken cancellationToken)
    {
        const string taskAssignmentSql = """
SELECT EXISTS(
    SELECT 1
    FROM task_assignments
    WHERE assignee_responsibility_id = @responsibilityId
);
""";

        await EnsureNoReferencedRows(
            connection,
            transaction,
            taskAssignmentSql,
            "responsibilityId",
            NpgsqlDbType.Integer,
            responsibilityId,
            "Responsibility cannot be deleted while workflow tasks still reference it.",
            cancellationToken);

        var responsibilityKey = await LoadResponsibilityKey(
            connection,
            transaction,
            responsibilityId,
            cancellationToken);
        if (!string.IsNullOrWhiteSpace(responsibilityKey))
        {
            const string workflowConfigSql = """
SELECT EXISTS(
    SELECT 1
    FROM workflow_node_configs config
    WHERE STRPOS(
        config.config_json::text,
        '"responsibilityKey": "' || @responsibilityKey || '"'
    ) > 0
);
""";

            await EnsureNoReferencedRows(
                connection,
                transaction,
                workflowConfigSql,
                "responsibilityKey",
                NpgsqlDbType.Text,
                responsibilityKey,
                "Responsibility cannot be deleted while workflow definitions still reference it.",
                cancellationToken);
        }
    }

    private static async Task<int?> LoadResponsibilityOwningDepartmentId(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int responsibilityId,
        CancellationToken cancellationToken)
    {
        const string sql = @"
SELECT department_id
FROM app_responsibilities
WHERE id = @responsibilityId
LIMIT 1;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("responsibilityId", responsibilityId);

        var scalar = await command.ExecuteScalarAsync(cancellationToken);
        return scalar is null || scalar is DBNull ? null : (int?)scalar;
    }

    private static async Task<string?> LoadResponsibilityKey(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int responsibilityId,
        CancellationToken cancellationToken)
    {
        const string sql = """
SELECT responsibility_key
FROM app_responsibilities
WHERE id = @responsibilityId
LIMIT 1;
""";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("responsibilityId", responsibilityId);

        var scalar = await command.ExecuteScalarAsync(cancellationToken);
        return scalar is null || scalar is DBNull ? null : (string?)scalar;
    }

    private static async Task<string> GenerateUniqueResponsibilityKey(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string responsibilityName,
        CancellationToken cancellationToken)
    {
        return await GenerateUniqueResponsibilityIdentifier(
            connection,
            transaction,
            responsibilityName,
            "admin",
            "responsibility_key",
            120,
            cancellationToken);
    }

    private static async Task<string> GenerateUniqueSystemKey(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string responsibilityName,
        CancellationToken cancellationToken)
    {
        return await GenerateUniqueResponsibilityIdentifier(
            connection,
            transaction,
            responsibilityName,
            "system",
            "system_key",
            64,
            cancellationToken);
    }

    private static async Task<string> GenerateUniqueResponsibilityIdentifier(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string sourceName,
        string prefix,
        string columnName,
        int maxLength,
        CancellationToken cancellationToken)
    {
        var slug = BuildResponsibilitySlug(sourceName);

        for (var attempt = 0; attempt < 5; attempt += 1)
        {
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var candidate = $"{prefix}_{slug}_{suffix}";
            if (candidate.Length > maxLength)
            {
                candidate = candidate[..maxLength];
            }

            if (!await ResponsibilityIdentifierExists(
                    connection,
                    transaction,
                    columnName,
                    candidate,
                    cancellationToken))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("Responsibility key could not be generated.");
    }

    private static async Task<bool> ResponsibilityIdentifierExists(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string columnName,
        string value,
        CancellationToken cancellationToken)
    {
        var sql = $"""
SELECT EXISTS(
    SELECT 1
    FROM app_responsibilities
    WHERE {columnName} = @value
);
""";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("value", value);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false);
    }

    private static string BuildResponsibilitySlug(string sourceName)
        => BuildAdminSlug(sourceName, "responsibility");
}
