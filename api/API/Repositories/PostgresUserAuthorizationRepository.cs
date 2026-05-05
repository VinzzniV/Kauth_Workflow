using Npgsql;
using NpgsqlTypes;

namespace API;

// Laedt Benutzer, Rollen, Gruppen und Verantwortlichkeiten fuer Simulations-Login und Admin-Bereich aus PostgreSQL.
internal sealed partial class PostgresUserAuthorizationRepository : IUserAuthorizationRepository
{
    // Baut aus einer aufgeloesten Identitaet das vollstaendige CurrentUser-Modell fuer die Autorisierung.
    public async Task<CurrentUser?> ResolveCurrentUser(ResolvedIdentity identity, CancellationToken cancellationToken = default)
    {
        var userIdFilter = identity.UserId is > 0 ? identity.UserId : null;
        var normalizedExternalKey = Normalize(identity.ExternalKey);
        var normalizedEmail = Normalize(identity.Email);

        if (!userIdFilter.HasValue
            && string.IsNullOrWhiteSpace(normalizedExternalKey)
            && string.IsNullOrWhiteSpace(normalizedEmail))
        {
            return null;
        }

        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync(cancellationToken);

        const string userSql = @"
SELECT
    u.id,
    u.external_key,
    u.display_name,
    u.email,
    u.is_active,
    u.directory_synced,
    u.department_source,
    u.department_override_active,
    COALESCE(p.department_id, u.department_id),
    d.name
FROM app_users u
LEFT JOIN people p ON p.app_user_id = u.id
LEFT JOIN departments d ON d.id = COALESCE(p.department_id, u.department_id)
WHERE (
    (@userId IS NOT NULL AND u.id = @userId)
    OR (@externalKey IS NOT NULL AND u.external_key = @externalKey)
    OR (@email IS NOT NULL AND LOWER(u.email) = LOWER(@email))
)
ORDER BY CASE
    WHEN @userId IS NOT NULL AND u.id = @userId THEN 0
    WHEN @externalKey IS NOT NULL AND u.external_key = @externalKey THEN 0
    ELSE 1
END,
u.id
LIMIT 1;";

        long userId;
        string? externalKey;
        string displayName;
        string email;
        bool isActive;
        bool directorySynced;
        string departmentSource;
        bool departmentOverrideActive;
        int? departmentId;
        string? departmentName;

        await using (var userCommand = new NpgsqlCommand(userSql, connection))
        {
            var userIdParameter = userCommand.Parameters.Add("userId", NpgsqlDbType.Bigint);
            userIdParameter.Value = (object?)userIdFilter ?? DBNull.Value;

            var externalKeyParameter = userCommand.Parameters.Add("externalKey", NpgsqlDbType.Text);
            externalKeyParameter.Value = (object?)normalizedExternalKey ?? DBNull.Value;

            var emailParameter = userCommand.Parameters.Add("email", NpgsqlDbType.Text);
            emailParameter.Value = (object?)normalizedEmail ?? DBNull.Value;

            await using var reader = await userCommand.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            userId = reader.GetInt64(0);
            externalKey = reader.IsDBNull(1) ? null : reader.GetString(1);
            displayName = reader.GetString(2);
            email = reader.GetString(3);
            isActive = reader.GetBoolean(4);
            directorySynced = reader.GetBoolean(5);
            departmentSource = reader.GetString(6);
            departmentOverrideActive = reader.GetBoolean(7);
            departmentId = reader.IsDBNull(8) ? null : reader.GetInt32(8);
            departmentName = reader.IsDBNull(9) ? null : reader.GetString(9);
        }

        if (string.Equals(identity.Provider, "entra", StringComparison.OrdinalIgnoreCase))
        {
            await EnsureAutoProvisionRoleAssignment(connection, userId, cancellationToken);
        }

        var directRoles = await LoadDirectRoles(connection, userId, cancellationToken);
        var groups = await LoadGroups(connection, userId, cancellationToken);
        var groupRoles = await LoadGroupRoles(connection, userId, cancellationToken);
        var directoryGroupRoles = await LoadDirectoryGroupRoles(connection, userId, cancellationToken);
        var effectiveRoles = BuildEffectiveRoles(directRoles, groupRoles, directoryGroupRoles);
        var rolePermissionDefinitions = await LoadRolePermissionDefinitions(
            connection,
            effectiveRoles.Select(role => role.RoleId).Distinct().ToArray(),
            cancellationToken);
        var userPermissionOverrides = await LoadUserPermissionOverrides(connection, userId, cancellationToken);
        var rolePermissions = BuildRolePermissionGrants(effectiveRoles, rolePermissionDefinitions);
        var effectivePermissions = BuildEffectivePermissions(rolePermissions, userPermissionOverrides);
        var permissionScopes = BuildPermissionScopes(effectivePermissions);
        var directResponsibilities = await LoadDirectResponsibilities(connection, userId, cancellationToken);
        var groupResponsibilities = await LoadGroupResponsibilities(connection, userId, cancellationToken);
        var effectiveResponsibilities = BuildEffectiveResponsibilities(directResponsibilities, groupResponsibilities);

        return new CurrentUser
        {
            UserId = userId,
            ExternalKey = externalKey,
            DisplayName = displayName,
            Email = email,
            IsActive = isActive,
            DepartmentId = departmentId,
            DepartmentName = departmentName,
            IdentityProvider = identity.Provider,
            DirectorySynced = directorySynced,
            DepartmentSource = departmentSource,
            DepartmentOverrideActive = departmentOverrideActive,
            Groups = groups,
            DirectRoles = directRoles,
            GroupRoles = groupRoles,
            EffectiveRoles = effectiveRoles,
            EffectivePermissions = effectivePermissions,
            PermissionScopes = permissionScopes,
            PermissionOverrides = userPermissionOverrides,
            DirectResponsibilities = directResponsibilities,
            GroupResponsibilities = groupResponsibilities,
            EffectiveResponsibilities = effectiveResponsibilities
        };
    }

