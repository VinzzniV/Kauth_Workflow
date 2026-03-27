using Azure.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Npgsql;
using NpgsqlTypes;
using System.Text.Json;

namespace API;

/// <summary>
/// Syncs groups and identities from Microsoft Entra ID into local projection tables
/// and serves the admin endpoints that manage directory-to-role mappings.
/// </summary>
internal sealed class EntraDirectorySyncService : IDirectorySyncService
{
    private readonly INotificationEmailConfigurationService _configService;
    private readonly ILogger<EntraDirectorySyncService> _logger;

    public EntraDirectorySyncService(
        INotificationEmailConfigurationService configService,
        ILogger<EntraDirectorySyncService> logger)
    {
        _configService = configService;
        _logger = logger;
    }

    public async Task<DirectorySyncResult> SyncAllAsync(
        string? groupPrefixOverride = null,
        CancellationToken cancellationToken = default)
    {
        var startedAt = DateTime.UtcNow;
        var connectionString = GetConnectionStringOrNull();
        var configuredGroupPrefix = Normalize(Environment.GetEnvironmentVariable("DIRECTORY_GROUP_PREFIX"));
        var effectiveGroupPrefix = ResolveEffectiveGroupPrefix(configuredGroupPrefix, groupPrefixOverride);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return new DirectorySyncResult
            {
                Status = "failed",
                ErrorMessage = "CONNECTION_STRING not configured.",
                AppliedGroupPrefix = effectiveGroupPrefix
            };
        }

        GraphServiceClient graphClient;
        try
        {
            var credentials = await ResolveGraphCredentialsAsync(cancellationToken);
            if (credentials is null)
            {
                return new DirectorySyncResult
                {
                    Status = "failed",
                    ErrorMessage =
                        "Graph credentials not configured. Set ENTRA_TENANT_ID, ENTRA_CLIENT_ID and ENTRA_CLIENT_SECRET/GRAPH_CLIENT_SECRET via environment variables or maintain them in the System configuration.",
                    AppliedGroupPrefix = effectiveGroupPrefix
                };
            }

            var credential = new ClientSecretCredential(
                credentials.Value.TenantId,
                credentials.Value.ClientId,
                credentials.Value.ClientSecret,
                new ClientSecretCredentialOptions { AuthorityHost = AzureAuthorityHosts.AzurePublicCloud });
            graphClient = new GraphServiceClient(credential, ["https://graph.microsoft.com/.default"]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create Graph client for directory sync.");
            return new DirectorySyncResult
            {
                Status = "failed",
                ErrorMessage = ex.Message,
                AppliedGroupPrefix = effectiveGroupPrefix
            };
        }

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        var groupsSynced = 0;
        var identitiesSynced = 0;
        var membershipsSynced = 0;
        string? errorMessage = null;
        var status = "success";
        try
        {
            var groupsResponse = await graphClient.Groups.GetAsync(config =>
            {
                config.QueryParameters.Select = ["id", "displayName", "description", "securityEnabled"];
                config.QueryParameters.Filter = "securityEnabled eq true";
                config.QueryParameters.Top = 999;
            }, cancellationToken);

            var groups = groupsResponse?.Value ?? [];

            foreach (var group in groups)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!MatchesGroupPrefix(group.DisplayName, effectiveGroupPrefix))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(group.Id) || !Guid.TryParse(group.Id, out var externalGroupId))
                {
                    continue;
                }

                var directoryGroupId = await UpsertDirectoryGroup(
                    connection,
                    externalGroupId,
                    group.DisplayName ?? group.Id,
                    group.Description,
                    cancellationToken);
                groupsSynced++;

