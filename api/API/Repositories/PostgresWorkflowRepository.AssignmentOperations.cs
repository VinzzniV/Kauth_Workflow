using Npgsql;

namespace API;

internal sealed partial class PostgresWorkflowRepository
{
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

        var fallbackUserId = await PostgresRepositorySharedHelpers.ResolvePrimaryAssigneeUserId(
            connection,
            transaction,
            fallbackResponsibilityId.Value,
            departmentId);

        return (fallbackUserId, fallbackResponsibilityId.Value);
    }

    private static string? ResolveTaskProcessArea(
        string? processAreaLabel,
        string? responsibilityDepartmentName,
        string? responsibilityType,
        string? responsibilityKey = null,
        string? responsibilityName = null)
    {
        if (!string.IsNullOrWhiteSpace(processAreaLabel))
        {
            return processAreaLabel.Trim();
        }

        if (string.Equals(responsibilityType, "department_lead", StringComparison.OrdinalIgnoreCase))
        {
            return "Abteilungsleitung";
        }

        if (!string.IsNullOrWhiteSpace(responsibilityDepartmentName))
        {
            return responsibilityDepartmentName.Trim();
        }

        var areaFromResponsibilityKey = ResolveTaskProcessAreaFromResponsibilityKey(responsibilityKey);
        if (!string.IsNullOrWhiteSpace(areaFromResponsibilityKey))
        {
            return areaFromResponsibilityKey;
        }

        return string.IsNullOrWhiteSpace(responsibilityName)
            ? null
            : responsibilityName.Trim();
    }

    private static string? ResolveTaskProcessAreaFromResponsibilityKey(string? responsibilityKey)
    {
        if (string.IsNullOrWhiteSpace(responsibilityKey))
        {
            return null;
        }

        var normalizedKey = responsibilityKey.Trim().ToLowerInvariant();
        if (normalizedKey.StartsWith("it_", StringComparison.Ordinal))
        {
            return "IT";
        }

        if (normalizedKey.StartsWith("qs_", StringComparison.Ordinal))
        {
            return "QS";
        }

        if (normalizedKey.StartsWith("av_", StringComparison.Ordinal))
        {
            return "AV";
        }

        if (normalizedKey.StartsWith("qmb_", StringComparison.Ordinal))
        {
            return "QMB";
        }

        if (normalizedKey.StartsWith("hr_", StringComparison.Ordinal))
        {
            return "HR";
        }

        return null;
    }
}