    // Auto-provisions a new app_users + people record from an external identity (e.g. Entra).
    // Returns the fully resolved CurrentUser after creation, or null if identity data is insufficient.
    public async Task<CurrentUser?> FindOrCreateFromExternalIdentity(
        ResolvedIdentity identity,
        CancellationToken cancellationToken = default)
    {
        var externalKey = Normalize(identity.ExternalKey);
        if (string.IsNullOrWhiteSpace(externalKey))
        {
            return null;
        }

        var displayName = !string.IsNullOrWhiteSpace(identity.DisplayName)
            ? identity.DisplayName!.Trim()
            : externalKey;

        var email = !string.IsNullOrWhiteSpace(identity.Email)
            ? identity.Email!.Trim()
            : $"{externalKey}@provisioned.local";

        var entraObjectId = Guid.TryParse(externalKey, out var parsedObjectId)
            ? parsedObjectId
            : (Guid?)null;

        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync(cancellationToken);

        const string findExistingSql = @"
SELECT id
FROM app_users
WHERE external_key = @externalKey
   OR (@entraObjectId IS NOT NULL AND entra_object_id = @entraObjectId)
   OR LOWER(email) = LOWER(@email)
ORDER BY CASE
    WHEN external_key = @externalKey THEN 0
    WHEN @entraObjectId IS NOT NULL AND entra_object_id = @entraObjectId THEN 1
    ELSE 2
END
LIMIT 1;";

        long? userId = null;
        await using (var findCmd = new NpgsqlCommand(findExistingSql, connection))
        {
            findCmd.Parameters.AddWithValue("externalKey", externalKey);
            findCmd.Parameters.AddWithValue("email", email);
            var entraObjectIdParameter = findCmd.Parameters.Add("entraObjectId", NpgsqlDbType.Uuid);
            entraObjectIdParameter.Value = (object?)entraObjectId ?? DBNull.Value;

            var existingId = await findCmd.ExecuteScalarAsync(cancellationToken);
            if (existingId is long existingUserId)
            {
                userId = existingUserId;
            }
        }

        if (userId.HasValue)
        {
            const string updateUserSql = @"
UPDATE app_users
SET external_key = @externalKey,
    entra_object_id = COALESCE(@entraObjectId, entra_object_id),
    display_name = @displayName,
    email = @email,
    is_active = TRUE,
    directory_synced = TRUE,
    last_directory_synced_at = NOW(),
    department_source = CASE
        WHEN department_override_active THEN 'override'
        WHEN department_id IS NULL THEN 'unassigned'
        ELSE department_source
    END
WHERE id = @userId;";

            await using var updateCmd = new NpgsqlCommand(updateUserSql, connection);
            updateCmd.Parameters.AddWithValue("userId", userId.Value);
            updateCmd.Parameters.AddWithValue("externalKey", externalKey);
            updateCmd.Parameters.AddWithValue("displayName", displayName);
            updateCmd.Parameters.AddWithValue("email", email);
            var entraObjectIdParameter = updateCmd.Parameters.Add("entraObjectId", NpgsqlDbType.Uuid);
            entraObjectIdParameter.Value = (object?)entraObjectId ?? DBNull.Value;
            await updateCmd.ExecuteNonQueryAsync(cancellationToken);
        }
        else
        {
            const string insertUserSql = @"
INSERT INTO app_users (
    external_key,
    entra_object_id,
    display_name,
    email,
    is_active,
    directory_synced,
    last_directory_synced_at,
    department_source
)
VALUES (@externalKey, @entraObjectId, @displayName, @email, TRUE, TRUE, NOW(), 'unassigned')
RETURNING id;";

            await using var insertCmd = new NpgsqlCommand(insertUserSql, connection);
            insertCmd.Parameters.AddWithValue("externalKey", externalKey);
            insertCmd.Parameters.AddWithValue("displayName", displayName);
            insertCmd.Parameters.AddWithValue("email", email);
            var entraObjectIdParameter = insertCmd.Parameters.Add("entraObjectId", NpgsqlDbType.Uuid);
            entraObjectIdParameter.Value = (object?)entraObjectId ?? DBNull.Value;

            var result = await insertCmd.ExecuteScalarAsync(cancellationToken);
            userId = (long)result!;
        }

        await EnsureAutoProvisionRoleAssignment(connection, userId.Value, cancellationToken);

        // Now resolve the full user model with roles/groups/responsibilities.
        return await ResolveCurrentUser(
            new ResolvedIdentity
            {
                UserId = userId,
                ExternalKey = identity.ExternalKey,
                Email = identity.Email,
                DisplayName = identity.DisplayName,
                Provider = identity.Provider
            },
            cancellationToken);
    }

