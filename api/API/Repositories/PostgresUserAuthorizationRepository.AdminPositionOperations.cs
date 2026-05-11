using Npgsql;
using NpgsqlTypes;
using System.Globalization;
using System.Text;

namespace API;

internal sealed partial class PostgresUserAuthorizationRepository
{
    public async Task<AdminRoleDto> CreateDepartmentPosition(
        int departmentId,
        string positionName,
        CancellationToken cancellationToken = default)
    {
        if (departmentId <= 0)
        {
            throw new InvalidOperationException("departmentId must be greater than zero.");
        }

        var normalizedPositionName = NormalizeRequired(positionName, "Position name is required.");

        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        if (!await DepartmentExists(connection, transaction, departmentId, cancellationToken))
        {
            throw new InvalidOperationException("Selected department is invalid.");
        }

        await EnsurePositionNameAvailable(connection, transaction, departmentId, normalizedPositionName, null, cancellationToken);
        var roleKey = await GenerateUniquePositionRoleKey(connection, transaction, departmentId, normalizedPositionName, cancellationToken);

        const string sql = """
INSERT INTO app_roles (department_id, role_key, name, role_kind, is_active)
VALUES (@departmentId, @roleKey, @name, 'position', TRUE)
RETURNING id;
""";

        int positionId;
        await using (var command = new NpgsqlCommand(sql, connection, transaction))
        {
            command.Parameters.AddWithValue("departmentId", departmentId);
            command.Parameters.AddWithValue("roleKey", roleKey);
            command.Parameters.AddWithValue("name", normalizedPositionName);
            var scalar = await command.ExecuteScalarAsync(cancellationToken);
            if (scalar is null)
            {
                throw new InvalidOperationException("Position could not be created.");
            }

            positionId = (int)scalar;
        }

        await transaction.CommitAsync(cancellationToken);
        var positions = await LoadAdminPositionRoles(connection, null, positionId, cancellationToken);
        return positions.Single();
    }

    public async Task<AdminRoleDto?> UpdateDepartmentPosition(
        int positionId,
        string positionName,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        if (positionId <= 0)
        {
            throw new InvalidOperationException("positionId must be greater than zero.");
        }

        var normalizedPositionName = NormalizeRequired(positionName, "Position name is required.");

        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var departmentId = await LoadPositionDepartmentId(connection, transaction, positionId, cancellationToken);
        if (departmentId is null)
        {
            return null;
        }

        await EnsurePositionNameAvailable(connection, transaction, departmentId.Value, normalizedPositionName, positionId, cancellationToken);

        const string sql = """
UPDATE app_roles
SET name = @name,
    is_active = @isActive
WHERE id = @positionId
  AND role_kind = 'position';
""";

        await using (var command = new NpgsqlCommand(sql, connection, transaction))
        {
            command.Parameters.AddWithValue("positionId", positionId);
            command.Parameters.AddWithValue("name", normalizedPositionName);
            command.Parameters.AddWithValue("isActive", isActive);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        var positions = await LoadAdminPositionRoles(connection, null, positionId, cancellationToken);
        return positions.FirstOrDefault();
    }

    public async Task<bool> DeleteDepartmentPosition(int positionId, CancellationToken cancellationToken = default)
    {
        if (positionId <= 0)
        {
            throw new InvalidOperationException("positionId must be greater than zero.");
        }

        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        if (await LoadPositionDepartmentId(connection, transaction, positionId, cancellationToken) is null)
        {
            return false;
        }

        await EnsurePositionDeletionAllowed(connection, transaction, positionId, cancellationToken);

        const string sql = """
DELETE FROM app_roles
WHERE id = @positionId
  AND role_kind = 'position';
""";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("positionId", positionId);
        await command.ExecuteNonQueryAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<List<EntraJobTitleDto>> GetDepartmentEntraJobTitles(
        int departmentId,
        CancellationToken cancellationToken = default)
    {
        if (departmentId <= 0)
        {
            return [];
        }

        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync(cancellationToken);

        const string sql = """
SELECT DISTINCT di.job_title
FROM directory_identities di
JOIN departments d ON d.name ILIKE di.department_name
WHERE d.id = @departmentId
  AND di.job_title IS NOT NULL
  AND di.job_title <> ''
ORDER BY di.job_title;
""";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("departmentId", departmentId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var result = new List<EntraJobTitleDto>();
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new EntraJobTitleDto { JobTitle = reader.GetString(0) });
        }

        return result;
    }

    public async Task<ImportPositionsFromEntraResult> ImportDepartmentPositionsFromEntra(
        int departmentId,
        IReadOnlyList<string> jobTitles,
        CancellationToken cancellationToken = default)
    {
        if (departmentId <= 0)
        {
            throw new InvalidOperationException("departmentId must be greater than zero.");
        }

        var normalized = jobTitles
            .Select(t => t.Trim())
            .Where(t => t.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalized.Count == 0)
        {
            return new ImportPositionsFromEntraResult { Created = 0, Skipped = 0 };
        }

        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        if (!await DepartmentExists(connection, transaction, departmentId, cancellationToken))
        {
            throw new InvalidOperationException("Selected department is invalid.");
        }

        const string existingSql = """
SELECT name FROM app_roles
WHERE department_id = @departmentId
  AND role_kind = 'position';
""";

        var existingNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using (var cmd = new NpgsqlCommand(existingSql, connection, transaction))
        {
            cmd.Parameters.AddWithValue("departmentId", departmentId);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                existingNames.Add(reader.GetString(0));
            }
        }

        var created = 0;
        var skipped = 0;

        foreach (var title in normalized)
        {
            if (existingNames.Contains(title))
            {
                skipped++;
                continue;
            }

            var roleKey = await GenerateUniquePositionRoleKey(connection, transaction, departmentId, title, cancellationToken);

            const string insertSql = """
INSERT INTO app_roles (department_id, role_key, name, role_kind, is_active)
VALUES (@departmentId, @roleKey, @name, 'position', TRUE);
""";
            await using var insertCmd = new NpgsqlCommand(insertSql, connection, transaction);
            insertCmd.Parameters.AddWithValue("departmentId", departmentId);
            insertCmd.Parameters.AddWithValue("roleKey", roleKey);
            insertCmd.Parameters.AddWithValue("name", title);
            await insertCmd.ExecuteNonQueryAsync(cancellationToken);

            existingNames.Add(title);
            created++;
        }

        await transaction.CommitAsync(cancellationToken);
        return new ImportPositionsFromEntraResult { Created = created, Skipped = skipped };
    }

