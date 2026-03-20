using Npgsql;
using NpgsqlTypes;

namespace API;

internal sealed partial class PostgresUserAuthorizationRepository
{
    private static async Task<List<AdminRoleDto>> LoadAdminRoles(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        CancellationToken cancellationToken)
    {
        var sql = $@"
SELECT
    r.id,
    r.role_key,
    r.name,
    r.role_kind,
    r.department_id,
    d.name,
    r.is_active
FROM app_roles r
LEFT JOIN departments d ON d.id = r.department_id
WHERE r.role_kind = '{AuthorizationRoles.SystemRoleKind}'
ORDER BY r.role_kind, d.name, r.name, r.id;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var roles = new List<AdminRoleDto>();
        while (await reader.ReadAsync(cancellationToken))
        {
            roles.Add(new AdminRoleDto
            {
                RoleId = reader.GetInt32(0),
                RoleKey = reader.GetString(1),
                RoleName = reader.GetString(2),
                RoleKind = reader.GetString(3),
                DepartmentId = reader.IsDBNull(4) ? null : reader.GetInt32(4),
                DepartmentName = reader.IsDBNull(5) ? null : reader.GetString(5),
                IsActive = reader.GetBoolean(6)
            });
        }

        return roles;
    }

    private static async Task<List<AdminGroupDto>> LoadAdminGroups(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        int? groupId,
        CancellationToken cancellationToken)
    {
        const string baseSql = @"
SELECT
    g.id,
    g.group_key,
    g.name,
    g.description,
    g.is_active
FROM app_groups g
WHERE (@groupId IS NULL OR g.id = @groupId)
ORDER BY g.name, g.id;";

        var groups = new List<AdminGroupDto>();
        var groupById = new Dictionary<int, AdminGroupDto>();

        await using (var baseCommand = new NpgsqlCommand(baseSql, connection, transaction))
        {
            var baseGroupIdParameter = baseCommand.Parameters.Add("groupId", NpgsqlDbType.Integer);
            baseGroupIdParameter.Value = (object?)groupId ?? DBNull.Value;
            await using var baseReader = await baseCommand.ExecuteReaderAsync(cancellationToken);

            while (await baseReader.ReadAsync(cancellationToken))
            {
                var group = new AdminGroupDto
                {
                    GroupId = baseReader.GetInt32(0),
                    GroupKey = baseReader.GetString(1),
                    GroupName = baseReader.GetString(2),
                    Description = baseReader.IsDBNull(3) ? null : baseReader.GetString(3),
                    IsActive = baseReader.GetBoolean(4),
                    Roles = new List<AdminRoleDto>()
                };

                groups.Add(group);
                groupById[group.GroupId] = group;
            }
        }

        if (groupById.Count == 0)
        {
            return groups;
        }

        var rolesSql = $@"
SELECT
    gr.app_group_id,
    r.id,
    r.role_key,
    r.name,
    r.role_kind,
    r.department_id,
    d.name,
    r.is_active
FROM app_group_roles gr
JOIN app_roles r ON r.id = gr.app_role_id
LEFT JOIN departments d ON d.id = r.department_id
WHERE (@groupId IS NULL OR gr.app_group_id = @groupId)
  AND r.role_kind = '{AuthorizationRoles.SystemRoleKind}'
ORDER BY gr.app_group_id, r.role_kind, d.name, r.name, r.id;";

        await using var rolesCommand = new NpgsqlCommand(rolesSql, connection, transaction);
        var rolesGroupIdParameter = rolesCommand.Parameters.Add("groupId", NpgsqlDbType.Integer);
        rolesGroupIdParameter.Value = (object?)groupId ?? DBNull.Value;
        await using var rolesReader = await rolesCommand.ExecuteReaderAsync(cancellationToken);

        while (await rolesReader.ReadAsync(cancellationToken))
        {
            var currentGroupId = rolesReader.GetInt32(0);
            if (!groupById.TryGetValue(currentGroupId, out var group))
            {
                continue;
            }

            group.Roles.Add(new AdminRoleDto
            {
                RoleId = rolesReader.GetInt32(1),
                RoleKey = rolesReader.GetString(2),
                RoleName = rolesReader.GetString(3),
                RoleKind = rolesReader.GetString(4),
                DepartmentId = rolesReader.IsDBNull(5) ? null : rolesReader.GetInt32(5),
                DepartmentName = rolesReader.IsDBNull(6) ? null : rolesReader.GetString(6),
                IsActive = rolesReader.GetBoolean(7)
            });
        }

        return groups;
    }

