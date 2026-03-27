using Npgsql;
using NpgsqlTypes;

namespace API;

// Laedt Benutzer, Rollen, Gruppen und Verantwortlichkeiten fuer Demo-Login und Admin-Bereich aus PostgreSQL.
internal sealed partial class PostgresUserAuthorizationRepository : IUserAuthorizationRepository
{
    private static readonly string DemoEligibleRoleFilterSql = string.Join(
        ",\n          ",
        AuthorizationRoles.ReadAllowed.Select(static roleKey => $"'{roleKey}'"));

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
            departmentId = reader.IsDBNull(5) ? null : reader.GetInt32(5);
            departmentName = reader.IsDBNull(6) ? null : reader.GetString(6);
        }

        var directRoles = await LoadDirectRoles(connection, userId, cancellationToken);
        var groups = await LoadGroups(connection, userId, cancellationToken);
        var groupRoles = await LoadGroupRoles(connection, userId, cancellationToken);
        var directoryGroupRoles = await LoadDirectoryGroupRoles(connection, userId, cancellationToken);
        var effectiveRoles = BuildEffectiveRoles(directRoles, groupRoles, directoryGroupRoles);
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
            Groups = groups,
            DirectRoles = directRoles,
            GroupRoles = groupRoles,
            EffectiveRoles = effectiveRoles,
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
    is_active = TRUE
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
INSERT INTO app_users (external_key, entra_object_id, display_name, email, is_active)
VALUES (@externalKey, @entraObjectId, @displayName, @email, TRUE)
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

        // INSERT associated people record.
        const string insertPeopleSql = @"
INSERT INTO people (app_user_id)
VALUES (@userId)
ON CONFLICT (app_user_id) DO NOTHING;";

        await using (var peopleCmd = new NpgsqlCommand(insertPeopleSql, connection))
        {
            peopleCmd.Parameters.AddWithValue("userId", userId!.Value);
            await peopleCmd.ExecuteNonQueryAsync(cancellationToken);
        }

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

    // Liefert die auswaehlbaren Demo-Benutzer fuer die Login-Seite.
    public async Task<List<DemoLoginUserOptionDto>> GetDemoLoginUsers(CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync(cancellationToken);

        var sql = $@"
SELECT
    u.id,
    u.external_key,
    u.display_name,
    u.email,
    d.name
FROM app_users u
LEFT JOIN people p ON p.app_user_id = u.id
LEFT JOIN departments d ON d.id = COALESCE(p.department_id, u.department_id)
WHERE u.is_active = TRUE
  AND u.external_key IS NOT NULL
  AND BTRIM(u.external_key) <> ''
  AND EXISTS (
      SELECT 1
      FROM (
          SELECT ur.app_role_id AS role_id
          FROM app_user_roles ur
          WHERE ur.app_user_id = u.id
          UNION
          SELECT gr.app_role_id AS role_id
          FROM app_user_groups ug
          JOIN app_group_roles gr ON gr.app_group_id = ug.app_group_id
          WHERE ug.app_user_id = u.id
      ) assigned_roles
      JOIN app_roles r ON r.id = assigned_roles.role_id
      WHERE r.role_key IN (
          {DemoEligibleRoleFilterSql}
      )
      AND r.role_kind = '{AuthorizationRoles.SystemRoleKind}'
  )
ORDER BY u.display_name, u.id;";

        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var users = new List<DemoLoginUserOptionDto>();
        while (await reader.ReadAsync(cancellationToken))
        {
            users.Add(new DemoLoginUserOptionDto
            {
                UserId = reader.GetInt64(0),
                Username = reader.GetString(1),
                DisplayName = reader.GetString(2),
                Email = reader.GetString(3),
                DepartmentName = reader.IsDBNull(4) ? null : reader.GetString(4)
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

    public async Task<List<AdminDepartmentAssignmentDto>> GetAdminDepartmentAssignments(CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync(cancellationToken);
        return await LoadAdminDepartmentAssignments(connection, null, null, cancellationToken);
    }

    public async Task<List<AdminResponsibilityOwnerDto>> GetAdminResponsibilityOwners(CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync(cancellationToken);
        return await LoadAdminResponsibilityOwners(connection, null, null, cancellationToken);
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
                GroupKey = null
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
        const string sql = @"
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
                AssignmentSource = "group"
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
    r.role_kind
FROM directory_identities di
JOIN directory_group_members dgm ON dgm.directory_identity_id = di.id
JOIN directory_group_role_mappings dgrm ON dgrm.directory_group_id = dgm.directory_group_id
JOIN directory_groups dg ON dg.id = dgm.directory_group_id
JOIN app_roles r ON r.id = dgrm.app_role_id
WHERE di.app_user_id = @userId
  AND dgrm.is_active = TRUE
  AND r.is_active = TRUE
  AND r.role_kind = 'system'
ORDER BY dg.display_name, r.name;";

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
                    AssignmentSource = "directory_group"
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
        var effectiveByRoleId = new Dictionary<int, CurrentUserRole>();

        // Priority: direct > app group > directory group.
        foreach (var role in directRoles)
        {
            effectiveByRoleId[role.RoleId] = role;
        }

        foreach (var role in groupRoles)
        {
            if (!effectiveByRoleId.ContainsKey(role.RoleId))
            {
                effectiveByRoleId[role.RoleId] = role;
            }
        }

        if (directoryGroupRoles is not null)
        {
            foreach (var role in directoryGroupRoles)
            {
                if (!effectiveByRoleId.ContainsKey(role.RoleId))
                {
                    effectiveByRoleId[role.RoleId] = role;
                }
            }
        }

        return effectiveByRoleId
            .Values
            .OrderBy(role => role.RoleKind, StringComparer.OrdinalIgnoreCase)
            .ThenBy(role => role.RoleName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(role => role.RoleKey, StringComparer.OrdinalIgnoreCase)
            .ToList();
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
        var connectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("CONNECTION_STRING is not configured.");
        }

        return connectionString;
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
}
