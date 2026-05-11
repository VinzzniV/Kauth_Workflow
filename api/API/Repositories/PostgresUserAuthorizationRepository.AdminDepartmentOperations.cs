using Npgsql;
using NpgsqlTypes;

namespace API;

internal sealed partial class PostgresUserAuthorizationRepository
{
    public async Task<AdminDepartmentAssignmentDto> CreateDepartment(
        string departmentName,
        CancellationToken cancellationToken = default)
    {
        var normalizedDepartmentName = NormalizeRequired(departmentName, "Department name is required.");

        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await EnsureDepartmentNameAvailable(connection, transaction, normalizedDepartmentName, null, cancellationToken);

        const string sql = @"
INSERT INTO departments (name)
VALUES (@name)
RETURNING id;";

        int departmentId;
        await using (var command = new NpgsqlCommand(sql, connection, transaction))
        {
            command.Parameters.AddWithValue("name", normalizedDepartmentName);
            var scalar = await command.ExecuteScalarAsync(cancellationToken);
            if (scalar is null)
            {
                throw new InvalidOperationException("Department could not be created.");
            }

            departmentId = (int)scalar;
        }

        await transaction.CommitAsync(cancellationToken);
        var departments = await LoadAdminDepartmentAssignments(connection, null, departmentId, cancellationToken);
        return departments.Single();
    }