    private static async Task<List<AdminDepartmentAssignmentDto>> LoadAdminDepartmentAssignments(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        int? departmentId,
        CancellationToken cancellationToken)
    {
        const string sql = @"
SELECT
    d.id,
    d.name,
    lead_user.id,
    lead_user.display_name,
    requirement_user.id,
    requirement_user.display_name,
    ds.updated_at
FROM departments d
LEFT JOIN department_settings ds ON ds.department_id = d.id
LEFT JOIN people lead_person ON lead_person.id = ds.department_lead_person_id
LEFT JOIN app_users lead_user ON lead_user.id = lead_person.app_user_id
LEFT JOIN people requirement_person ON requirement_person.id = ds.requirement_approver_person_id
LEFT JOIN app_users requirement_user ON requirement_user.id = requirement_person.app_user_id
WHERE (@departmentId IS NULL OR d.id = @departmentId)
ORDER BY d.name, d.id;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        var departmentIdParameter = command.Parameters.Add("departmentId", NpgsqlDbType.Integer);
        departmentIdParameter.Value = (object?)departmentId ?? DBNull.Value;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var assignments = new List<AdminDepartmentAssignmentDto>();
        while (await reader.ReadAsync(cancellationToken))
        {
            assignments.Add(new AdminDepartmentAssignmentDto
            {
                DepartmentId = reader.GetInt32(0),
                DepartmentName = reader.GetString(1),
                DepartmentLeadUserId = reader.IsDBNull(2) ? null : reader.GetInt64(2),
                DepartmentLeadDisplayName = reader.IsDBNull(3) ? null : reader.GetString(3),
                RequirementOwnerUserId = reader.IsDBNull(4) ? null : reader.GetInt64(4),
                RequirementOwnerDisplayName = reader.IsDBNull(5) ? null : reader.GetString(5),
                UpdatedAt = reader.IsDBNull(6) ? null : reader.GetDateTime(6)
            });
        }

        return assignments;
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
    r.name,
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

    private static async Task<List<AdminUserDto>> LoadAdminUsers(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        long? userId,
        CancellationToken cancellationToken)
    {
        var baseSql = $@"
SELECT
    u.id,
    u.external_key,
    u.display_name,
    u.email,
    u.notification_email,
    u.is_active,
    COALESCE(p.department_id, u.department_id),
    d.name,
    EXISTS (
        SELECT 1
        FROM (
            SELECT ur.app_role_id AS role_id
            FROM app_user_roles ur
            WHERE ur.app_user_id = u.id
            UNION
            SELECT gr.app_role_id AS role_id
            FROM app_user_groups ug
            JOIN app_group_roles gr ON gr.app_group_id = ug.app_group_id
            JOIN app_groups g ON g.id = ug.app_group_id
            WHERE ug.app_user_id = u.id
              AND g.is_active = TRUE
        ) assigned_roles
        JOIN app_roles r ON r.id = assigned_roles.role_id
        WHERE r.is_active = TRUE
          AND r.role_kind = '{AuthorizationRoles.SystemRoleKind}'
          AND r.role_key = '{AuthorizationRoles.Manager}'
    ) AS has_manager_access
FROM app_users u
LEFT JOIN people p ON p.app_user_id = u.id
LEFT JOIN departments d ON d.id = COALESCE(p.department_id, u.department_id)
WHERE (@userId IS NULL OR u.id = @userId)
ORDER BY u.display_name, u.id;";

        var users = new List<AdminUserDto>();
        var userById = new Dictionary<long, AdminUserDto>();

        await using (var baseCommand = new NpgsqlCommand(baseSql, connection, transaction))
        {
            var baseUserIdParameter = baseCommand.Parameters.Add("userId", NpgsqlDbType.Bigint);
            baseUserIdParameter.Value = (object?)userId ?? DBNull.Value;
            await using var baseReader = await baseCommand.ExecuteReaderAsync(cancellationToken);

            while (await baseReader.ReadAsync(cancellationToken))
            {
                var user = new AdminUserDto
                {
                    UserId = baseReader.GetInt64(0),
                    ExternalKey = baseReader.IsDBNull(1) ? null : baseReader.GetString(1),
                    DisplayName = baseReader.GetString(2),
                    Email = baseReader.GetString(3),
                    NotificationEmail = baseReader.IsDBNull(4) ? null : baseReader.GetString(4),
                    IsActive = baseReader.GetBoolean(5),
                    DepartmentId = baseReader.IsDBNull(6) ? null : baseReader.GetInt32(6),
                    DepartmentName = baseReader.IsDBNull(7) ? null : baseReader.GetString(7),
                    HasManagerAccess = baseReader.GetBoolean(8),
                    Roles = new List<AdminRoleDto>(),
                    Groups = new List<AdminGroupRefDto>()
                };

                users.Add(user);
                userById[user.UserId] = user;
            }
        }

        if (userById.Count == 0)
        {
            return users;
        }

        var roleSql = $@"
SELECT
    ur.app_user_id,
    r.id,
    r.role_key,
    r.name,
    r.role_kind,
    r.department_id,
    d.name,
    r.is_active
FROM app_user_roles ur
JOIN app_roles r ON r.id = ur.app_role_id
LEFT JOIN departments d ON d.id = r.department_id
WHERE (@userId IS NULL OR ur.app_user_id = @userId)
  AND r.role_kind = '{AuthorizationRoles.SystemRoleKind}'
ORDER BY ur.app_user_id, r.role_kind, d.name, r.name, r.id;";

        await using (var roleCommand = new NpgsqlCommand(roleSql, connection, transaction))
        {
            var roleUserIdParameter = roleCommand.Parameters.Add("userId", NpgsqlDbType.Bigint);
            roleUserIdParameter.Value = (object?)userId ?? DBNull.Value;
            await using var roleReader = await roleCommand.ExecuteReaderAsync(cancellationToken);

            while (await roleReader.ReadAsync(cancellationToken))
            {
                var currentUserId = roleReader.GetInt64(0);
                if (!userById.TryGetValue(currentUserId, out var user))
                {
                    continue;
                }

                user.Roles.Add(new AdminRoleDto
                {
                    RoleId = roleReader.GetInt32(1),
                    RoleKey = roleReader.GetString(2),
                    RoleName = roleReader.GetString(3),
                    RoleKind = roleReader.GetString(4),
                    DepartmentId = roleReader.IsDBNull(5) ? null : roleReader.GetInt32(5),
                    DepartmentName = roleReader.IsDBNull(6) ? null : roleReader.GetString(6),
                    IsActive = roleReader.GetBoolean(7)
                });
            }
        }

        const string groupSql = @"
SELECT
    ug.app_user_id,
    g.id,
    g.group_key,
    g.name,
    g.description,
    g.is_active
FROM app_user_groups ug
JOIN app_groups g ON g.id = ug.app_group_id
WHERE (@userId IS NULL OR ug.app_user_id = @userId)
ORDER BY ug.app_user_id, g.name, g.id;";

        await using var groupCommand = new NpgsqlCommand(groupSql, connection, transaction);
        var groupUserIdParameter = groupCommand.Parameters.Add("userId", NpgsqlDbType.Bigint);
        groupUserIdParameter.Value = (object?)userId ?? DBNull.Value;
        await using var groupReader = await groupCommand.ExecuteReaderAsync(cancellationToken);

        while (await groupReader.ReadAsync(cancellationToken))
        {
            var currentUserId = groupReader.GetInt64(0);
            if (!userById.TryGetValue(currentUserId, out var user))
            {
                continue;
            }

            user.Groups.Add(new AdminGroupRefDto
            {
                GroupId = groupReader.GetInt32(1),
                GroupKey = groupReader.GetString(2),
                GroupName = groupReader.GetString(3),
                Description = groupReader.IsDBNull(4) ? null : groupReader.GetString(4),
                IsActive = groupReader.GetBoolean(5)
            });
        }

        return users;
    }
}
