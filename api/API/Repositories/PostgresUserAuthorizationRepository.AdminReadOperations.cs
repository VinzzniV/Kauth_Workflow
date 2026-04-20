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

        var roles = new List<AdminRoleDto>();
        await using (var command = new NpgsqlCommand(sql, connection, transaction))
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

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
                    Scope = "global",
                    ScopeDepartmentId = null,
                    ScopeDepartmentName = null,
                    IsActive = reader.GetBoolean(6)
                });
            }
        }

        if (roles.Count == 0)
        {
            return roles;
        }

        try
        {
            const string permissionsSql = @"
SELECT
    rp.app_role_id,
    p.id,
    p.permission_key,
    p.name,
    p.description,
    p.scope_kind,
    p.category,
    p.is_active
FROM app_role_permissions rp
JOIN app_permissions p ON p.id = rp.app_permission_id
WHERE rp.app_role_id = ANY(@roleIds)
ORDER BY rp.app_role_id, p.category, p.name, p.id;";

            await using var permissionCommand = new NpgsqlCommand(permissionsSql, connection, transaction);
            permissionCommand.Parameters.Add("roleIds", NpgsqlDbType.Array | NpgsqlDbType.Integer).Value =
                roles.Select(role => role.RoleId).Distinct().ToArray();

            var roleById = roles.ToDictionary(role => role.RoleId);
            await using var permissionReader = await permissionCommand.ExecuteReaderAsync(cancellationToken);
            while (await permissionReader.ReadAsync(cancellationToken))
            {
                if (!roleById.TryGetValue(permissionReader.GetInt32(0), out var role))
                {
                    continue;
                }

                role.Permissions.Add(new AdminPermissionDto
                {
                    PermissionId = permissionReader.GetInt32(1),
                    PermissionKey = permissionReader.GetString(2),
                    PermissionName = permissionReader.GetString(3),
                    Description = permissionReader.IsDBNull(4) ? null : permissionReader.GetString(4),
                    ScopeKind = permissionReader.GetString(5),
                    Category = permissionReader.GetString(6),
                    IsActive = permissionReader.GetBoolean(7)
                });
            }
        }
        catch (PostgresException ex) when (ex.SqlState == "42P01")
        {
            // Permission migration not applied yet.
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
                Scope = "global",
                ScopeDepartmentId = null,
                ScopeDepartmentName = null,
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
WITH managed_departments AS (
    SELECT DISTINCT u.department_id
    FROM app_users u
    JOIN directory_identities di ON di.app_user_id = u.id
    WHERE u.directory_synced = TRUE
      AND u.is_active = TRUE
      AND di.account_enabled = TRUE
      AND u.department_override_active = FALSE
      AND u.department_id IS NOT NULL
),
entra_manager_candidates AS (
    SELECT DISTINCT
        u.department_id,
        u.id AS app_user_id,
        p.id AS person_id,
        u.display_name
    FROM app_users u
    JOIN directory_identities di ON di.app_user_id = u.id
    JOIN directory_group_members dgm ON dgm.directory_identity_id = di.id
    JOIN directory_group_role_mappings dgrm ON dgrm.directory_group_id = dgm.directory_group_id
    JOIN app_roles ar ON ar.id = dgrm.app_role_id
    LEFT JOIN people p ON p.app_user_id = u.id
    WHERE u.directory_synced = TRUE
      AND u.is_active = TRUE
      AND di.account_enabled = TRUE
      AND u.department_override_active = FALSE
      AND u.department_id IS NOT NULL
      AND dgrm.is_active = TRUE
      AND ar.role_key = 'auth_manager'
      AND ar.role_kind = 'system'
),
candidate_summary AS (
    SELECT
        department_id,
        COUNT(*) AS candidate_count,
        MIN(app_user_id) AS resolved_user_id,
        MIN(person_id) AS resolved_person_id,
        MIN(display_name) AS resolved_display_name,
        STRING_AGG(display_name, ', ' ORDER BY display_name) AS candidate_names
    FROM entra_manager_candidates
    GROUP BY department_id
)
SELECT
    d.id,
    d.name,
    lead_user.id,
    lead_user.display_name,
    requirement_user.id,
    requirement_user.display_name,
    CASE
        WHEN managed.department_id IS NULL THEN 'manual'
        ELSE 'entra_managed'
    END AS assignment_source,
    CASE
        WHEN managed.department_id IS NULL THEN 'manual'
        WHEN COALESCE(candidate.candidate_count, 0) = 1 THEN 'resolved'
        WHEN COALESCE(candidate.candidate_count, 0) = 0 THEN 'missing'
        ELSE 'conflict'
    END AS sync_state,
    CASE
        WHEN managed.department_id IS NULL THEN 'Keine Entra-geführte Abteilungsleitung erkannt.'
        WHEN COALESCE(candidate.candidate_count, 0) = 1
         AND candidate.resolved_person_id IS NOT NULL
         AND candidate.resolved_person_id = ds.department_lead_person_id
         AND candidate.resolved_person_id = ds.requirement_approver_person_id
            THEN 'Entra hat genau eine aktive Abteilungsleitung für diese Abteilung aufgelöst.'
        WHEN COALESCE(candidate.candidate_count, 0) = 1
            THEN 'Entra führt diese Abteilung. Beim nächsten Sync werden Leitung und Anforderungsverantwortung auf '
                || COALESCE(candidate.resolved_display_name, 'die gefundene Person')
                || ' gesetzt.'
        WHEN COALESCE(candidate.candidate_count, 0) = 0
            THEN 'Keine aktive Entra-Abteilungsleitung für diese Abteilung gefunden.'
        ELSE 'Mehrere aktive Entra-Abteilungsleitungen gefunden: '
            || COALESCE(candidate.candidate_names, 'unbekannt')
            || '.'
    END AS sync_detail,
    ds.updated_at
FROM departments d
LEFT JOIN department_settings ds ON ds.department_id = d.id
LEFT JOIN people lead_person ON lead_person.id = ds.department_lead_person_id
LEFT JOIN app_users lead_user ON lead_user.id = lead_person.app_user_id
LEFT JOIN people requirement_person ON requirement_person.id = ds.requirement_approver_person_id
LEFT JOIN app_users requirement_user ON requirement_user.id = requirement_person.app_user_id
LEFT JOIN managed_departments managed ON managed.department_id = d.id
LEFT JOIN candidate_summary candidate ON candidate.department_id = d.id
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
                AssignmentSource = reader.GetString(6),
                SyncState = reader.GetString(7),
                SyncDetail = reader.IsDBNull(8) ? null : reader.GetString(8),
                UpdatedAt = reader.IsDBNull(9) ? null : reader.GetDateTime(9)
            });
        }

        return assignments;
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
    u.directory_synced,
    u.department_source,
    u.department_override_active,
    (
        LOWER(u.display_name) LIKE 'automation actor %'
        AND LOWER(u.email) LIKE 'automation.actor.%@example.test'
    ) AS is_technical_actor,
    di.id,
    di.user_principal_name,
    di.display_name,
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
            UNION
            SELECT dgrm.app_role_id AS role_id
            FROM directory_identities di_current
            JOIN directory_group_members dgm ON dgm.directory_identity_id = di_current.id
            JOIN directory_group_role_mappings dgrm ON dgrm.directory_group_id = dgm.directory_group_id
            WHERE di_current.app_user_id = u.id
              AND dgrm.is_active = TRUE
        ) assigned_roles
        JOIN app_roles r ON r.id = assigned_roles.role_id
        WHERE r.is_active = TRUE
          AND r.role_kind = '{AuthorizationRoles.SystemRoleKind}'
          AND r.role_key = '{AuthorizationRoles.Manager}'
    ) AS has_manager_access