    private static async Task<int?> LoadPositionDepartmentId(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int positionId,
        CancellationToken cancellationToken)
    {
        const string sql = """
SELECT department_id
FROM app_roles
WHERE id = @positionId
  AND role_kind = 'position';
""";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("positionId", positionId);
        var scalar = await command.ExecuteScalarAsync(cancellationToken);
        return scalar is null || scalar is DBNull ? null : Convert.ToInt32(scalar);
    }

    private static async Task EnsurePositionNameAvailable(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int departmentId,
        string positionName,
        int? excludePositionId,
        CancellationToken cancellationToken)
    {
        const string sql = """
SELECT EXISTS(
    SELECT 1
    FROM app_roles
    WHERE department_id = @departmentId
      AND role_kind = 'position'
      AND LOWER(name) = LOWER(@positionName)
      AND (@excludePositionId IS NULL OR id <> @excludePositionId)
);
""";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("departmentId", departmentId);
        command.Parameters.AddWithValue("positionName", positionName);
        var excludePositionIdParameter = command.Parameters.Add("excludePositionId", NpgsqlDbType.Integer);
        excludePositionIdParameter.Value = (object?)excludePositionId ?? DBNull.Value;

        var exists = (bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false);
        if (exists)
        {
            throw new InvalidOperationException("A position with this name already exists in this department.");
        }
    }

    private static async Task EnsurePositionDeletionAllowed(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int positionId,
        CancellationToken cancellationToken)
    {
        const string workflowSql = """
SELECT EXISTS(
    SELECT 1
    FROM workflows
    WHERE position_role_id = @positionId
);
""";

        await EnsureNoReferencedRows(
            connection,
            transaction,
            workflowSql,
            "positionId",
            NpgsqlDbType.Integer,
            positionId,
            "Position cannot be deleted while workflows still reference it. Deactivate it instead.",
            cancellationToken);
    }

    private static async Task<string> GenerateUniquePositionRoleKey(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int departmentId,
        string positionName,
        CancellationToken cancellationToken)
    {
        var departmentSlug = await LoadDepartmentSlug(connection, transaction, departmentId, cancellationToken);
        var positionSlug = BuildAdminSlug(positionName, "position");

        for (var attempt = 0; attempt < 5; attempt += 1)
        {
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var candidate = $"position_{departmentSlug}_{positionSlug}_{suffix}";
            if (candidate.Length > 120)
            {
                candidate = candidate[..120].TrimEnd('_');
            }

            if (!await PositionRoleKeyExists(connection, transaction, candidate, cancellationToken))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("Position key could not be generated.");
    }

    private static async Task<string> LoadDepartmentSlug(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int departmentId,
        CancellationToken cancellationToken)
    {
        const string sql = "SELECT name FROM departments WHERE id = @departmentId;";
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("departmentId", departmentId);
        var scalar = await command.ExecuteScalarAsync(cancellationToken);
        return BuildAdminSlug(Convert.ToString(scalar, CultureInfo.InvariantCulture) ?? "", "department");
    }

    private static async Task<bool> PositionRoleKeyExists(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string roleKey,
        CancellationToken cancellationToken)
    {
        const string sql = """
SELECT EXISTS(
    SELECT 1
    FROM app_roles
    WHERE role_key = @roleKey
);
""";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("roleKey", roleKey);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false);
    }

    private static string BuildAdminSlug(string sourceName, string fallback)
    {
        var normalized = sourceName.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();
        var previousWasSeparator = false;

        foreach (var character in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (character <= sbyte.MaxValue && char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
                previousWasSeparator = false;
                continue;
            }

            if (builder.Length == 0 || previousWasSeparator)
            {
                continue;
            }

            builder.Append('_');
            previousWasSeparator = true;
        }

        var slug = builder.ToString().Trim('_');
        if (string.IsNullOrWhiteSpace(slug))
        {
            return fallback;
        }

        return slug.Length <= 40 ? slug : slug[..40].TrimEnd('_');
    }
}