    // Liefert die lokal synchronisierten Entra-Benutzer fuer die Dev-Simulationsseite.
    public async Task<List<SimulationLoginUserOptionDto>> GetSimulationLoginUsers(CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
SELECT DISTINCT ON (u.id)
    u.id,
    COALESCE(di.user_principal_name, u.external_key, u.email),
    u.display_name,
    u.email,
    d.name,
    (
        SELECT COALESCE(array_agg(DISTINCT r.role_key ORDER BY r.role_key), '{}')
        FROM (
            SELECT app_role_id FROM app_user_roles WHERE app_user_id = u.id
            UNION
            SELECT dgrm.app_role_id
            FROM directory_group_members dgm2
            JOIN directory_group_role_mappings dgrm
              ON dgrm.directory_group_id = dgm2.directory_group_id
            WHERE dgm2.directory_identity_id = di.id
        ) effective
        JOIN app_roles r ON r.id = effective.app_role_id
    ) AS role_keys
FROM app_users u
JOIN directory_identities di ON di.app_user_id = u.id
JOIN directory_group_members dgm ON dgm.directory_identity_id = di.id
LEFT JOIN people p ON p.app_user_id = u.id
LEFT JOIN departments d ON d.id = COALESCE(p.department_id, u.department_id)
WHERE u.directory_synced = TRUE
  AND di.account_enabled = TRUE
ORDER BY u.id, di.last_synced_at DESC NULLS LAST, di.id DESC;";

        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var users = new List<SimulationLoginUserOptionDto>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var roleKeys = reader.IsDBNull(5)
                ? Array.Empty<string>()
                : reader.GetFieldValue<string[]>(5);

            users.Add(new SimulationLoginUserOptionDto
            {
                UserId = reader.GetInt64(0),
                Username = reader.GetString(1),
                DisplayName = reader.GetString(2),
                Email = reader.GetString(3),
                DepartmentName = reader.IsDBNull(4) ? null : reader.GetString(4),
                RoleKeys = roleKeys
            });
        }