FROM app_users u
LEFT JOIN people p ON p.app_user_id = u.id
LEFT JOIN directory_identities di ON di.app_user_id = u.id
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
                    DirectorySynced = baseReader.GetBoolean(6),
                    DepartmentSource = baseReader.GetString(7),
                    DepartmentOverrideActive = baseReader.GetBoolean(8),
                    IsTechnicalActor = baseReader.GetBoolean(9),
                    DirectoryIdentityId = baseReader.IsDBNull(10) ? null : baseReader.GetInt64(10),
                    UserPrincipalName = baseReader.IsDBNull(11) ? null : baseReader.GetString(11),
                    DirectoryDisplayName = baseReader.IsDBNull(12) ? null : baseReader.GetString(12),
                    DepartmentId = baseReader.IsDBNull(13) ? null : baseReader.GetInt32(13),
                    DepartmentName = baseReader.IsDBNull(14) ? null : baseReader.GetString(14),
                    HasManagerAccess = baseReader.GetBoolean(15),
                    Roles = new List<AdminRoleDto>(),
                    Groups = new List<AdminGroupRefDto>(),
                    EffectiveRoles = new List<AdminRoleDto>(),
                    PermissionOverrides = new List<AdminPermissionOverrideDto>(),
                    EffectivePermissions = new List<AdminPermissionGrantDto>()
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
                    Scope = "global",
                    ScopeDepartmentId = null,
                    ScopeDepartmentName = null,
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

        await using (var groupCommand = new NpgsqlCommand(groupSql, connection, transaction))
        {
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
        }

        foreach (var user in users)
        {
            var directRoles = user.Roles
                .Select(role => new CurrentUserRole
                {
                    RoleId = role.RoleId,
                    RoleKey = role.RoleKey,
                    RoleName = role.RoleName,
                    RoleKind = role.RoleKind,
                    AssignmentSource = "direct",
                    Scope = "global"
                })
                .ToList();
            var groupRoles = await LoadGroupRoles(connection, user.UserId, cancellationToken);
            var directoryGroupRoles = await LoadDirectoryGroupRoles(connection, user.UserId, cancellationToken);
            var effectiveRoles = BuildEffectiveRoles(directRoles, groupRoles, directoryGroupRoles);
            var rolePermissionDefinitions = await LoadRolePermissionDefinitions(
                connection,
                effectiveRoles.Select(role => role.RoleId).Distinct().ToArray(),
                cancellationToken);
            var permissionOverrides = await LoadAdminUserPermissionOverrides(connection, transaction, user.UserId, cancellationToken);
            var effectivePermissions = BuildEffectivePermissions(
                BuildRolePermissionGrants(effectiveRoles, rolePermissionDefinitions),
                permissionOverrides.Select(overridePermission => new CurrentUserPermissionOverride
                {
                    OverrideId = overridePermission.OverrideId,
                    PermissionId = overridePermission.PermissionId,
                    PermissionKey = overridePermission.PermissionKey,
                    PermissionName = overridePermission.PermissionName,
                    Effect = overridePermission.Effect,
                    Scope = overridePermission.Scope,
                    ScopeDepartmentId = overridePermission.ScopeDepartmentId,
                    ScopeDepartmentName = overridePermission.ScopeDepartmentName
                }).ToList());

            user.EffectiveRoles.AddRange(effectiveRoles.Select(role => new AdminRoleDto
            {
                RoleId = role.RoleId,
                RoleKey = role.RoleKey,
                RoleName = role.RoleName,
                RoleKind = role.RoleKind,
                DepartmentId = null,
                DepartmentName = null,
                Scope = role.Scope,
                ScopeDepartmentId = role.ScopeDepartmentId,
                ScopeDepartmentName = role.ScopeDepartmentName,
                IsActive = true
            }));
                user.PermissionOverrides.AddRange(permissionOverrides);
                user.EffectivePermissions.AddRange(effectivePermissions.Select(permission => new AdminPermissionGrantDto
                {
                    PermissionId = permission.PermissionId,
                    PermissionKey = permission.PermissionKey,
                PermissionName = permission.PermissionName,
                Scope = permission.Scope,
                    ScopeDepartmentId = permission.ScopeDepartmentId,
                    ScopeDepartmentName = permission.ScopeDepartmentName
                }));
                user.CanAccessSupervisorStep = AdminUserEligibility.CanAccessSupervisorStep(user);
        }

        return users;
    }
}