    public async Task<bool> DeleteDepartment(int departmentId, CancellationToken cancellationToken = default)
    {
        if (departmentId <= 0)
        {
            throw new InvalidOperationException("departmentId must be greater than zero.");
        }

        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        if (!await DepartmentExists(connection, transaction, departmentId, cancellationToken))
        {
            return false;
        }

        await EnsureDepartmentDeletionAllowed(connection, transaction, departmentId, cancellationToken);

        const string sql = @"
DELETE FROM departments
WHERE id = @departmentId;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("departmentId", departmentId);
        await command.ExecuteNonQueryAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<AdminDepartmentAssignmentDto?> UpdateDepartmentAssignment(
        int departmentId,
        long? departmentLeadUserId,
        long? requirementOwnerUserId,
        CancellationToken cancellationToken = default)
    {
        if (departmentId <= 0)
        {
            throw new InvalidOperationException("departmentId must be greater than zero.");
        }

        var normalizedDepartmentLeadUserId = NormalizeNullableUserId(departmentLeadUserId);
        var normalizedRequirementOwnerUserId = NormalizeNullableUserId(requirementOwnerUserId);
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        if (!await DepartmentExists(connection, transaction, departmentId, cancellationToken))
        {
            return null;
        }

        var selectedUserIds = new[] { normalizedDepartmentLeadUserId, normalizedRequirementOwnerUserId }
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToArray();

        await EnsureActiveUserIdsExist(
            connection,
            transaction,
            selectedUserIds,
            cancellationToken);

        if (normalizedDepartmentLeadUserId.HasValue)
        {
            await EnsureUsersCanAccessSupervisorStep(
                connection,
                transaction,
                [normalizedDepartmentLeadUserId.Value],
                cancellationToken);
        }

        var personIdsByUserId = new Dictionary<long, long>();
        foreach (var userId in selectedUserIds)
        {
            personIdsByUserId[userId] = await UpsertPersonRecord(connection, transaction, userId, null, cancellationToken);
        }

        var normalizedDepartmentLeadPersonId = normalizedDepartmentLeadUserId.HasValue
            ? personIdsByUserId[normalizedDepartmentLeadUserId.Value]
            : (long?)null;
        var normalizedRequirementOwnerPersonId = normalizedRequirementOwnerUserId.HasValue
            ? personIdsByUserId[normalizedRequirementOwnerUserId.Value]
            : (long?)null;

        if (normalizedDepartmentLeadUserId is null && normalizedRequirementOwnerUserId is null)
        {
            const string deleteSql = @"
DELETE FROM department_settings
WHERE department_id = @departmentId;";

            await using var deleteCommand = new NpgsqlCommand(deleteSql, connection, transaction);
            deleteCommand.Parameters.AddWithValue("departmentId", departmentId);
            await deleteCommand.ExecuteNonQueryAsync(cancellationToken);
        }
        else
        {
            const string upsertSql = @"
INSERT INTO department_settings (
    department_id,
    department_lead_person_id,
    requirement_approver_person_id,
    updated_at
)
VALUES (
    @departmentId,
    @departmentLeadPersonId,
    @requirementOwnerPersonId,
    NOW()
)
ON CONFLICT (department_id) DO UPDATE
SET department_lead_person_id = EXCLUDED.department_lead_person_id,
    requirement_approver_person_id = EXCLUDED.requirement_approver_person_id,
    updated_at = NOW();";

            await using var upsertCommand = new NpgsqlCommand(upsertSql, connection, transaction);
            upsertCommand.Parameters.AddWithValue("departmentId", departmentId);
            var leadParameter = upsertCommand.Parameters.Add("departmentLeadPersonId", NpgsqlDbType.Bigint);
            leadParameter.Value = (object?)normalizedDepartmentLeadPersonId ?? DBNull.Value;
            var requirementParameter = upsertCommand.Parameters.Add("requirementOwnerPersonId", NpgsqlDbType.Bigint);
            requirementParameter.Value = (object?)normalizedRequirementOwnerPersonId ?? DBNull.Value;
            await upsertCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        var assignments = await LoadAdminDepartmentAssignments(connection, null, departmentId, cancellationToken);
        return assignments.FirstOrDefault();
    }

    private static async Task<bool> DepartmentExists(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int departmentId,
        CancellationToken cancellationToken)
    {
        const string sql = "SELECT EXISTS(SELECT 1 FROM departments WHERE id = @departmentId);";
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("departmentId", departmentId);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false);
    }

    private static async Task EnsureDepartmentNameAvailable(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string departmentName,
        int? excludeDepartmentId,
        CancellationToken cancellationToken)
    {
        const string sql = @"
SELECT EXISTS(
    SELECT 1
    FROM departments
    WHERE LOWER(name) = LOWER(@departmentName)
      AND (@excludeDepartmentId IS NULL OR id <> @excludeDepartmentId)
);";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("departmentName", departmentName);
        var excludeDepartmentIdParameter = command.Parameters.Add("excludeDepartmentId", NpgsqlDbType.Integer);
        excludeDepartmentIdParameter.Value = (object?)excludeDepartmentId ?? DBNull.Value;

        var exists = (bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false);
        if (exists)
        {
            throw new InvalidOperationException("A department with this name already exists.");
        }
    }

    private static async Task EnsureDepartmentDeletionAllowed(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int departmentId,
        CancellationToken cancellationToken)
    {
        const string workflowSql = @"
SELECT EXISTS(
    SELECT 1
    FROM workflows
    WHERE department_id = @departmentId
);";

        await EnsureNoReferencedRows(
            connection,
            transaction,
            workflowSql,
            "departmentId",
            NpgsqlDbType.Integer,
            departmentId,
            "Department cannot be deleted while workflows still reference it.",
            cancellationToken);

        const string departmentSettingsSql = @"
SELECT EXISTS(
    SELECT 1
    FROM department_settings
    WHERE department_id = @departmentId
);";

        await EnsureNoReferencedRows(
            connection,
            transaction,
            departmentSettingsSql,
            "departmentId",
            NpgsqlDbType.Integer,
            departmentId,
            "Department cannot be deleted while department ownership settings still exist.",
            cancellationToken);

        const string userSql = @"
SELECT EXISTS(
    SELECT 1
    FROM app_users
    WHERE department_id = @departmentId
);";

        await EnsureNoReferencedRows(
            connection,
            transaction,
            userSql,
            "departmentId",
            NpgsqlDbType.Integer,
            departmentId,
            "Department cannot be deleted while users are still assigned to it.",
            cancellationToken);

        const string peopleSql = @"
SELECT EXISTS(
    SELECT 1
    FROM people
    WHERE department_id = @departmentId
);";

        await EnsureNoReferencedRows(
            connection,
            transaction,
            peopleSql,
            "departmentId",
            NpgsqlDbType.Integer,
            departmentId,
            "Department cannot be deleted while person records are still assigned to it.",
            cancellationToken);

        const string roleSql = @"
SELECT EXISTS(
    SELECT 1
    FROM app_roles
    WHERE department_id = @departmentId
);";

        await EnsureNoReferencedRows(
            connection,
            transaction,
            roleSql,
            "departmentId",
            NpgsqlDbType.Integer,
            departmentId,
            "Department cannot be deleted while roles still belong to it.",
            cancellationToken);

        const string responsibilitySql = @"
SELECT EXISTS(
    SELECT 1
    FROM app_responsibilities
    WHERE department_id = @departmentId
);";

        await EnsureNoReferencedRows(
            connection,
            transaction,
            responsibilitySql,
            "departmentId",
            NpgsqlDbType.Integer,
            departmentId,
            "Department cannot be deleted while responsibilities still belong to it.",
            cancellationToken);

        const string systemOwnerSql = @"
SELECT EXISTS(
    SELECT 1
    FROM system_responsibilities
    WHERE responsible_department_id = @departmentId
);";

        await EnsureNoReferencedRows(
            connection,
            transaction,
            systemOwnerSql,
            "departmentId",
            NpgsqlDbType.Integer,
            departmentId,
            "Department cannot be deleted while it is assigned as an owner for one or more systems.",
            cancellationToken);

        // LA5: workflow_node_task_specs hat keine owning_department_id-Spalte mehr
        // (Inventur: 0 Eintraege belegt, Spalte gestrichen). Department-Delete kollidiert
        // also nicht mehr mit Task-Specs.
    }

    private static async Task EnsureNoReferencedRows(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string sql,
        string parameterName,
        NpgsqlDbType parameterType,
        object parameterValue,
        string errorMessage,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.Add(parameterName, parameterType).Value = parameterValue;

        var exists = (bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false);
        if (exists)
        {
            throw new InvalidOperationException(errorMessage);
        }
    }
}