                try
                {
                    var membersResponse = await graphClient.Groups[group.Id].Members.GetAsync(config =>
                    {
                        config.QueryParameters.Select = ["id", "displayName", "mail", "userPrincipalName", "accountEnabled"];
                        config.QueryParameters.Top = 999;
                    }, cancellationToken);

                    var members = membersResponse?.Value ?? [];

                    await ClearGroupMemberships(connection, directoryGroupId, cancellationToken);

                    foreach (var member in members)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        if (member is not Microsoft.Graph.Models.User user
                            || string.IsNullOrWhiteSpace(user.Id)
                            || !Guid.TryParse(user.Id, out var userObjectId))
                        {
                            continue;
                        }

                        var directoryIdentityId = await UpsertDirectoryIdentity(
                            connection,
                            userObjectId,
                            user,
                            cancellationToken);
                        identitiesSynced++;

                        await InsertGroupMembership(connection, directoryGroupId, directoryIdentityId, cancellationToken);
                        membershipsSynced++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to sync members for group {GroupId} ({GroupName}).", group.Id, group.DisplayName);
                    status = "partial";
                    errorMessage ??= $"Some group members could not be synced: {ex.Message}";
                }
            }

            await AutoLinkIdentitiesToAppUsers(connection, cancellationToken);
        }
        catch (PostgresException ex) when (ex.SqlState == "42P01")
        {
            status = "failed";
            errorMessage = "Directory tables are missing. Apply migration db/35_directory_tables.sql first.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Directory sync failed.");
            status = "failed";
            errorMessage = ex.Message;
        }

        try
        {
            await LogSyncRun(
                connection,
                "full",
                status,
                groupsSynced,
                identitiesSynced,
                membershipsSynced,
                errorMessage,
                startedAt,
                cancellationToken);
        }
        catch (PostgresException ex) when (ex.SqlState == "42P01")
        {
            // Ignore when migration is not applied yet.
        }

        return new DirectorySyncResult
        {
            Status = status,
            GroupsSynced = groupsSynced,
            IdentitiesSynced = identitiesSynced,
            MembershipsSynced = membershipsSynced,
            ErrorMessage = errorMessage,
            AppliedGroupPrefix = effectiveGroupPrefix
        };
    }

    private async Task<(string TenantId, string ClientId, string ClientSecret)?> ResolveGraphCredentialsAsync(
        CancellationToken cancellationToken)
    {
        var tenantId = Normalize(Environment.GetEnvironmentVariable("ENTRA_TENANT_ID"));
        var clientId = Normalize(Environment.GetEnvironmentVariable("ENTRA_CLIENT_ID"));
        var clientSecret =
            Normalize(Environment.GetEnvironmentVariable("ENTRA_CLIENT_SECRET"))
            ?? Normalize(Environment.GetEnvironmentVariable("GRAPH_CLIENT_SECRET"));

        if (!string.IsNullOrWhiteSpace(tenantId)
            && !string.IsNullOrWhiteSpace(clientId)
            && !string.IsNullOrWhiteSpace(clientSecret))
        {
            return (tenantId, clientId, clientSecret);
        }

        var configuration = await _configService.GetRuntimeConfiguration(cancellationToken);
        if (string.IsNullOrWhiteSpace(configuration.TenantId)
            || string.IsNullOrWhiteSpace(configuration.ClientId)
            || string.IsNullOrWhiteSpace(configuration.ClientSecret))
        {
            return null;
        }

        return (configuration.TenantId, configuration.ClientId, configuration.ClientSecret);
    }

    public async Task<DirectorySyncStatusDto> GetSyncStatusAsync(CancellationToken cancellationToken = default)
    {
        var connectionString = GetConnectionStringOrNull();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return new DirectorySyncStatusDto();
        }

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        try
        {
            const string sql = @"
WITH latest_sync AS (
    SELECT started_at
    FROM directory_sync_log
    ORDER BY id DESC
    LIMIT 1
)
SELECT
    (SELECT completed_at FROM directory_sync_log ORDER BY id DESC LIMIT 1),
    (SELECT status FROM directory_sync_log ORDER BY id DESC LIMIT 1),
    (
        SELECT COUNT(*)::int
        FROM directory_groups dg
        WHERE dg.last_synced_at >= COALESCE((SELECT started_at FROM latest_sync), '-infinity'::timestamptz)
    ),
    (
        SELECT COUNT(DISTINCT dgm.directory_identity_id)::int
        FROM directory_group_members dgm
        JOIN directory_groups dg ON dg.id = dgm.directory_group_id
        WHERE dg.last_synced_at >= COALESCE((SELECT started_at FROM latest_sync), '-infinity'::timestamptz)
    ),
    (
        SELECT COUNT(*)::int
        FROM directory_group_role_mappings dgrm
        JOIN directory_groups dg ON dg.id = dgrm.directory_group_id
        WHERE dgrm.is_active = TRUE
          AND dg.last_synced_at >= COALESCE((SELECT started_at FROM latest_sync), '-infinity'::timestamptz)
    ),
    (SELECT error_message FROM directory_sync_log WHERE error_message IS NOT NULL ORDER BY id DESC LIMIT 1);";

            await using var cmd = new NpgsqlCommand(sql, connection);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                return new DirectorySyncStatusDto
                {
                    LastSyncAt = reader.IsDBNull(0) ? null : reader.GetDateTime(0),
                    LastSyncStatus = reader.IsDBNull(1) ? null : reader.GetString(1),
                    TotalGroups = reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                    TotalIdentities = reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                    TotalMappings = reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
                    LastError = reader.IsDBNull(5) ? null : reader.GetString(5),
                    ConfiguredGroupPrefix = Normalize(Environment.GetEnvironmentVariable("DIRECTORY_GROUP_PREFIX"))
                };
            }
        }
        catch (PostgresException ex) when (ex.SqlState == "42P01")
        {
            // Migration not applied yet.
        }

        return new DirectorySyncStatusDto
        {
            ConfiguredGroupPrefix = Normalize(Environment.GetEnvironmentVariable("DIRECTORY_GROUP_PREFIX"))
        };
    }

    public async Task<List<AdminDirectoryGroupDto>> GetGroupsAsync(CancellationToken cancellationToken = default)
    {
        var connectionString = GetConnectionStringOrNull();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return [];
        }

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        try
        {
            const string groupsSql = @"
WITH latest_sync AS (
    SELECT started_at
    FROM directory_sync_log
    ORDER BY id DESC
    LIMIT 1
)
SELECT
    dg.id,
    dg.external_group_id::text,
    dg.display_name,
    dg.description,
    dg.last_synced_at,
    COUNT(dgm.directory_identity_id)::int AS member_count
FROM directory_groups dg
LEFT JOIN directory_group_members dgm ON dgm.directory_group_id = dg.id
WHERE dg.last_synced_at >= COALESCE((SELECT started_at FROM latest_sync), '-infinity'::timestamptz)
GROUP BY dg.id, dg.external_group_id, dg.display_name, dg.description, dg.last_synced_at
ORDER BY dg.display_name, dg.id;";

            var groups = new Dictionary<int, AdminDirectoryGroupDto>();
            await using (var groupsCommand = new NpgsqlCommand(groupsSql, connection))
            await using (var reader = await groupsCommand.ExecuteReaderAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    var groupId = reader.GetInt32(0);
                    groups[groupId] = new AdminDirectoryGroupDto
                    {
                        DirectoryGroupId = groupId,
                        ExternalGroupId = reader.GetString(1),
                        DisplayName = reader.GetString(2),
                        Description = reader.IsDBNull(3) ? null : reader.GetString(3),
                        LastSyncedAt = reader.IsDBNull(4) ? null : reader.GetDateTime(4),
                        MemberCount = reader.GetInt32(5),
                        RoleMappings = []
                    };
                }
            }

            const string mappingsSql = @"
