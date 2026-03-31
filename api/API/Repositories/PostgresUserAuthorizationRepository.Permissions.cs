using Npgsql;
using NpgsqlTypes;
using System.Text.Json;

namespace API;

internal sealed partial class PostgresUserAuthorizationRepository
{
    private sealed class RolePermissionDefinition
    {
        public required int RoleId { get; init; }
        public required int PermissionId { get; init; }
        public required string PermissionKey { get; init; }
        public required string PermissionName { get; init; }
        public required string ScopeKind { get; init; }
    }

    public async Task<List<AdminPermissionDto>> GetAdminPermissions(CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync(cancellationToken);
        return await LoadAdminPermissions(connection, null, cancellationToken);
    }

    public async Task<List<AdminPermissionAuditEntryDto>> GetAdminPermissionAudit(
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync(cancellationToken);
        return await LoadAdminPermissionAudit(connection, null, Math.Clamp(limit, 1, 200), cancellationToken);
    }

    public async Task<AdminRoleDto?> UpdateRolePermissions(
        int roleId,
        IReadOnlyList<int> permissionIds,
        long? actorUserId = null,
        CancellationToken cancellationToken = default)
    {
        if (roleId <= 0)
        {
            throw new InvalidOperationException("roleId must be greater than zero.");
        }

        var normalizedPermissionIds = permissionIds
            .Where(id => id > 0)
            .Distinct()
            .ToArray();

        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        if (!await RoleExists(connection, transaction, roleId, cancellationToken))
        {
            return null;
        }

        await EnsurePermissionIdsExist(connection, transaction, normalizedPermissionIds, cancellationToken);
        var previousRole = (await LoadAdminRoles(connection, transaction, cancellationToken))
            .FirstOrDefault(role => role.RoleId == roleId);

        const string deleteSql = @"
DELETE FROM app_role_permissions
WHERE app_role_id = @roleId
  AND (cardinality(@permissionIds) = 0 OR app_permission_id <> ALL(@permissionIds));";

        await using (var deleteCommand = new NpgsqlCommand(deleteSql, connection, transaction))
        {
            deleteCommand.Parameters.AddWithValue("roleId", roleId);
            deleteCommand.Parameters.Add("permissionIds", NpgsqlDbType.Array | NpgsqlDbType.Integer).Value = normalizedPermissionIds;
            await deleteCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        if (normalizedPermissionIds.Length > 0)
        {
            const string insertSql = @"
INSERT INTO app_role_permissions (app_role_id, app_permission_id)
SELECT @roleId, permission_id
FROM UNNEST(@permissionIds) AS permission_id
ON CONFLICT (app_role_id, app_permission_id) DO NOTHING;";

            await using var insertCommand = new NpgsqlCommand(insertSql, connection, transaction);
            insertCommand.Parameters.AddWithValue("roleId", roleId);
            insertCommand.Parameters.Add("permissionIds", NpgsqlDbType.Array | NpgsqlDbType.Integer).Value = normalizedPermissionIds;
            await insertCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        var updatedRole = (await LoadAdminRoles(connection, transaction, cancellationToken))
            .FirstOrDefault(role => role.RoleId == roleId);

        if (updatedRole is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        await LogPermissionAuditAsync(
            connection,
            transaction,
            actorUserId,
            "updated",
            "role_permissions",
            $"Permission bundle for role {updatedRole.RoleKey} updated.",
            previousRole,
            updatedRole,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return updatedRole;
    }

    public async Task<AdminUserDto?> UpdateUserPermissionOverrides(
        long userId,
        IReadOnlyList<AdminUserPermissionOverrideUpsertRequest> overrides,
        long? actorUserId = null,
        CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
        {
            throw new InvalidOperationException("userId must be greater than zero.");
        }

        await using var connection = new NpgsqlConnection(GetConnectionString());
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        if (!await UserExists(connection, transaction, userId, cancellationToken))
        {
            return null;
        }

        var normalizedOverrides = NormalizePermissionOverrides(overrides);
        await EnsurePermissionIdsExist(connection, transaction, normalizedOverrides.Select(item => item.PermissionId).Distinct().ToArray(), cancellationToken);
        await ValidatePermissionOverrideScopes(connection, transaction, normalizedOverrides, cancellationToken);

        var previousOverrides = await LoadAdminUserPermissionOverrides(connection, transaction, userId, cancellationToken);

        const string deleteSql = "DELETE FROM app_user_permission_overrides WHERE app_user_id = @userId;";
        await using (var deleteCommand = new NpgsqlCommand(deleteSql, connection, transaction))
        {
            deleteCommand.Parameters.AddWithValue("userId", userId);
            await deleteCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        if (normalizedOverrides.Count > 0)
        {
            const string insertSql = @"
INSERT INTO app_user_permission_overrides (
    app_user_id,
    app_permission_id,
    effect,
    scope,
    scope_department_id,
    created_at,
    updated_at
)
VALUES (
    @userId,
    @permissionId,
    @effect,
    @scope,
    @scopeDepartmentId,
    NOW(),
    NOW()
);";

            foreach (var item in normalizedOverrides)
            {
                await using var insertCommand = new NpgsqlCommand(insertSql, connection, transaction);
                insertCommand.Parameters.AddWithValue("userId", userId);
                insertCommand.Parameters.AddWithValue("permissionId", item.PermissionId);
                insertCommand.Parameters.AddWithValue("effect", item.Effect!);
                insertCommand.Parameters.AddWithValue("scope", item.Scope!);
                var scopeDepartmentIdParameter = insertCommand.Parameters.Add("scopeDepartmentId", NpgsqlDbType.Integer);
                scopeDepartmentIdParameter.Value = (object?)item.ScopeDepartmentId ?? DBNull.Value;
                await insertCommand.ExecuteNonQueryAsync(cancellationToken);
            }
        }

        var updatedOverrides = await LoadAdminUserPermissionOverrides(connection, transaction, userId, cancellationToken);
        await LogPermissionAuditAsync(
            connection,
            transaction,
            actorUserId,
            "updated",
            "user_permission_overrides",
            $"Permission overrides for user {userId} updated.",
            previousOverrides,
            updatedOverrides,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        var users = await LoadAdminUsers(connection, null, userId, cancellationToken);
        return users.FirstOrDefault();
    }

    private static async Task<Dictionary<int, List<RolePermissionDefinition>>> LoadRolePermissionDefinitions(
        NpgsqlConnection connection,
        IReadOnlyCollection<int> roleIds,
        CancellationToken cancellationToken)
    {
        if (roleIds.Count == 0)
        {
            return [];
        }

        const string sql = @"
SELECT
    rp.app_role_id,
    p.id,
    p.permission_key,
    p.name,
    p.scope_kind
FROM app_role_permissions rp
JOIN app_permissions p ON p.id = rp.app_permission_id
WHERE rp.app_role_id = ANY(@roleIds)
  AND p.is_active = TRUE
ORDER BY rp.app_role_id, p.category, p.name, p.id;";

        try
        {
            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.Add("roleIds", NpgsqlDbType.Array | NpgsqlDbType.Integer).Value = roleIds.ToArray();

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var definitions = new Dictionary<int, List<RolePermissionDefinition>>();
            while (await reader.ReadAsync(cancellationToken))
            {
                var roleId = reader.GetInt32(0);
                if (!definitions.TryGetValue(roleId, out var items))
                {
                    items = [];
                    definitions[roleId] = items;
                }

                items.Add(new RolePermissionDefinition
                {
                    RoleId = roleId,
                    PermissionId = reader.GetInt32(1),
                    PermissionKey = reader.GetString(2),
                    PermissionName = reader.GetString(3),
                    ScopeKind = reader.GetString(4)
                });
            }

            return definitions;
        }
        catch (PostgresException ex) when (ex.SqlState == "42P01")
        {
            return [];
        }
    }

    private static async Task<List<CurrentUserPermissionOverride>> LoadUserPermissionOverrides(
        NpgsqlConnection connection,
        long userId,
        CancellationToken cancellationToken)
    {
        const string sql = @"
SELECT
    upo.id,
    p.id,
    p.permission_key,
    p.name,
    upo.effect,
    upo.scope,
    upo.scope_department_id,
    d.name
FROM app_user_permission_overrides upo
JOIN app_permissions p ON p.id = upo.app_permission_id
LEFT JOIN departments d ON d.id = upo.scope_department_id
WHERE upo.app_user_id = @userId
ORDER BY p.category, p.name, upo.effect, upo.scope, d.name, upo.id;";

        try
        {
            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("userId", userId);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var overrides = new List<CurrentUserPermissionOverride>();
            while (await reader.ReadAsync(cancellationToken))
            {
                overrides.Add(new CurrentUserPermissionOverride
                {
                    OverrideId = reader.GetInt64(0),
                    PermissionId = reader.GetInt32(1),
                    PermissionKey = reader.GetString(2),
                    PermissionName = reader.GetString(3),
                    Effect = reader.GetString(4),
                    Scope = reader.GetString(5),
                    ScopeDepartmentId = reader.IsDBNull(6) ? null : reader.GetInt32(6),
                    ScopeDepartmentName = reader.IsDBNull(7) ? null : reader.GetString(7)
                });
            }

            return overrides;
        }
        catch (PostgresException ex) when (ex.SqlState == "42P01")
        {
            return [];
        }
    }

    private static List<CurrentUserPermission> BuildRolePermissionGrants(
        IReadOnlyList<CurrentUserRole> effectiveRoles,
        IReadOnlyDictionary<int, List<RolePermissionDefinition>> rolePermissionDefinitions)
    {
        var permissions = new List<CurrentUserPermission>();
        foreach (var role in effectiveRoles)
        {
            if (!rolePermissionDefinitions.TryGetValue(role.RoleId, out var definitions))
            {
                continue;
            }

            foreach (var definition in definitions)
            {
                var permissionScope = string.Equals(definition.ScopeKind, "department", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(role.Scope, "department", StringComparison.OrdinalIgnoreCase)
                    && role.ScopeDepartmentId.HasValue
                    ? "department"
                    : "global";

                permissions.Add(new CurrentUserPermission
                {
                    PermissionId = definition.PermissionId,
                    PermissionKey = definition.PermissionKey,
                    PermissionName = definition.PermissionName,
                    Scope = permissionScope,
                    ScopeDepartmentId = permissionScope == "department" ? role.ScopeDepartmentId : null,
                    ScopeDepartmentName = permissionScope == "department" ? role.ScopeDepartmentName : null
                });
            }
        }

        return permissions;
    }

    private static List<CurrentUserPermission> BuildEffectivePermissions(
        IReadOnlyList<CurrentUserPermission> rolePermissions,
        IReadOnlyList<CurrentUserPermissionOverride> overrides)
    {
        var grants = new Dictionary<string, CurrentUserPermission>(StringComparer.OrdinalIgnoreCase);

        foreach (var permission in rolePermissions)
        {
            grants[ToPermissionGrantKey(permission.PermissionId, permission.Scope, permission.ScopeDepartmentId)] = permission;
        }

        foreach (var permission in overrides
                     .Where(overridePermission => string.Equals(overridePermission.Effect, "allow", StringComparison.OrdinalIgnoreCase)))
        {
            grants[ToPermissionGrantKey(permission.PermissionId, permission.Scope, permission.ScopeDepartmentId)] =
                new CurrentUserPermission
                {
                    PermissionId = permission.PermissionId,
                    PermissionKey = permission.PermissionKey,
                    PermissionName = permission.PermissionName,
                    Scope = permission.Scope,
                    ScopeDepartmentId = permission.ScopeDepartmentId,
                    ScopeDepartmentName = permission.ScopeDepartmentName
                };
        }

        foreach (var permission in overrides
                     .Where(overridePermission => string.Equals(overridePermission.Effect, "deny", StringComparison.OrdinalIgnoreCase)))
        {
            if (string.Equals(permission.Scope, "global", StringComparison.OrdinalIgnoreCase))
            {
                var keysToRemove = grants
                    .Where(item => item.Value.PermissionId == permission.PermissionId)
                    .Select(item => item.Key)
                    .ToList();

                foreach (var key in keysToRemove)
                {
                    grants.Remove(key);
                }

                continue;
            }

            grants.Remove(ToPermissionGrantKey(permission.PermissionId, permission.Scope, permission.ScopeDepartmentId));
        }

        return grants.Values
            .OrderBy(permission => permission.PermissionName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(permission => permission.Scope, StringComparer.OrdinalIgnoreCase)
            .ThenBy(permission => permission.ScopeDepartmentName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(permission => permission.PermissionKey, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<CurrentUserPermissionScope> BuildPermissionScopes(IReadOnlyList<CurrentUserPermission> effectivePermissions)
    {
        return effectivePermissions
            .Select(permission => new CurrentUserPermissionScope
            {
                PermissionKey = permission.PermissionKey,
                Scope = permission.Scope,
                ScopeDepartmentId = permission.ScopeDepartmentId,
                ScopeDepartmentName = permission.ScopeDepartmentName
            })
            .OrderBy(scope => scope.PermissionKey, StringComparer.OrdinalIgnoreCase)
            .ThenBy(scope => scope.Scope, StringComparer.OrdinalIgnoreCase)
            .ThenBy(scope => scope.ScopeDepartmentName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static async Task<List<AdminPermissionDto>> LoadAdminPermissions(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        CancellationToken cancellationToken)
    {
        const string sql = @"
SELECT
    id,
    permission_key,
    name,
    description,
    scope_kind,
    category,
    is_active
FROM app_permissions
ORDER BY category, name, id;";

        try
        {
            await using var command = new NpgsqlCommand(sql, connection, transaction);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var permissions = new List<AdminPermissionDto>();
            while (await reader.ReadAsync(cancellationToken))
            {
                permissions.Add(new AdminPermissionDto
                {
                    PermissionId = reader.GetInt32(0),
                    PermissionKey = reader.GetString(1),
                    PermissionName = reader.GetString(2),
                    Description = reader.IsDBNull(3) ? null : reader.GetString(3),
                    ScopeKind = reader.GetString(4),
                    Category = reader.GetString(5),
                    IsActive = reader.GetBoolean(6)
                });
            }

            return permissions;
        }
        catch (PostgresException ex) when (ex.SqlState == "42P01")
        {
            return [];
        }
    }

    private static async Task<List<AdminPermissionOverrideDto>> LoadAdminUserPermissionOverrides(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        long userId,
        CancellationToken cancellationToken)
    {
        try
        {
            var overrides = await LoadUserPermissionOverrides(connection, userId, cancellationToken);
            return overrides
                .Select(overridePermission => new AdminPermissionOverrideDto
                {
                    OverrideId = overridePermission.OverrideId,
                    PermissionId = overridePermission.PermissionId,
                    PermissionKey = overridePermission.PermissionKey,
                    PermissionName = overridePermission.PermissionName,
                    Effect = overridePermission.Effect,
                    Scope = overridePermission.Scope,
                    ScopeDepartmentId = overridePermission.ScopeDepartmentId,
                    ScopeDepartmentName = overridePermission.ScopeDepartmentName
                })
                .ToList();
        }
        catch (PostgresException ex) when (ex.SqlState == "42P01")
        {
            return [];
        }
    }

    private static async Task<List<AdminPermissionAuditEntryDto>> LoadAdminPermissionAudit(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        int limit,
        CancellationToken cancellationToken)
    {
        const string sql = @"
SELECT
    audit.id,
    audit.actor_user_id,
    actor.display_name,
    audit.event_type,
    audit.entity_type,
    audit.detail,
    audit.old_value::text,
    audit.new_value::text,
    audit.created_at
FROM auth_permission_audit_log audit
LEFT JOIN app_users actor ON actor.id = audit.actor_user_id
ORDER BY audit.created_at DESC, audit.id DESC
LIMIT @limit;";

        try
        {
            await using var command = new NpgsqlCommand(sql, connection, transaction);
            command.Parameters.AddWithValue("limit", limit);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var entries = new List<AdminPermissionAuditEntryDto>();
            while (await reader.ReadAsync(cancellationToken))
            {
                entries.Add(new AdminPermissionAuditEntryDto
                {
                    AuditEntryId = reader.GetInt64(0),
                    ActorUserId = reader.IsDBNull(1) ? null : reader.GetInt64(1),
                    ActorDisplayName = reader.IsDBNull(2) ? null : reader.GetString(2),
                    EventType = reader.GetString(3),
                    EntityType = reader.GetString(4),
                    Detail = reader.IsDBNull(5) ? null : reader.GetString(5),
                    OldValue = reader.IsDBNull(6) ? null : reader.GetString(6),
                    NewValue = reader.IsDBNull(7) ? null : reader.GetString(7),
                    CreatedAt = reader.GetDateTime(8)
                });
            }

            return entries;
        }
        catch (PostgresException ex) when (ex.SqlState == "42P01")
        {
            return [];
        }
    }

    private static async Task EnsurePermissionIdsExist(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int[] permissionIds,
        CancellationToken cancellationToken)
    {
        if (permissionIds.Length == 0)
        {
            return;
        }

        const string sql = @"
SELECT COUNT(*)
FROM app_permissions
WHERE id = ANY(@permissionIds)
  AND is_active = TRUE;";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.Add("permissionIds", NpgsqlDbType.Array | NpgsqlDbType.Integer).Value = permissionIds;
        var count = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken) ?? 0);
        if (count != permissionIds.Length)
        {
            throw new InvalidOperationException("One or more permissions are invalid or inactive.");
        }
    }

    private static async Task ValidatePermissionOverrideScopes(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        IReadOnlyList<AdminUserPermissionOverrideUpsertRequest> overrides,
        CancellationToken cancellationToken)
    {
        if (overrides.Count == 0)
        {
            return;
        }

        foreach (var item in overrides)
        {
            const string sql = @"
SELECT scope_kind
FROM app_permissions
WHERE id = @permissionId
LIMIT 1;";

            await using var command = new NpgsqlCommand(sql, connection, transaction);
            command.Parameters.AddWithValue("permissionId", item.PermissionId);
            var scopeKind = await command.ExecuteScalarAsync(cancellationToken) as string;
            if (scopeKind is null)
            {
                throw new InvalidOperationException("Permission not found.");
            }

            var normalizedScope = NormalizeScope(item.Scope);
            if (string.Equals(scopeKind, "global", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(normalizedScope, "global", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Globale Berechtigungen dürfen nicht abteilungsbezogen überschrieben werden.");
            }

            if (string.Equals(normalizedScope, "department", StringComparison.OrdinalIgnoreCase)
                && item.ScopeDepartmentId is not > 0)
            {
                throw new InvalidOperationException("Für abteilungsbezogene Berechtigungen muss eine Abteilung gewählt werden.");
            }
        }
    }

    private static async Task LogPermissionAuditAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long? actorUserId,
        string eventType,
        string entityType,
        string detail,
        object? oldValue,
        object? newValue,
        CancellationToken cancellationToken)
    {
        const string sql = @"
INSERT INTO auth_permission_audit_log (actor_user_id, event_type, entity_type, detail, old_value, new_value, created_at)
VALUES (@actorUserId, @eventType, @entityType, @detail, CAST(@oldValue AS jsonb), CAST(@newValue AS jsonb), NOW());";

        try
        {
            await using var command = new NpgsqlCommand(sql, connection, transaction);
            var actorParameter = command.Parameters.Add("actorUserId", NpgsqlDbType.Bigint);
            actorParameter.Value = (object?)actorUserId ?? DBNull.Value;
            command.Parameters.AddWithValue("eventType", eventType);
            command.Parameters.AddWithValue("entityType", entityType);
            command.Parameters.AddWithValue("detail", detail);
            command.Parameters.AddWithValue("oldValue", (object?)SerializeAuditJson(oldValue) ?? DBNull.Value);
            command.Parameters.AddWithValue("newValue", (object?)SerializeAuditJson(newValue) ?? DBNull.Value);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (PostgresException ex) when (ex.SqlState == "42P01")
        {
            // Migration not applied yet.
        }
    }

    private static IReadOnlyList<AdminUserPermissionOverrideUpsertRequest> NormalizePermissionOverrides(
        IReadOnlyList<AdminUserPermissionOverrideUpsertRequest> overrides)
    {
        return overrides
            .Where(item => item.PermissionId > 0 && !string.IsNullOrWhiteSpace(item.Effect))
            .Select(item => new AdminUserPermissionOverrideUpsertRequest
            {
                PermissionId = item.PermissionId,
                Effect = NormalizeEffect(item.Effect),
                Scope = NormalizeScope(item.Scope),
                ScopeDepartmentId = NormalizeNullableDepartmentId(item.ScopeDepartmentId)
            })
            .DistinctBy(item => $"{item.PermissionId}|{item.Effect}|{item.Scope}|{item.ScopeDepartmentId}")
            .ToArray();
    }

    private static string NormalizeEffect(string? value)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? "allow" : value.Trim().ToLowerInvariant();
        return normalized switch
        {
            "allow" => "allow",
            "deny" => "deny",
            _ => throw new InvalidOperationException("Effect must be 'allow' or 'deny'.")
        };
    }

    private static string NormalizeScope(string? value)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? "global" : value.Trim().ToLowerInvariant();
        return normalized switch
        {
            "global" => "global",
            "department" => "department",
            _ => throw new InvalidOperationException("Scope must be 'global' or 'department'.")
        };
    }

    private static string ToPermissionGrantKey(int permissionId, string scope, int? scopeDepartmentId)
    {
        return $"{permissionId}|{scope.Trim().ToLowerInvariant()}|{scopeDepartmentId?.ToString() ?? "global"}";
    }

    private static string? SerializeAuditJson(object? value)
    {
        return value is null ? null : JsonSerializer.Serialize(value);
    }
}
