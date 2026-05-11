using Npgsql;
using NpgsqlTypes;

namespace API;

internal sealed partial class PostgresUserAuthorizationRepository
{
    private static async Task<AdminListPageDto<AdminResponsibilityOwnerDto>> LoadAdminResponsibilityOwnersPage(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        AdminListQuery query,
        CancellationToken cancellationToken)
    {
        var orderBy = query.Sort?.Trim().ToLowerInvariant() switch
        {
            "name" => "r.name ASC, r.id ASC",
            "name_desc" => "r.name DESC, r.id DESC",
            "department" => "COALESCE(configured_department.name, owning_department.name, '') ASC, r.name ASC, r.id ASC",
            "department_desc" => "COALESCE(configured_department.name, owning_department.name, '') DESC, r.name DESC, r.id DESC",
            "id" => "r.id ASC",
            "id_desc" => "r.id DESC",
            _ => @"
    CASE r.responsibility_type
        WHEN 'process' THEN 0
        ELSE 1
    END,
    COALESCE(configured_department.name, owning_department.name, ''),
    r.name,
    r.id"
        };

        var sql = $@"
SELECT
    r.id,
    r.responsibility_key,
    CASE
        WHEN r.responsibility_type = 'application'
         AND COALESCE(configured_department.name, owning_department.name) IS NOT NULL
         AND POSITION(COALESCE(configured_department.name, owning_department.name) || ' - ' IN r.name) = 1
            THEN SUBSTRING(r.name FROM LENGTH(COALESCE(configured_department.name, owning_department.name)) + 4)
        ELSE r.name
    END,
    r.responsibility_type,
    r.system_key,
    COALESCE(sr.responsible_department_id, r.department_id),
    COALESCE(configured_department.name, owning_department.name),
    u.id,
    u.display_name,
    sr.updated_at,
    COUNT(*) OVER() AS total_count
FROM app_responsibilities r
LEFT JOIN system_responsibilities sr ON sr.app_responsibility_id = r.id
LEFT JOIN departments configured_department ON configured_department.id = sr.responsible_department_id
LEFT JOIN departments owning_department ON owning_department.id = r.department_id
LEFT JOIN people p ON p.id = sr.responsible_person_id
LEFT JOIN app_users u ON u.id = p.app_user_id
WHERE r.is_active = TRUE
  AND r.system_key IS NOT NULL
  AND (
        @search = ''
        OR r.name ILIKE @pattern
        OR COALESCE(configured_department.name, owning_department.name, '') ILIKE @pattern
        OR COALESCE(r.system_key, '') ILIKE @pattern
        OR COALESCE(u.display_name, '') ILIKE @pattern
  )
ORDER BY {orderBy}
LIMIT @limit OFFSET @offset;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("search", query.NormalizedSearch);
        command.Parameters.AddWithValue("pattern", query.SearchPattern);
        command.Parameters.AddWithValue("limit", query.Limit);
        command.Parameters.AddWithValue("offset", query.Offset);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var assignments = new List<AdminResponsibilityOwnerDto>();
        var total = 0;
        while (await reader.ReadAsync(cancellationToken))
        {
            assignments.Add(new AdminResponsibilityOwnerDto
            {
                ResponsibilityId = reader.GetInt32(0),
                ResponsibilityKey = reader.GetString(1),
                ResponsibilityName = reader.GetString(2),
                ResponsibilityType = reader.GetString(3),
                SystemKey = reader.IsDBNull(4) ? null : reader.GetString(4),
                DepartmentId = reader.IsDBNull(5) ? null : reader.GetInt32(5),
                DepartmentName = reader.IsDBNull(6) ? null : reader.GetString(6),
                AppUserId = reader.IsDBNull(7) ? null : reader.GetInt64(7),
                AppUserDisplayName = reader.IsDBNull(8) ? null : reader.GetString(8),
                UpdatedAt = reader.IsDBNull(9) ? null : reader.GetDateTime(9)
            });
            total = reader.GetInt32(10);
        }

        return new AdminListPageDto<AdminResponsibilityOwnerDto>
        {
            Items = assignments,
            Total = total,
            Limit = query.Limit,
            Offset = query.Offset
        };
    }

    private static async Task<List<AdminResponsibilityOwnerDto>> LoadAdminResponsibilityOwners(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        int? responsibilityId,
        CancellationToken cancellationToken)
    {
        const string sql = @"
SELECT
    r.id,
    r.responsibility_key,
    CASE
        WHEN r.responsibility_type = 'application'
         AND COALESCE(configured_department.name, owning_department.name) IS NOT NULL
         AND POSITION(COALESCE(configured_department.name, owning_department.name) || ' - ' IN r.name) = 1
            THEN SUBSTRING(r.name FROM LENGTH(COALESCE(configured_department.name, owning_department.name)) + 4)
        ELSE r.name
    END,
    r.responsibility_type,
    r.system_key,
    COALESCE(sr.responsible_department_id, r.department_id),
    COALESCE(configured_department.name, owning_department.name),
    u.id,
    u.display_name,
    sr.updated_at
FROM app_responsibilities r
LEFT JOIN system_responsibilities sr ON sr.app_responsibility_id = r.id
LEFT JOIN departments configured_department ON configured_department.id = sr.responsible_department_id
LEFT JOIN departments owning_department ON owning_department.id = r.department_id
LEFT JOIN people p ON p.id = sr.responsible_person_id
LEFT JOIN app_users u ON u.id = p.app_user_id
WHERE r.is_active = TRUE
  AND r.system_key IS NOT NULL
  AND (@responsibilityId IS NULL OR r.id = @responsibilityId)
ORDER BY
    CASE r.responsibility_type
        WHEN 'process' THEN 0
        ELSE 1
    END,
    COALESCE(configured_department.name, owning_department.name, ''),
    r.name,
    r.id;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        var responsibilityIdParameter = command.Parameters.Add("responsibilityId", NpgsqlDbType.Integer);
        responsibilityIdParameter.Value = (object?)responsibilityId ?? DBNull.Value;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var assignments = new List<AdminResponsibilityOwnerDto>();
        while (await reader.ReadAsync(cancellationToken))
        {
            assignments.Add(new AdminResponsibilityOwnerDto
            {
                ResponsibilityId = reader.GetInt32(0),
                ResponsibilityKey = reader.GetString(1),
                ResponsibilityName = reader.GetString(2),
                ResponsibilityType = reader.GetString(3),
                SystemKey = reader.IsDBNull(4) ? null : reader.GetString(4),
                DepartmentId = reader.IsDBNull(5) ? null : reader.GetInt32(5),
                DepartmentName = reader.IsDBNull(6) ? null : reader.GetString(6),
                AppUserId = reader.IsDBNull(7) ? null : reader.GetInt64(7),
                AppUserDisplayName = reader.IsDBNull(8) ? null : reader.GetString(8),
                UpdatedAt = reader.IsDBNull(9) ? null : reader.GetDateTime(9)
            });
        }

        return assignments;
    }
}
