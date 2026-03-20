using Npgsql;

namespace API;

internal sealed partial class PostgresWorkflowRepository
{
    private static async Task EnsureAssignableResponsibilityExists(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int responsibilityId)
    {
        const string sql = @"
SELECT id
FROM app_responsibilities
WHERE id = @responsibilityId
  AND is_active = TRUE
LIMIT 1;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("responsibilityId", responsibilityId);
        var scalar = await command.ExecuteScalarAsync();
        if (scalar is null)
        {
            throw new InvalidOperationException("Assignee responsibility is invalid or inactive.");
        }
    }

    private static async Task EnsureAssignableUserExists(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long userId)
    {
        const string sql = @"
SELECT id
FROM app_users
WHERE id = @userId
  AND is_active = TRUE
LIMIT 1;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("userId", userId);
        var scalar = await command.ExecuteScalarAsync();
        if (scalar is null)
        {
            throw new InvalidOperationException("Assignee user is invalid or inactive.");
        }
    }

    private static async Task EnsureUserHasResponsibility(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long userId,
        int responsibilityId)
    {
        const string sql = @"
SELECT 1
FROM app_user_responsibilities
WHERE app_user_id = @userId
  AND app_responsibility_id = @responsibilityId
LIMIT 1;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("userId", userId);
        command.Parameters.AddWithValue("responsibilityId", responsibilityId);

        var scalar = await command.ExecuteScalarAsync();
        if (scalar is null)
        {
            throw new InvalidOperationException("The selected user is not directly assigned to the selected responsibility.");
        }
    }

    private static async Task<int?> LoadResponsibilityIdByKey(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string responsibilityKey)
    {
        const string sql = @"
SELECT id
FROM app_responsibilities
WHERE responsibility_key = @responsibilityKey
  AND is_active = TRUE
LIMIT 1;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("responsibilityKey", responsibilityKey);

        var scalar = await command.ExecuteScalarAsync();
        return scalar is null ? null : (int?)scalar;
    }

    private static async Task<int?> LoadDepartmentLeadResponsibilityId(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int departmentId)
    {
        const string sql = @"
SELECT id
FROM app_responsibilities
WHERE department_id = @departmentId
  AND responsibility_type = 'department_lead'
  AND is_active = TRUE
ORDER BY id
LIMIT 1;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("departmentId", departmentId);

        var scalar = await command.ExecuteScalarAsync();
        return scalar is null ? null : (int?)scalar;
    }

    private static async Task<long?> LoadExplicitResponsibilityOwnerUserId(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int responsibilityId)
    {
        const string sql = @"
SELECT u.id
FROM system_responsibilities sr
JOIN people p ON p.id = sr.responsible_person_id
JOIN app_users u ON u.id = p.app_user_id
WHERE sr.app_responsibility_id = @responsibilityId
  AND u.is_active = TRUE
LIMIT 1;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("responsibilityId", responsibilityId);

        var scalar = await command.ExecuteScalarAsync();
        return scalar is null ? null : (long?)scalar;
    }

    private static async Task<int?> LoadResponsibilityDepartmentOverrideId(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int responsibilityId)
    {
        const string sql = @"
SELECT COALESCE(sr.responsible_department_id, r.department_id)
FROM app_responsibilities r
LEFT JOIN system_responsibilities sr ON sr.app_responsibility_id = r.id
WHERE r.id = @responsibilityId
LIMIT 1;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("responsibilityId", responsibilityId);

        var scalar = await command.ExecuteScalarAsync();
        return scalar is null || scalar is DBNull ? null : (int?)scalar;
    }

    private static async Task<long?> ResolvePrimaryAssigneeUserId(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int responsibilityId,
        int departmentId)
    {
        var explicitOwnerUserId = await LoadExplicitResponsibilityOwnerUserId(connection, transaction, responsibilityId);
        if (explicitOwnerUserId.HasValue)
        {
            return explicitOwnerUserId.Value;
        }

        var effectiveDepartmentId = await LoadResponsibilityDepartmentOverrideId(connection, transaction, responsibilityId)
            ?? departmentId;

        const string sql = @"
SELECT u.id
FROM app_users u
LEFT JOIN people p ON p.app_user_id = u.id
JOIN app_user_responsibilities ur ON ur.app_user_id = u.id
LEFT JOIN app_responsibilities r ON r.id = ur.app_responsibility_id
WHERE u.is_active = TRUE
  AND ur.app_responsibility_id = @responsibilityId
  AND (
      r.department_id = @effectiveDepartmentId
      OR r.department_id IS NULL
      OR COALESCE(p.department_id, u.department_id) = @effectiveDepartmentId
      OR COALESCE(p.department_id, u.department_id) IS NULL
  )
ORDER BY CASE WHEN COALESCE(p.department_id, u.department_id) = @effectiveDepartmentId THEN 0 ELSE 1 END, u.id
LIMIT 1;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("responsibilityId", responsibilityId);
        command.Parameters.AddWithValue("effectiveDepartmentId", effectiveDepartmentId);

        var scalar = await command.ExecuteScalarAsync();
        return scalar is null ? null : (long?)scalar;
    }

    private static async Task<(long? UserId, int? ResponsibilityId)> ResolveDepartmentRequirementSelectionAssignment(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int departmentId)
    {
        const string sql = @"
SELECT
    COALESCE(requirement_user.id, lead_user.id) AS user_id,
    r.id AS responsibility_id
FROM departments d
LEFT JOIN department_settings ds ON ds.department_id = d.id
LEFT JOIN people requirement_person ON requirement_person.id = ds.requirement_approver_person_id
LEFT JOIN app_users requirement_user ON requirement_user.id = requirement_person.app_user_id AND requirement_user.is_active = TRUE
LEFT JOIN people lead_person ON lead_person.id = ds.department_lead_person_id
LEFT JOIN app_users lead_user ON lead_user.id = lead_person.app_user_id AND lead_user.is_active = TRUE
LEFT JOIN app_responsibilities r
    ON r.department_id = d.id
   AND r.responsibility_type = 'department_lead'
   AND r.is_active = TRUE
WHERE d.id = @departmentId
LIMIT 1;";

        await using (var command = new NpgsqlCommand(sql, connection, transaction))
        {
            command.Parameters.AddWithValue("departmentId", departmentId);
            await using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                long? userId = reader.IsDBNull(0) ? null : reader.GetInt64(0);
                int? responsibilityId = reader.IsDBNull(1) ? null : reader.GetInt32(1);
                if (userId.HasValue || responsibilityId.HasValue)
                {
                    return (userId, responsibilityId);
                }
            }
        }

        var fallbackResponsibilityId = await LoadDepartmentLeadResponsibilityId(connection, transaction, departmentId);
        if (!fallbackResponsibilityId.HasValue)
        {
            return (null, null);
        }

        var fallbackUserId = await ResolvePrimaryAssigneeUserId(
            connection,
            transaction,
            fallbackResponsibilityId.Value,
            departmentId);

        return (fallbackUserId, fallbackResponsibilityId.Value);
    }

    private static string? ResolveTaskProcessArea(
        string? processAreaLabel,
        string? responsibilityDepartmentName,
        string? responsibilityType)
    {
        if (!string.IsNullOrWhiteSpace(processAreaLabel))
        {
            return processAreaLabel.Trim();
        }

        if (string.Equals(responsibilityType, "department_lead", StringComparison.OrdinalIgnoreCase))
        {
            return "Abteilungsleitung";
        }

        if (string.IsNullOrWhiteSpace(responsibilityDepartmentName))
        {
            return null;
        }

        return responsibilityDepartmentName.Trim();
    }
}
