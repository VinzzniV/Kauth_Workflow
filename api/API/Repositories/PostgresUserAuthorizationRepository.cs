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
    u.department_id,
    d.name
FROM app_users u
LEFT JOIN departments d ON d.id = u.department_id
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
        var effectiveRoles = BuildEffectiveRoles(directRoles, groupRoles);
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
LEFT JOIN departments d ON d.id = u.department_id
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

    private static List<CurrentUserRole> BuildEffectiveRoles(
        IReadOnlyList<CurrentUserRole> directRoles,
        IReadOnlyList<CurrentUserRole> groupRoles)
    {
        var effectiveByRoleId = new Dictionary<int, CurrentUserRole>();

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
