using Npgsql;
using NpgsqlTypes;

namespace API;

internal sealed partial class PostgresUserAuthorizationRepository
{
    private static async Task<AdminListPageDto<AdminRoleDto>> LoadAdminPositionRolesPage(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        AdminListQuery query,
        CancellationToken cancellationToken)
    {
        var orderBy = query.Sort?.Trim().ToLowerInvariant() switch
        {
            "name" => "r.name ASC, r.id ASC",
            "name_desc" => "r.name DESC, r.id DESC",
            "department" => "d.name ASC, r.name ASC, r.id ASC",
            "department_desc" => "d.name DESC, r.name DESC, r.id DESC",
            "id" => "r.id ASC",
            "id_desc" => "r.id DESC",
            _ => "d.name ASC, r.name ASC, r.id ASC"
        };

        var sql = $@"
SELECT
    r.id,
    r.role_key,
    r.name,
    r.role_kind,
    d.id,
    d.name,
    r.is_active,
    COUNT(*) OVER() AS total_count
FROM app_roles r
JOIN departments d ON d.id = r.department_id
WHERE r.role_kind = 'position'
  AND (@search = '' OR r.name ILIKE @pattern OR d.name ILIKE @pattern)
ORDER BY {orderBy}
LIMIT @limit OFFSET @offset;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("search", query.NormalizedSearch);
        command.Parameters.AddWithValue("pattern", query.SearchPattern);
        command.Parameters.AddWithValue("limit", query.Limit);
        command.Parameters.AddWithValue("offset", query.Offset);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var positions = new List<AdminRoleDto>();
        var total = 0;
        while (await reader.ReadAsync(cancellationToken))
        {
            positions.Add(new AdminRoleDto
            {
                RoleId = reader.GetInt32(0),
                RoleKey = reader.GetString(1),
                RoleName = reader.GetString(2),
                RoleKind = reader.GetString(3),
                DepartmentId = reader.GetInt32(4),
                DepartmentName = reader.GetString(5),
                Scope = "department",
                ScopeDepartmentId = reader.GetInt32(4),
                ScopeDepartmentName = reader.GetString(5),
                IsActive = reader.GetBoolean(6),
                Permissions = []
            });
            total = reader.GetInt32(7);
        }

        return new AdminListPageDto<AdminRoleDto>
        {
            Items = positions,
            Total = total,
            Limit = query.Limit,
            Offset = query.Offset
        };
    }

    private static async Task<List<AdminRoleDto>> LoadAdminPositionRoles(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        int? positionId,
        CancellationToken cancellationToken)
    {
        const string sql = $@"
SELECT
    r.id,
    r.role_key,
    r.name,
    r.role_kind,
    d.id,
    d.name,
    r.is_active
FROM app_roles r
JOIN departments d ON d.id = r.department_id
WHERE r.role_kind = 'position'
  AND (@positionId IS NULL OR r.id = @positionId)
ORDER BY d.name, r.name, r.id;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        var positionIdParameter = command.Parameters.Add("positionId", NpgsqlDbType.Integer);
        positionIdParameter.Value = (object?)positionId ?? DBNull.Value;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var positions = new List<AdminRoleDto>();
        while (await reader.ReadAsync(cancellationToken))
        {
            positions.Add(new AdminRoleDto
            {
                RoleId = reader.GetInt32(0),
                RoleKey = reader.GetString(1),
                RoleName = reader.GetString(2),
                RoleKind = reader.GetString(3),
                DepartmentId = reader.GetInt32(4),
                DepartmentName = reader.GetString(5),
                Scope = "department",
                ScopeDepartmentId = reader.GetInt32(4),
                ScopeDepartmentName = reader.GetString(5),
                IsActive = reader.GetBoolean(6),
                Permissions = []
            });
        }

        return positions;
    }
}