        return users;
    }

    public async Task<List<AdminUserDto>> GetAdminUsers(CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync(cancellationToken);
        return await LoadAdminUsers(connection, null, null, cancellationToken);
    }

    public async Task<List<AdminRoleDto>> GetAdminRoles(CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync(cancellationToken);
        return await LoadAdminRoles(connection, null, cancellationToken);
    }

    public async Task<List<AdminGroupDto>> GetAdminGroups(CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync(cancellationToken);
        return await LoadAdminGroups(connection, null, null, cancellationToken);
    }

    public async Task<AdminListPageDto<AdminDepartmentAssignmentDto>> GetAdminDepartmentAssignments(AdminListQuery query, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync(cancellationToken);
        return await LoadAdminDepartmentAssignmentsPage(connection, null, query, cancellationToken);
    }

    public async Task<AdminListPageDto<AdminRoleDto>> GetAdminDepartmentPositions(AdminListQuery query, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync(cancellationToken);
        return await LoadAdminPositionRolesPage(connection, null, query, cancellationToken);
    }

    public async Task<AdminListPageDto<AdminResponsibilityOwnerDto>> GetAdminResponsibilityOwners(AdminListQuery query, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync(cancellationToken);
        return await LoadAdminResponsibilityOwnersPage(connection, null, query, cancellationToken);
    }

    private static async Task<List<CurrentUserRole>> LoadDirectRoles(
        NpgsqlConnection connection,
        long userId,
        CancellationToken cancellationToken)
    {
        var sql = $@"
SELECT
    r.id,
    r.role_key,
    r.name,
    r.role_kind
FROM app_user_roles ur
JOIN app_roles r ON r.id = ur.app_role_id
WHERE ur.app_user_id = @userId
  AND r.is_active = TRUE
  AND r.role_kind = '{AuthorizationRoles.SystemRoleKind}'
ORDER BY r.role_kind, r.name;";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("userId", userId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var roles = new List<CurrentUserRole>();
        while (await reader.ReadAsync(cancellationToken))
        {
            roles.Add(new CurrentUserRole
            {
                RoleId = reader.GetInt32(0),
                RoleKey = reader.GetString(1),
                RoleName = reader.GetString(2),
                RoleKind = reader.GetString(3),
                AssignmentSource = "direct",
                GroupId = null,
                GroupKey = null,
                Scope = "global",
                ScopeDepartmentId = null,
                ScopeDepartmentName = null
            });
        }

        return roles;
    }

    private static async Task<List<CurrentUserGroup>> LoadGroups(
        NpgsqlConnection connection,
        long userId,
        CancellationToken cancellationToken)
    {
        var sql = $@"
SELECT
    g.id,
    g.group_key,
    g.name,
    g.description
FROM app_user_groups ug
JOIN app_groups g ON g.id = ug.app_group_id
WHERE ug.app_user_id = @userId
  AND g.is_active = TRUE
ORDER BY g.name;";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("userId", userId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var groups = new List<CurrentUserGroup>();
        while (await reader.ReadAsync(cancellationToken))
        {
            groups.Add(new CurrentUserGroup
            {
                GroupId = reader.GetInt32(0),
                GroupKey = reader.GetString(1),
                GroupName = reader.GetString(2),
                Description = reader.IsDBNull(3) ? null : reader.GetString(3)
            });
        }

        return groups;
    }

    private static async Task<List<CurrentUserRole>> LoadGroupRoles(
        NpgsqlConnection connection,
        long userId,
        CancellationToken cancellationToken)
    {
        var sql = $@"
SELECT
    g.id,
    g.group_key,
    r.id,
    r.role_key,
    r.name,
    r.role_kind
FROM app_user_groups ug
JOIN app_groups g ON g.id = ug.app_group_id
JOIN app_group_roles gr ON gr.app_group_id = g.id
JOIN app_roles r ON r.id = gr.app_role_id
WHERE ug.app_user_id = @userId
  AND g.is_active = TRUE
  AND r.is_active = TRUE
  AND r.role_kind = '{AuthorizationRoles.SystemRoleKind}'
ORDER BY g.name, r.role_kind, r.name;";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("userId", userId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var groupRoles = new List<CurrentUserRole>();
        while (await reader.ReadAsync(cancellationToken))
        {
            groupRoles.Add(new CurrentUserRole
            {
                GroupId = reader.GetInt32(0),
                GroupKey = reader.GetString(1),
                RoleId = reader.GetInt32(2),
                RoleKey = reader.GetString(3),
                RoleName = reader.GetString(4),
                RoleKind = reader.GetString(5),
                AssignmentSource = "group",
                Scope = "global",
                ScopeDepartmentId = null,
                ScopeDepartmentName = null
            });
        }

        return groupRoles;
    }

    private static async Task<List<CurrentUserRole>> LoadDirectoryGroupRoles(
        NpgsqlConnection connection,
        long userId,
        CancellationToken cancellationToken)
    {
        const string sql = @"
SELECT
    dg.id,
    dg.display_name,
    r.id,
    r.role_key,
    r.name,
    r.role_kind,
    dgrm.scope,
    dgrm.scope_department_id,
    scope_department.name
FROM directory_identities di
JOIN directory_group_members dgm ON dgm.directory_identity_id = di.id
JOIN directory_group_role_mappings dgrm ON dgrm.directory_group_id = dgm.directory_group_id
JOIN directory_groups dg ON dg.id = dgm.directory_group_id
JOIN app_roles r ON r.id = dgrm.app_role_id
LEFT JOIN departments scope_department ON scope_department.id = dgrm.scope_department_id
WHERE di.app_user_id = @userId
  AND dgrm.is_active = TRUE
  AND r.is_active = TRUE
  AND r.role_kind = 'system'
ORDER BY dg.display_name, r.name, dgrm.scope, scope_department.name;";

        // Guard: if directory tables don't exist yet (migration not applied), return empty.
        try
        {
            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("userId", userId);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var roles = new List<CurrentUserRole>();
            while (await reader.ReadAsync(cancellationToken))
            {
                roles.Add(new CurrentUserRole
                {
                    GroupId = reader.GetInt32(0),
                    GroupKey = null,
                    DirectoryGroupName = reader.GetString(1),
                    RoleId = reader.GetInt32(2),
                    RoleKey = reader.GetString(3),
                    RoleName = reader.GetString(4),
                    RoleKind = reader.GetString(5),
                    AssignmentSource = "directory_group",
                    Scope = reader.GetString(6),
                    ScopeDepartmentId = reader.IsDBNull(7) ? null : reader.GetInt32(7),
                    ScopeDepartmentName = reader.IsDBNull(8) ? null : reader.GetString(8)
                });
            }

            return roles;
        }
        catch (Npgsql.PostgresException ex) when (ex.SqlState == "42P01")
        {
            // Table does not exist yet — migration not applied. Return empty list.
            return [];
        }
    }

    private static List<CurrentUserRole> BuildEffectiveRoles(
        IReadOnlyList<CurrentUserRole> directRoles,
        IReadOnlyList<CurrentUserRole> groupRoles,
        IReadOnlyList<CurrentUserRole>? directoryGroupRoles = null)
    {
        var effectiveByRoleKey = new Dictionary<string, CurrentUserRole>(StringComparer.OrdinalIgnoreCase);

        // Priority: direct > app group > directory group.
        foreach (var role in directRoles)
        {
            effectiveByRoleKey[ToEffectiveRoleKey(role)] = role;
        }

        foreach (var role in groupRoles)
        {
            var key = ToEffectiveRoleKey(role);
            if (!effectiveByRoleKey.ContainsKey(key))
            {
                effectiveByRoleKey[key] = role;
            }
        }

        if (directoryGroupRoles is not null)
        {
            foreach (var role in directoryGroupRoles)
            {
                var key = ToEffectiveRoleKey(role);
                if (!effectiveByRoleKey.ContainsKey(key))
                {
                    effectiveByRoleKey[key] = role;
                }
            }
        }

        return effectiveByRoleKey
            .Values
            .OrderBy(role => role.RoleKind, StringComparer.OrdinalIgnoreCase)
            .ThenBy(role => role.RoleName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(role => role.Scope, StringComparer.OrdinalIgnoreCase)
            .ThenBy(role => role.ScopeDepartmentName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(role => role.RoleKey, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string ToEffectiveRoleKey(CurrentUserRole role)
    {
        return $"{role.RoleId}|{role.Scope.Trim().ToLowerInvariant()}|{role.ScopeDepartmentId?.ToString() ?? "global"}";
    }

    private static async Task<List<CurrentUserResponsibility>> LoadDirectResponsibilities(
        NpgsqlConnection connection,
        long userId,
        CancellationToken cancellationToken)
    {
        const string sql = @"
SELECT
    r.id,
    r.responsibility_key,
    r.name,
    r.responsibility_type,
    r.department_id,
    d.name
FROM app_user_responsibilities ur
JOIN app_responsibilities r ON r.id = ur.app_responsibility_id
LEFT JOIN departments d ON d.id = r.department_id
WHERE ur.app_user_id = @userId
  AND r.is_active = TRUE
ORDER BY r.responsibility_type, r.name;";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("userId", userId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var responsibilities = new List<CurrentUserResponsibility>();
        while (await reader.ReadAsync(cancellationToken))
        {
            responsibilities.Add(new CurrentUserResponsibility
            {
                ResponsibilityId = reader.GetInt32(0),
                ResponsibilityKey = reader.GetString(1),
                ResponsibilityName = reader.GetString(2),
                ResponsibilityType = reader.GetString(3),
                DepartmentId = reader.IsDBNull(4) ? null : reader.GetInt32(4),
                DepartmentName = reader.IsDBNull(5) ? null : reader.GetString(5),
                AssignmentSource = "direct",
                GroupId = null,
                GroupKey = null
            });
        }

        return responsibilities;
    }

    private static async Task<List<CurrentUserResponsibility>> LoadGroupResponsibilities(
        NpgsqlConnection connection,
        long userId,
        CancellationToken cancellationToken)
    {
        const string sql = @"
SELECT
    g.id,
    g.group_key,
    r.id,
    r.responsibility_key,
    r.name,
    r.responsibility_type,
    r.department_id,
    d.name
FROM app_user_groups ug
JOIN app_groups g ON g.id = ug.app_group_id
JOIN app_group_responsibilities gr ON gr.app_group_id = g.id
JOIN app_responsibilities r ON r.id = gr.app_responsibility_id
LEFT JOIN departments d ON d.id = r.department_id
WHERE ug.app_user_id = @userId
  AND g.is_active = TRUE
  AND r.is_active = TRUE
ORDER BY g.name, r.responsibility_type, r.name;";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("userId", userId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var responsibilities = new List<CurrentUserResponsibility>();
        while (await reader.ReadAsync(cancellationToken))
        {
            responsibilities.Add(new CurrentUserResponsibility
            {
                GroupId = reader.GetInt32(0),
                GroupKey = reader.GetString(1),
                ResponsibilityId = reader.GetInt32(2),
                ResponsibilityKey = reader.GetString(3),
                ResponsibilityName = reader.GetString(4),
                ResponsibilityType = reader.GetString(5),
                DepartmentId = reader.IsDBNull(6) ? null : reader.GetInt32(6),
                DepartmentName = reader.IsDBNull(7) ? null : reader.GetString(7),
                AssignmentSource = "group"
            });
        }

        return responsibilities;
    }

    private static List<CurrentUserResponsibility> BuildEffectiveResponsibilities(
        IReadOnlyList<CurrentUserResponsibility> directResponsibilities,
        IReadOnlyList<CurrentUserResponsibility> groupResponsibilities)
    {
        var effectiveByResponsibilityId = new Dictionary<int, CurrentUserResponsibility>();

        foreach (var responsibility in directResponsibilities)
        {
            effectiveByResponsibilityId[responsibility.ResponsibilityId] = responsibility;
        }

        foreach (var responsibility in groupResponsibilities)
        {
            if (!effectiveByResponsibilityId.ContainsKey(responsibility.ResponsibilityId))
            {
                effectiveByResponsibilityId[responsibility.ResponsibilityId] = responsibility;
            }
        }

        return effectiveByResponsibilityId
            .Values
            .OrderBy(responsibility => responsibility.ResponsibilityType, StringComparer.OrdinalIgnoreCase)
            .ThenBy(responsibility => responsibility.ResponsibilityName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(responsibility => responsibility.ResponsibilityKey, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string GetConnectionString()
    {
        return LifecycleRuntimeSettingsResolver.GetRequiredConnectionString();
    }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string NormalizeRequired(string? value, string errorMessage)
    {
        var normalized = Normalize(value);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidOperationException(errorMessage);
        }

        return normalized;
    }

    private static long? NormalizeNullableUserId(long? value)
    {
        return value is > 0 ? value : null;
    }

    private static int? NormalizeNullableDepartmentId(int? value)
    {
        return value is > 0 ? value : null;
    }

    private static async Task EnsureAutoProvisionRoleAssignment(
        NpgsqlConnection connection,
        long userId,
        CancellationToken cancellationToken)
    {
        var roleKey = LifecycleRuntimeSettingsResolver.ResolveFromEnvironment().AutoProvisionDefaultRoleKey;
        if (string.IsNullOrWhiteSpace(roleKey))
        {
            return;
        }

        const string sql = @"
INSERT INTO app_user_roles (app_user_id, app_role_id)
SELECT @userId, r.id
FROM app_roles r
WHERE r.role_key = @roleKey
  AND r.is_active = TRUE
  AND r.role_kind = 'system'
  AND NOT EXISTS (
      SELECT 1
      FROM app_user_roles ur
      WHERE ur.app_user_id = @userId
        AND ur.app_role_id = r.id
  );";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("userId", userId);
        command.Parameters.AddWithValue("roleKey", roleKey);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