SELECT
    dgrm.id,
    dgrm.directory_group_id,
    dgrm.app_role_id,
    r.role_key,
    r.name,
    r.role_kind,
    r.department_id,
    department.name,
    dgrm.scope,
    dgrm.scope_department_id,
    dgrm.is_active
FROM directory_group_role_mappings dgrm
JOIN app_roles r ON r.id = dgrm.app_role_id
LEFT JOIN departments department ON department.id = r.department_id
ORDER BY dgrm.directory_group_id, r.role_kind, r.name, dgrm.id;";

            await using (var mappingsCommand = new NpgsqlCommand(mappingsSql, connection))
            await using (var reader = await mappingsCommand.ExecuteReaderAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    var directoryGroupId = reader.GetInt32(1);
                    if (!groups.TryGetValue(directoryGroupId, out var group))
                    {
                        continue;
                    }

                    group.RoleMappings.Add(new AdminDirectoryGroupRoleMappingDto
                    {
                        MappingId = reader.GetInt32(0),
                        DirectoryGroupId = directoryGroupId,
                        AppRoleId = reader.GetInt32(2),
                        AppRoleKey = reader.GetString(3),
                        AppRoleName = reader.GetString(4),
                        AppRoleKind = reader.GetString(5),
                        RoleDepartmentId = reader.IsDBNull(6) ? null : reader.GetInt32(6),
                        RoleDepartmentName = reader.IsDBNull(7) ? null : reader.GetString(7),
                        Scope = reader.GetString(8),
                        ScopeDepartmentId = reader.IsDBNull(9) ? null : reader.GetInt32(9),
                        IsActive = reader.GetBoolean(10)
                    });
                }
            }

            return groups.Values
                .OrderBy(group => group.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(group => group.DirectoryGroupId)
                .ToList();
        }
        catch (PostgresException ex) when (ex.SqlState == "42P01")
        {
            return [];
        }
    }

    public async Task<List<AdminDirectoryIdentityDto>> GetIdentitiesAsync(
        int limit = 100,
        int offset = 0,
        CancellationToken cancellationToken = default)
    {
        var connectionString = GetConnectionStringOrNull();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return [];
        }

        limit = Math.Clamp(limit, 1, 250);
        offset = Math.Max(offset, 0);

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        try
        {
            const string sql = @"
WITH latest_sync AS (
    SELECT started_at
    FROM directory_sync_log
    ORDER BY id DESC
    LIMIT 1
),
current_groups AS (
    SELECT dg.id
    FROM directory_groups dg
    WHERE dg.last_synced_at >= COALESCE((SELECT started_at FROM latest_sync), '-infinity'::timestamptz)
)
SELECT
    di.id,
    di.entra_object_id::text,
    di.user_principal_name,
    di.mail,
    di.display_name,
    di.account_enabled,
    di.app_user_id,
    u.display_name,
    di.last_synced_at
FROM directory_identities di
JOIN directory_group_members dgm ON dgm.directory_identity_id = di.id
JOIN current_groups cg ON cg.id = dgm.directory_group_id
LEFT JOIN app_users u ON u.id = di.app_user_id
GROUP BY di.id, di.entra_object_id, di.user_principal_name, di.mail, di.display_name, di.account_enabled, di.app_user_id, u.display_name, di.last_synced_at
ORDER BY di.display_name, di.id
LIMIT @limit OFFSET @offset;";

            await using var cmd = new NpgsqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("limit", limit);
            cmd.Parameters.AddWithValue("offset", offset);

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            var identities = new List<AdminDirectoryIdentityDto>();
            while (await reader.ReadAsync(cancellationToken))
            {
                identities.Add(new AdminDirectoryIdentityDto
                {
                    DirectoryIdentityId = reader.GetInt64(0),
                    EntraObjectId = reader.GetString(1),
                    UserPrincipalName = reader.GetString(2),
                    Mail = reader.IsDBNull(3) ? null : reader.GetString(3),
                    DisplayName = reader.GetString(4),
                    AccountEnabled = reader.GetBoolean(5),
                    AppUserId = reader.IsDBNull(6) ? null : reader.GetInt64(6),
                    AppUserDisplayName = reader.IsDBNull(7) ? null : reader.GetString(7),
                    LastSyncedAt = reader.IsDBNull(8) ? null : reader.GetDateTime(8)
                });
            }

            return identities;
        }
        catch (PostgresException ex) when (ex.SqlState == "42P01")
        {
            return [];
        }
    }

    public async Task<List<AdminDirectoryMappingAuditEntryDto>> GetMappingAuditAsync(
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var connectionString = GetConnectionStringOrNull();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return [];
        }

        limit = Math.Clamp(limit, 1, 200);

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        try
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
FROM directory_mapping_audit_log audit
LEFT JOIN app_users actor ON actor.id = audit.actor_user_id
ORDER BY audit.created_at DESC, audit.id DESC
LIMIT @limit;";

            await using var cmd = new NpgsqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("limit", limit);

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            var entries = new List<AdminDirectoryMappingAuditEntryDto>();
            while (await reader.ReadAsync(cancellationToken))
            {
                entries.Add(new AdminDirectoryMappingAuditEntryDto
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

    public async Task<AdminDirectoryGroupRoleMappingDto> UpsertGroupRoleMappingAsync(
        AdminDirectoryGroupRoleMappingUpsertRequest request,
        long? actorUserId = null,
        CancellationToken cancellationToken = default)
    {
        var connectionString = GetConnectionStringOrNull();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("CONNECTION_STRING is not configured.");
        }

        var scope = NormalizeScope(request.Scope);
        var scopeDepartmentId = request.ScopeDepartmentId is > 0 ? request.ScopeDepartmentId : null;

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        try
        {
            var existingId = await FindExistingMappingIdAsync(
                connection,
                request.DirectoryGroupId,
                request.AppRoleId,
                scope,
                scopeDepartmentId,
                cancellationToken);

            if (existingId.HasValue)
            {
                var previousMapping = await GetGroupRoleMappingByIdAsync(connection, existingId.Value, cancellationToken);

                const string updateSql = @"
UPDATE directory_group_role_mappings
SET is_active = @isActive,
    scope_department_id = @scopeDepartmentId
WHERE id = @mappingId;";

                await using var updateCommand = new NpgsqlCommand(updateSql, connection);
                updateCommand.Parameters.AddWithValue("mappingId", existingId.Value);
                updateCommand.Parameters.AddWithValue("isActive", request.IsActive);
                var scopeDepartmentParameter = updateCommand.Parameters.Add("scopeDepartmentId", NpgsqlDbType.Integer);
                scopeDepartmentParameter.Value = (object?)scopeDepartmentId ?? DBNull.Value;
                await updateCommand.ExecuteNonQueryAsync(cancellationToken);

                var updatedMapping = await GetGroupRoleMappingByIdAsync(connection, existingId.Value, cancellationToken);
                await LogMappingAuditAsync(
                    connection,
                    actorUserId,
                    "updated",
                    "directory_group_role_mapping",
                    $"Mapping {updatedMapping.MappingId} for directory group {updatedMapping.DirectoryGroupId} updated.",
                    previousMapping,
                    updatedMapping,
                    cancellationToken);
                return updatedMapping;
            }

            const string insertSql = @"
INSERT INTO directory_group_role_mappings (directory_group_id, app_role_id, scope, scope_department_id, is_active)
VALUES (@directoryGroupId, @appRoleId, @scope, @scopeDepartmentId, @isActive)
RETURNING id;";

            await using var insertCommand = new NpgsqlCommand(insertSql, connection);
            insertCommand.Parameters.AddWithValue("directoryGroupId", request.DirectoryGroupId);
            insertCommand.Parameters.AddWithValue("appRoleId", request.AppRoleId);
            insertCommand.Parameters.AddWithValue("scope", scope);
            var insertScopeDepartmentParameter = insertCommand.Parameters.Add("scopeDepartmentId", NpgsqlDbType.Integer);
            insertScopeDepartmentParameter.Value = (object?)scopeDepartmentId ?? DBNull.Value;
            insertCommand.Parameters.AddWithValue("isActive", request.IsActive);

            var mappingIdObject = await insertCommand.ExecuteScalarAsync(cancellationToken);
            var mappingId = Convert.ToInt32(mappingIdObject);
            var createdMapping = await GetGroupRoleMappingByIdAsync(connection, mappingId, cancellationToken);
            await LogMappingAuditAsync(
                connection,
                actorUserId,
                "created",
                "directory_group_role_mapping",
                $"Mapping {createdMapping.MappingId} for directory group {createdMapping.DirectoryGroupId} created.",
                null,
                createdMapping,
                cancellationToken);
            return createdMapping;
        }
        catch (PostgresException ex) when (ex.SqlState == "42P01")
        {
            throw new InvalidOperationException("Directory tables are missing. Apply migration db/35_directory_tables.sql first.", ex);
        }
        catch (PostgresException ex) when (ex.SqlState == "23503")
        {
            throw new InvalidOperationException("Die ausgewählte Verzeichnisgruppe oder Rolle existiert nicht.", ex);
        }
    }

    public async Task<bool> DeleteGroupRoleMappingAsync(
        int mappingId,
        long? actorUserId = null,
        CancellationToken cancellationToken = default)
    {
        var connectionString = GetConnectionStringOrNull();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return false;
        }

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        try
        {
            var previousMapping = await GetGroupRoleMappingByIdOrNullAsync(connection, mappingId, cancellationToken);
            if (previousMapping is null)
            {
                return false;
            }

            const string sql = "DELETE FROM directory_group_role_mappings WHERE id = @mappingId;";
            await using var cmd = new NpgsqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("mappingId", mappingId);
            var affected = await cmd.ExecuteNonQueryAsync(cancellationToken);
            if (affected > 0)
            {
                await LogMappingAuditAsync(
                    connection,
                    actorUserId,
                    "deleted",
                    "directory_group_role_mapping",
                    $"Mapping {mappingId} deleted.",
                    previousMapping,
                    null,
                    cancellationToken);
            }
            return affected > 0;
        }
        catch (PostgresException ex) when (ex.SqlState == "42P01")
        {
            return false;
        }
    }

    private static async Task<int?> FindExistingMappingIdAsync(
        NpgsqlConnection connection,
        int directoryGroupId,
        int appRoleId,
        string scope,
        int? scopeDepartmentId,
        CancellationToken cancellationToken)
    {
        const string sql = @"
SELECT id
FROM directory_group_role_mappings
WHERE directory_group_id = @directoryGroupId
  AND app_role_id = @appRoleId
  AND scope = @scope
  AND (
      (scope_department_id IS NULL AND @scopeDepartmentId IS NULL)
      OR scope_department_id = @scopeDepartmentId
  )
ORDER BY id
LIMIT 1;";

        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("directoryGroupId", directoryGroupId);
        cmd.Parameters.AddWithValue("appRoleId", appRoleId);
        cmd.Parameters.AddWithValue("scope", scope);
        var scopeDepartmentParameter = cmd.Parameters.Add("scopeDepartmentId", NpgsqlDbType.Integer);
        scopeDepartmentParameter.Value = (object?)scopeDepartmentId ?? DBNull.Value;

        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        return result is null ? null : Convert.ToInt32(result);
    }

    private static async Task<AdminDirectoryGroupRoleMappingDto> GetGroupRoleMappingByIdAsync(
        NpgsqlConnection connection,
        int mappingId,
        CancellationToken cancellationToken)
    {
        const string sql = @"
SELECT
    dgrm.id,
    dgrm.directory_group_id,
    dgrm.app_role_id,
    r.role_key,
    r.name,
    r.role_kind,
    r.department_id,
    department.name,
    dgrm.scope,
    dgrm.scope_department_id,
    dgrm.is_active
FROM directory_group_role_mappings dgrm
JOIN app_roles r ON r.id = dgrm.app_role_id
LEFT JOIN departments department ON department.id = r.department_id
WHERE dgrm.id = @mappingId
LIMIT 1;";

        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("mappingId", mappingId);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException("Directory mapping not found.");
        }

        return new AdminDirectoryGroupRoleMappingDto
        {
            MappingId = reader.GetInt32(0),
            DirectoryGroupId = reader.GetInt32(1),
            AppRoleId = reader.GetInt32(2),
            AppRoleKey = reader.GetString(3),
            AppRoleName = reader.GetString(4),
            AppRoleKind = reader.GetString(5),
            RoleDepartmentId = reader.IsDBNull(6) ? null : reader.GetInt32(6),
            RoleDepartmentName = reader.IsDBNull(7) ? null : reader.GetString(7),
            Scope = reader.GetString(8),
            ScopeDepartmentId = reader.IsDBNull(9) ? null : reader.GetInt32(9),
            IsActive = reader.GetBoolean(10)
        };
    }

    private static async Task<AdminDirectoryGroupRoleMappingDto?> GetGroupRoleMappingByIdOrNullAsync(
        NpgsqlConnection connection,
        int mappingId,
        CancellationToken cancellationToken)
    {
        try
        {
            return await GetGroupRoleMappingByIdAsync(connection, mappingId, cancellationToken);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private static async Task LogMappingAuditAsync(
        NpgsqlConnection connection,
        long? actorUserId,
        string eventType,
        string entityType,
        string detail,
        AdminDirectoryGroupRoleMappingDto? oldValue,
        AdminDirectoryGroupRoleMappingDto? newValue,
        CancellationToken cancellationToken)
    {
        const string sql = @"
INSERT INTO directory_mapping_audit_log (actor_user_id, event_type, entity_type, detail, old_value, new_value, created_at)
VALUES (@actorUserId, @eventType, @entityType, @detail, CAST(@oldValue AS jsonb), CAST(@newValue AS jsonb), NOW());";

        await using var cmd = new NpgsqlCommand(sql, connection);
        var actorParameter = cmd.Parameters.Add("actorUserId", NpgsqlDbType.Bigint);
        actorParameter.Value = (object?)actorUserId ?? DBNull.Value;
        cmd.Parameters.AddWithValue("eventType", eventType);
        cmd.Parameters.AddWithValue("entityType", entityType);
        cmd.Parameters.AddWithValue("detail", detail);
        cmd.Parameters.AddWithValue("oldValue", (object?)SerializeJson(oldValue) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("newValue", (object?)SerializeJson(newValue) ?? DBNull.Value);
        try
        {
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (PostgresException ex) when (ex.SqlState == "42P01")
        {
            // Audit migration not applied yet.
        }
    }

    private static async Task<int> UpsertDirectoryGroup(
        NpgsqlConnection connection,
        Guid externalId,
        string displayName,
        string? description,
        CancellationToken cancellationToken)
    {
        const string sql = @"
INSERT INTO directory_groups (external_group_id, display_name, description, last_synced_at)
VALUES (@externalGroupId, @displayName, @description, NOW())
ON CONFLICT (external_group_id) DO UPDATE SET
    display_name = EXCLUDED.display_name,
    description = EXCLUDED.description,
    last_synced_at = NOW()
RETURNING id;";

        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("externalGroupId", externalId);
        cmd.Parameters.AddWithValue("displayName", displayName);
        cmd.Parameters.AddWithValue("description", (object?)description ?? DBNull.Value);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(cancellationToken));
    }

    private static async Task<long> UpsertDirectoryIdentity(
        NpgsqlConnection connection,
        Guid entraObjectId,
        Microsoft.Graph.Models.User user,
        CancellationToken cancellationToken)
    {
        const string sql = @"
INSERT INTO directory_identities (entra_object_id, user_principal_name, mail, display_name, account_enabled, last_synced_at)
VALUES (@entraObjectId, @userPrincipalName, @mail, @displayName, @accountEnabled, NOW())
ON CONFLICT (entra_object_id) DO UPDATE SET
    user_principal_name = EXCLUDED.user_principal_name,
    mail = EXCLUDED.mail,
    display_name = EXCLUDED.display_name,
    account_enabled = EXCLUDED.account_enabled,
    last_synced_at = NOW()
RETURNING id;";

        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("entraObjectId", entraObjectId);
        cmd.Parameters.AddWithValue("userPrincipalName", user.UserPrincipalName ?? user.Id ?? entraObjectId.ToString());
        cmd.Parameters.AddWithValue("mail", (object?)user.Mail ?? DBNull.Value);
        cmd.Parameters.AddWithValue("displayName", user.DisplayName ?? user.UserPrincipalName ?? entraObjectId.ToString());
        cmd.Parameters.AddWithValue("accountEnabled", user.AccountEnabled ?? true);
        return Convert.ToInt64(await cmd.ExecuteScalarAsync(cancellationToken));
    }

    private static async Task ClearGroupMemberships(
        NpgsqlConnection connection,
        int directoryGroupId,
        CancellationToken cancellationToken)
    {
        const string sql = "DELETE FROM directory_group_members WHERE directory_group_id = @directoryGroupId;";
        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("directoryGroupId", directoryGroupId);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertGroupMembership(
        NpgsqlConnection connection,
        int directoryGroupId,
        long directoryIdentityId,
        CancellationToken cancellationToken)
    {
        const string sql = @"
INSERT INTO directory_group_members (directory_group_id, directory_identity_id, synced_at)
VALUES (@directoryGroupId, @directoryIdentityId, NOW())
ON CONFLICT DO NOTHING;";

        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("directoryGroupId", directoryGroupId);
        cmd.Parameters.AddWithValue("directoryIdentityId", directoryIdentityId);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task AutoLinkIdentitiesToAppUsers(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        const string sql = @"
UPDATE directory_identities di
SET app_user_id = u.id
FROM app_users u
WHERE di.app_user_id IS NULL
  AND (
    (u.external_key IS NOT NULL AND u.external_key = di.entra_object_id::text)
    OR (LOWER(u.email) = LOWER(di.mail) AND di.mail IS NOT NULL)
  );";

        await using var cmd = new NpgsqlCommand(sql, connection);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task LogSyncRun(
        NpgsqlConnection connection,
        string syncType,
        string status,
        int groupsSynced,
        int identitiesSynced,
        int membershipsSynced,
        string? errorMessage,
        DateTime startedAt,
        CancellationToken cancellationToken)
    {
        const string sql = @"
INSERT INTO directory_sync_log (sync_type, status, groups_synced, identities_synced, memberships_synced, error_message, started_at, completed_at)
VALUES (@syncType, @status, @groupsSynced, @identitiesSynced, @membershipsSynced, @errorMessage, @startedAt, NOW());";

        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("syncType", syncType);
        cmd.Parameters.AddWithValue("status", status);
        cmd.Parameters.AddWithValue("groupsSynced", groupsSynced);
        cmd.Parameters.AddWithValue("identitiesSynced", identitiesSynced);
        cmd.Parameters.AddWithValue("membershipsSynced", membershipsSynced);
        cmd.Parameters.AddWithValue("errorMessage", (object?)errorMessage ?? DBNull.Value);
        cmd.Parameters.AddWithValue("startedAt", startedAt);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string? GetConnectionStringOrNull()
    {
        var connectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING");
        return string.IsNullOrWhiteSpace(connectionString) ? null : connectionString;
    }

    private static string NormalizeScope(string? value)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? "global" : value.Trim().ToLowerInvariant();
        return normalized.Length == 0 ? "global" : normalized;
    }

    private static bool MatchesGroupPrefix(string? displayName, string? prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            return false;
        }

        return displayName.Contains(prefix, StringComparison.OrdinalIgnoreCase);
    }

    private static string? ResolveEffectiveGroupPrefix(string? configuredPrefix, string? overridePrefix)
    {
        if (overridePrefix is null)
        {
            return configuredPrefix;
        }

        var normalizedOverride = Normalize(overridePrefix);
        return normalizedOverride;
    }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string? SerializeJson<T>(T? value)
    {
        return value is null ? null : JsonSerializer.Serialize(value);
    }
}
