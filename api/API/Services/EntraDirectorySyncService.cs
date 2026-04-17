using Azure.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Models;
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
    private readonly IGraphApplicationConfigurationService _graphApplicationConfigurationService;
    private readonly LifecycleRuntimeSettings _runtimeSettings;
    private readonly ISystemEventLogService _systemEventLogService;
    private readonly ILogger<EntraDirectorySyncService> _logger;

    public EntraDirectorySyncService(
        IGraphApplicationConfigurationService graphApplicationConfigurationService,
        LifecycleRuntimeSettings runtimeSettings,
        ISystemEventLogService systemEventLogService,
        ILogger<EntraDirectorySyncService> logger)
    {
        _graphApplicationConfigurationService = graphApplicationConfigurationService;
        _runtimeSettings = runtimeSettings;
        _systemEventLogService = systemEventLogService;
        _logger = logger;
    }

    public async Task<DirectorySyncResult> SyncAllAsync(
        string? groupPrefixOverride = null,
        CancellationToken cancellationToken = default)
    {
        var startedAt = DateTime.UtcNow;
        var connectionString = _runtimeSettings.ConnectionString;
        var configuredGroupPrefix = _runtimeSettings.DirectoryGroupPrefix;
        var explicitGroupIds = ParseExplicitGroupIds(_runtimeSettings.DirectoryExplicitGroupIds);
        var effectiveGroupPrefix = ResolveEffectiveGroupPrefix(configuredGroupPrefix, groupPrefixOverride);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            await _systemEventLogService.WriteAsync(new SystemEventLogWriteModel
            {
                Severity = "error",
                Source = "entra",
                Category = "configuration",
                EventKey = "directory_sync_missing_connection_string",
                Message = "Directory sync failed because ConnectionStrings:Default is not configured."
            }, cancellationToken);
            return new DirectorySyncResult
            {
                Status = "failed",
                ErrorMessage = "ConnectionStrings:Default not configured.",
                AppliedGroupPrefix = effectiveGroupPrefix
            };
        }

        GraphServiceClient graphClient;
        try
        {
            var credentials = await ResolveGraphCredentialsAsync(cancellationToken);
            if (credentials is null)
            {
                await _systemEventLogService.WriteAsync(new SystemEventLogWriteModel
                {
                    Severity = "error",
                    Source = "entra",
                    Category = "configuration",
                    EventKey = "directory_sync_missing_graph_credentials",
                    Message = "Directory sync failed because Graph credentials are not configured."
                }, cancellationToken);
                return new DirectorySyncResult
                {
                    Status = "failed",
                    ErrorMessage =
                        "Graph credentials not configured. Set ENTRA_TENANT_ID, ENTRA_CLIENT_ID and ENTRA_CLIENT_SECRET or GRAPH_CLIENT_SECRET via environment variables or your secret store.",
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
            await _systemEventLogService.WriteAsync(new SystemEventLogWriteModel
            {
                Severity = "error",
                Source = "entra",
                Category = "graph",
                EventKey = "directory_sync_graph_client_failed",
                Message = $"Failed to create Graph client for directory sync: {ex.Message}",
                Details = new { error = ex.Message, exceptionType = ex.GetType().FullName }
            }, cancellationToken);
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
            var groups = await LoadSecurityGroupsAsync(graphClient, cancellationToken);

            foreach (var group in groups)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!ShouldSyncGroup(group.Id, group.DisplayName, effectiveGroupPrefix, explicitGroupIds))
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
                    var members = await LoadGroupMembersAsync(graphClient, group.Id, cancellationToken);

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
                    await _systemEventLogService.WriteAsync(new SystemEventLogWriteModel
                    {
                        Severity = "warning",
                        Source = "directory",
                        Category = "sync",
                        EventKey = "directory_group_member_sync_failed",
                        Message = $"Failed to sync members for group {group.DisplayName ?? group.Id}: {ex.Message}",
                        Details = new
                        {
                            groupId = group.Id,
                            groupName = group.DisplayName,
                            error = ex.Message
                        }
                    }, cancellationToken);
                    status = "partial";
                    errorMessage ??= $"Some group members could not be synced: {ex.Message}";
                }
            }

            await AutoLinkIdentitiesToAppUsers(connection, cancellationToken);
            await EnsureDirectoryDepartmentsExist(connection, cancellationToken);
            await UpsertProjectedAppUsersFromDirectory(connection, cancellationToken);
            if (_runtimeSettings.DevSimulationEnabled)
            {
                await EnsureDevelopmentDefaultGroupMappings(connection, cancellationToken);
            }
            await UpdateDirectoryUserActivationStates(connection, cancellationToken);
            await SyncDepartmentLeadAssignmentsFromDirectory(connection, cancellationToken);
        }
        catch (PostgresException ex) when (ex.SqlState == "42P01")
        {
            status = "failed";
            errorMessage = "Directory tables are missing. Apply migrations db/35_directory_tables.sql and db/38_permission_model.sql first.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Directory sync failed.");
            status = "failed";
            errorMessage = ex.Message;
            await _systemEventLogService.WriteAsync(new SystemEventLogWriteModel
            {
                Severity = "error",
                Source = "directory",
                Category = "sync",
                EventKey = "directory_sync_failed",
                Message = $"Directory sync failed: {ex.Message}",
                Details = new { error = ex.Message, exceptionType = ex.GetType().FullName }
            }, cancellationToken);
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

        await _systemEventLogService.WriteAsync(new SystemEventLogWriteModel
        {
            Severity = string.Equals(status, "failed", StringComparison.OrdinalIgnoreCase)
                ? "error"
                : string.Equals(status, "partial", StringComparison.OrdinalIgnoreCase)
                    ? "warning"
                    : "info",
            Source = "directory",
            Category = "sync",
            EventKey = string.Equals(status, "success", StringComparison.OrdinalIgnoreCase)
                ? "directory_sync_succeeded"
                : string.Equals(status, "partial", StringComparison.OrdinalIgnoreCase)
                    ? "directory_sync_partial"
                    : "directory_sync_failed",
            Message = $"Directory sync finished with status {status}.",
            Details = new
            {
                status,
                groupsSynced,
                identitiesSynced,
                membershipsSynced,
                errorMessage,
                appliedGroupPrefix = effectiveGroupPrefix
            }
        }, cancellationToken);

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

    private static async Task<List<Group>> LoadSecurityGroupsAsync(
        GraphServiceClient graphClient,
        CancellationToken cancellationToken)
    {
        var groups = new List<Group>();

        var response = await graphClient.Groups.GetAsync(config =>
        {
            config.QueryParameters.Select = ["id", "displayName", "description", "securityEnabled"];
            config.QueryParameters.Filter = "securityEnabled eq true";
            config.QueryParameters.Top = 999;
        }, cancellationToken);

        while (response is not null)
        {
            groups.AddRange(response.Value ?? []);

            var nextLink = response.OdataNextLink;
            if (string.IsNullOrWhiteSpace(nextLink))
            {
                break;
            }

            response = await graphClient.Groups.WithUrl(nextLink).GetAsync(cancellationToken: cancellationToken);
        }

        return groups;
    }

    private static async Task<List<DirectoryObject>> LoadGroupMembersAsync(
        GraphServiceClient graphClient,
        string groupId,
        CancellationToken cancellationToken)
    {
        var members = new List<DirectoryObject>();

        var response = await graphClient.Groups[groupId].Members.GetAsync(config =>
        {
            config.QueryParameters.Select = ["id", "displayName", "mail", "userPrincipalName", "accountEnabled", "department"];
            config.QueryParameters.Top = 999;
        }, cancellationToken);

        while (response is not null)
        {
            members.AddRange(response.Value ?? []);

            var nextLink = response.OdataNextLink;
            if (string.IsNullOrWhiteSpace(nextLink))
            {
                break;
            }

            response = await graphClient.Groups[groupId].Members.WithUrl(nextLink).GetAsync(cancellationToken: cancellationToken);
        }

        return members;
    }

    private async Task<(string TenantId, string ClientId, string ClientSecret)?> ResolveGraphCredentialsAsync(
        CancellationToken cancellationToken)
    {
        var configuration = await _graphApplicationConfigurationService.GetRuntimeConfiguration(cancellationToken);
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
        var connectionString = _runtimeSettings.ConnectionString;
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
                    ConfiguredGroupPrefix = _runtimeSettings.DirectoryGroupPrefix
                };
            }
        }
        catch (PostgresException ex) when (ex.SqlState == "42P01")
        {
            // Migration not applied yet.
        }

        return new DirectorySyncStatusDto
        {
            ConfiguredGroupPrefix = _runtimeSettings.DirectoryGroupPrefix
        };
    }

    public async Task<List<AdminDirectoryGroupDto>> GetGroupsAsync(CancellationToken cancellationToken = default)
    {
        var connectionString = _runtimeSettings.ConnectionString;
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
        var connectionString = _runtimeSettings.ConnectionString;
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
        var connectionString = _runtimeSettings.ConnectionString;
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
        var connectionString = _runtimeSettings.ConnectionString;
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("ConnectionStrings:Default is not configured.");
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
        var connectionString = _runtimeSettings.ConnectionString;
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

    private static async Task LogDirectoryAuditEventAsync(
        NpgsqlConnection connection,
        long? actorUserId,
        string eventType,
        string entityType,
        string detail,
        object? oldValue,
        object? newValue,
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

    private static DepartmentLeadAuditSnapshot CreateDepartmentLeadAuditSnapshot(
        DepartmentLeadSyncState state,
        long? resolvedPersonId = null,
        string? resolvedDisplayName = null,
        string? syncState = null,
        IReadOnlyList<string>? candidateNames = null)
    {
        return new DepartmentLeadAuditSnapshot
        {
            DepartmentId = state.DepartmentId,
            DepartmentName = state.DepartmentName,
            DepartmentLeadPersonId = resolvedPersonId ?? state.CurrentDepartmentLeadPersonId,
            RequirementApproverPersonId = resolvedPersonId ?? state.CurrentRequirementApproverPersonId,
            ResolvedDisplayName = resolvedDisplayName,
            SyncState = syncState ?? (state.Candidates.Count switch
            {
                0 => "missing",
                1 => "resolved",
                _ => "conflict"
            }),
            CandidateNames = candidateNames ?? state.Candidates.Select(candidate => candidate.DisplayName).ToArray()
        };
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
INSERT INTO directory_identities (entra_object_id, user_principal_name, mail, display_name, account_enabled, department_name, last_synced_at)
VALUES (@entraObjectId, @userPrincipalName, @mail, @displayName, @accountEnabled, @departmentName, NOW())
ON CONFLICT (entra_object_id) DO UPDATE SET
    user_principal_name = EXCLUDED.user_principal_name,
    mail = EXCLUDED.mail,
    display_name = EXCLUDED.display_name,
    account_enabled = EXCLUDED.account_enabled,
    department_name = EXCLUDED.department_name,
    last_synced_at = NOW()
RETURNING id;";

        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("entraObjectId", entraObjectId);
        cmd.Parameters.AddWithValue("userPrincipalName", user.UserPrincipalName ?? user.Id ?? entraObjectId.ToString());
        cmd.Parameters.AddWithValue("mail", (object?)user.Mail ?? DBNull.Value);
        cmd.Parameters.AddWithValue("displayName", user.DisplayName ?? user.UserPrincipalName ?? entraObjectId.ToString());
        cmd.Parameters.AddWithValue("accountEnabled", user.AccountEnabled ?? true);
        cmd.Parameters.AddWithValue("departmentName", (object?)Normalize(user.Department) ?? DBNull.Value);
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

        return displayName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
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

    private static HashSet<Guid> ParseExplicitGroupIds(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(item => Guid.TryParse(item, out var parsed) ? parsed : (Guid?)null)
            .Where(item => item.HasValue)
            .Select(item => item!.Value)
            .ToHashSet();
    }

    private static bool ShouldSyncGroup(
        string? groupId,
        string? displayName,
        string? prefix,
        IReadOnlySet<Guid> explicitGroupIds)
    {
        if (!string.IsNullOrWhiteSpace(groupId)
            && Guid.TryParse(groupId, out var parsedGroupId)
            && explicitGroupIds.Contains(parsedGroupId))
        {
            return true;
        }

        if (explicitGroupIds.Count > 0 && string.IsNullOrWhiteSpace(prefix))
        {
            return false;
        }

        return MatchesGroupPrefix(displayName, prefix);
    }

    private static async Task EnsureDirectoryDepartmentsExist(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        const string sql = @"
INSERT INTO departments (name)
SELECT DISTINCT BTRIM(di.department_name)
FROM directory_identities di
WHERE di.department_name IS NOT NULL
  AND BTRIM(di.department_name) <> ''
ON CONFLICT (name) DO NOTHING;";

        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task UpsertProjectedAppUsersFromDirectory(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        const string sql = @"
WITH scoped_identities AS (
    SELECT DISTINCT
        di.id AS directory_identity_id,
        di.app_user_id,
        di.entra_object_id,
        di.user_principal_name,
        di.mail,
        COALESCE(di.mail, di.user_principal_name, di.entra_object_id::text || '@directory.local') AS resolved_email,
        di.display_name,
        di.department_name,
        di.account_enabled,
        department.id AS department_id
    FROM directory_identities di
    JOIN directory_group_members dgm ON dgm.directory_identity_id = di.id
    JOIN directory_groups dg ON dg.id = dgm.directory_group_id
    LEFT JOIN departments department ON LOWER(department.name) = LOWER(di.department_name)
),
insert_candidates AS (
    SELECT DISTINCT ON (LOWER(scoped.resolved_email))
        scoped.entra_object_id,
        scoped.department_id,
        scoped.display_name,
        scoped.resolved_email,
        scoped.account_enabled
    FROM scoped_identities scoped
    WHERE scoped.app_user_id IS NULL
      AND NOT EXISTS (
        SELECT 1
        FROM app_users existing
        WHERE existing.entra_object_id = scoped.entra_object_id
           OR LOWER(existing.email) = LOWER(scoped.resolved_email)
    )
    ORDER BY LOWER(scoped.resolved_email), scoped.directory_identity_id
),
upserted_users AS (
    INSERT INTO app_users (
        external_key,
        entra_object_id,
        department_id,
        display_name,
        email,
        notification_email,
        is_active,
        directory_synced,
        last_directory_synced_at,
        department_source,
        department_override_active
    )
    SELECT
        scoped.entra_object_id::text,
        scoped.entra_object_id,
        scoped.department_id,
        scoped.display_name,
        scoped.resolved_email,
        NULL,
        scoped.account_enabled,
        TRUE,
        NOW(),
        CASE
            WHEN scoped.department_id IS NULL THEN 'unassigned'
            ELSE 'directory'
        END,
        FALSE
    FROM insert_candidates scoped
    RETURNING id, entra_object_id
)
UPDATE app_users u
SET
    external_key = di.entra_object_id::text,
    entra_object_id = di.entra_object_id,
    display_name = di.display_name,
    email = COALESCE(di.mail, di.user_principal_name, u.email),
    directory_synced = TRUE,
    last_directory_synced_at = NOW(),
    department_id = CASE
        WHEN u.department_override_active = TRUE THEN u.department_id
        ELSE department.id
    END,
    department_source = CASE
        WHEN u.department_override_active = TRUE THEN 'override'
        WHEN department.id IS NULL THEN 'unassigned'
        ELSE 'directory'
    END
FROM directory_identities di
LEFT JOIN departments department ON LOWER(department.name) = LOWER(di.department_name)
WHERE u.entra_object_id = di.entra_object_id;";

        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);

        await AutoLinkIdentitiesToAppUsers(connection, cancellationToken);

        const string linkSql = @"
UPDATE directory_identities di
SET app_user_id = u.id
FROM app_users u
WHERE u.entra_object_id = di.entra_object_id
  AND di.app_user_id IS DISTINCT FROM u.id;";

        await using var linkCommand = new NpgsqlCommand(linkSql, connection);
        await linkCommand.ExecuteNonQueryAsync(cancellationToken);

        const string personSql = @"
INSERT INTO people (app_user_id, department_id, directory_identity_id, updated_at)
SELECT
    u.id,
    u.department_id,
    di.id,
    NOW()
FROM app_users u
JOIN directory_identities di ON di.app_user_id = u.id
ON CONFLICT (app_user_id) DO UPDATE
SET
    department_id = EXCLUDED.department_id,
    directory_identity_id = EXCLUDED.directory_identity_id,
    updated_at = NOW();";

        await using var personCommand = new NpgsqlCommand(personSql, connection);
        await personCommand.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task UpdateDirectoryUserActivationStates(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        const string sql = @"
WITH role_based_access AS (
    SELECT DISTINCT di.app_user_id AS user_id
    FROM directory_identities di
    JOIN directory_group_members dgm ON dgm.directory_identity_id = di.id
    JOIN directory_group_role_mappings dgrm ON dgrm.directory_group_id = dgm.directory_group_id
    JOIN app_role_permissions rp ON rp.app_role_id = dgrm.app_role_id
    JOIN app_permissions p ON p.id = rp.app_permission_id
    WHERE di.app_user_id IS NOT NULL
      AND di.account_enabled = TRUE
      AND dgrm.is_active = TRUE
      AND p.permission_key = 'app.access'
      AND p.is_active = TRUE
),
override_allow AS (
    SELECT DISTINCT upo.app_user_id AS user_id
    FROM app_user_permission_overrides upo
    JOIN app_permissions p ON p.id = upo.app_permission_id
    WHERE p.permission_key = 'app.access'
      AND upo.effect = 'allow'
),
override_deny AS (
    SELECT DISTINCT upo.app_user_id AS user_id
    FROM app_user_permission_overrides upo
    JOIN app_permissions p ON p.id = upo.app_permission_id
    WHERE p.permission_key = 'app.access'
      AND upo.effect = 'deny'
)
UPDATE app_users u
SET is_active = (
    di.account_enabled = TRUE
    AND (
        u.id IN (SELECT user_id FROM role_based_access)
        OR u.id IN (SELECT user_id FROM override_allow)
    )
    AND u.id NOT IN (SELECT user_id FROM override_deny)
)
FROM directory_identities di
WHERE di.app_user_id = u.id
  AND u.directory_synced = TRUE;";

        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task SyncDepartmentLeadAssignmentsFromDirectory(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        var states = await LoadDepartmentLeadSyncStatesAsync(connection, cancellationToken);
        foreach (var state in states)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (state.Candidates.Count == 1)
            {
                var candidate = state.Candidates[0];
                var personId = await EnsureDirectoryManagedPersonRecordAsync(connection, candidate.AppUserId, cancellationToken);
                var alreadyResolved = state.CurrentDepartmentLeadPersonId == personId
                    && state.CurrentRequirementApproverPersonId == personId;

                if (alreadyResolved)
                {
                    continue;
                }

                await UpsertDepartmentLeadAssignmentAsync(connection, state.DepartmentId, personId, cancellationToken);
                await LogDirectoryAuditEventAsync(
                    connection,
                    actorUserId: null,
                    eventType: "department_lead_synced",
                    entityType: "department_assignment",
                    detail: $"Abteilung {state.DepartmentName} ({state.DepartmentId}) wurde aus Entra auf {candidate.DisplayName} synchronisiert.",
                    oldValue: CreateDepartmentLeadAuditSnapshot(state),
                    newValue: CreateDepartmentLeadAuditSnapshot(state, personId, candidate.DisplayName, "resolved"),
                    cancellationToken);
                await _systemEventLogService.WriteAsync(new SystemEventLogWriteModel
                {
                    Severity = "info",
                    Source = "directory",
                    Category = "department_lead",
                    EventKey = "department_lead_synced",
                    Message = $"Department lead resolved for {state.DepartmentName}: {candidate.DisplayName}.",
                    EntityType = "department",
                    EntityId = state.DepartmentId.ToString(),
                    Details = new { state.DepartmentId, state.DepartmentName, candidate.DisplayName }
                }, cancellationToken);
                continue;
            }

            if (state.Candidates.Count == 0)
            {
                if (state.CurrentDepartmentLeadPersonId.HasValue || state.CurrentRequirementApproverPersonId.HasValue)
                {
                    await ClearDepartmentLeadAssignmentAsync(connection, state.DepartmentId, cancellationToken);
                    await LogDirectoryAuditEventAsync(
                        connection,
                        actorUserId: null,
                        eventType: "department_lead_cleared",
                        entityType: "department_assignment",
                        detail: $"Abteilung {state.DepartmentName} ({state.DepartmentId}) hat keine eindeutige Entra-Abteilungsleitung mehr.",
                        oldValue: CreateDepartmentLeadAuditSnapshot(state),
                        newValue: CreateDepartmentLeadAuditSnapshot(state, null, null, "missing"),
                        cancellationToken);
                    await _systemEventLogService.WriteAsync(new SystemEventLogWriteModel
                    {
                        Severity = "warning",
                        Source = "directory",
                        Category = "department_lead",
                        EventKey = "department_lead_missing",
                        Message = $"Department lead missing for {state.DepartmentName}.",
                        EntityType = "department",
                        EntityId = state.DepartmentId.ToString(),
                        Details = new { state.DepartmentId, state.DepartmentName }
                    }, cancellationToken);
                }

                continue;
            }

            if (state.CurrentDepartmentLeadPersonId.HasValue || state.CurrentRequirementApproverPersonId.HasValue)
            {
                await ClearDepartmentLeadAssignmentAsync(connection, state.DepartmentId, cancellationToken);
            }

            await LogDirectoryAuditEventAsync(
                connection,
                actorUserId: null,
                eventType: "department_lead_conflict",
                entityType: "department_assignment",
                detail: $"Abteilung {state.DepartmentName} ({state.DepartmentId}) hat mehrere Entra-Abteilungsleitungen: {string.Join(", ", state.Candidates.Select(candidate => candidate.DisplayName))}.",
                oldValue: CreateDepartmentLeadAuditSnapshot(state),
                newValue: CreateDepartmentLeadAuditSnapshot(
                    state,
                    null,
                    null,
                    "conflict",
                    state.Candidates.Select(candidate => candidate.DisplayName).ToArray()),
                cancellationToken);
            await _systemEventLogService.WriteAsync(new SystemEventLogWriteModel
            {
                Severity = "warning",
                Source = "directory",
                Category = "department_lead",
                EventKey = "department_lead_conflict",
                Message = $"Department lead conflict for {state.DepartmentName}.",
                EntityType = "department",
                EntityId = state.DepartmentId.ToString(),
                Details = new
                {
                    state.DepartmentId,
                    state.DepartmentName,
                    candidateNames = state.Candidates.Select(candidate => candidate.DisplayName).ToArray()
                }
            }, cancellationToken);
        }
    }

    private static async Task<List<DepartmentLeadSyncState>> LoadDepartmentLeadSyncStatesAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        const string sql = @"
SELECT
    d.id,
    d.name,
    ds.department_lead_person_id,
    ds.requirement_approver_person_id,
    candidate.app_user_id,
    candidate.display_name
FROM departments d
LEFT JOIN department_settings ds ON ds.department_id = d.id
LEFT JOIN (
    SELECT DISTINCT
        u.department_id,
        u.id AS app_user_id,
        u.display_name
    FROM app_users u
    JOIN directory_identities di ON di.app_user_id = u.id
    JOIN directory_group_members dgm ON dgm.directory_identity_id = di.id
    JOIN directory_group_role_mappings dgrm ON dgrm.directory_group_id = dgm.directory_group_id
    JOIN app_roles ar ON ar.id = dgrm.app_role_id
    WHERE u.directory_synced = TRUE
      AND u.is_active = TRUE
      AND di.account_enabled = TRUE
      AND u.department_override_active = FALSE
      AND u.department_id IS NOT NULL
      AND dgrm.is_active = TRUE
      AND ar.role_key = 'auth_manager'
      AND ar.role_kind = 'system'
) candidate ON candidate.department_id = d.id
ORDER BY d.id, candidate.display_name, candidate.app_user_id;";

        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var states = new List<DepartmentLeadSyncState>();
        DepartmentLeadSyncState? current = null;
        while (await reader.ReadAsync(cancellationToken))
        {
            var departmentId = reader.GetInt32(0);
            if (current is null || current.DepartmentId != departmentId)
            {
                current = new DepartmentLeadSyncState(
                    departmentId,
                    reader.GetString(1),
                    reader.IsDBNull(2) ? null : reader.GetInt64(2),
                    reader.IsDBNull(3) ? null : reader.GetInt64(3),
                    []);
                states.Add(current);
            }

            if (!reader.IsDBNull(4))
            {
                current.Candidates.Add(new DepartmentLeadSyncCandidate(
                    reader.GetInt64(4),
                    reader.GetString(5)));
            }
        }

        return states;
    }

    private static async Task<long> EnsureDirectoryManagedPersonRecordAsync(
        NpgsqlConnection connection,
        long userId,
        CancellationToken cancellationToken)
    {
        const string sql = @"
INSERT INTO people (app_user_id, department_id, directory_identity_id, updated_at)
SELECT
    u.id,
    u.department_id,
    latest_identity.id,
    NOW()
FROM app_users u
LEFT JOIN LATERAL (
    SELECT di.id
    FROM directory_identities di
    WHERE di.app_user_id = u.id
    ORDER BY di.last_synced_at DESC NULLS LAST, di.id DESC
    LIMIT 1
) latest_identity ON TRUE
WHERE u.id = @userId
ON CONFLICT (app_user_id) DO UPDATE
SET
    department_id = EXCLUDED.department_id,
    directory_identity_id = COALESCE(EXCLUDED.directory_identity_id, people.directory_identity_id),
    updated_at = NOW()
RETURNING id;";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("userId", userId);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is long longId
            ? longId
            : result is int intId
                ? intId
                : throw new InvalidOperationException($"Person record for app user {userId} could not be synchronized.");
    }

    private static async Task UpsertDepartmentLeadAssignmentAsync(
        NpgsqlConnection connection,
        int departmentId,
        long personId,
        CancellationToken cancellationToken)
    {
        const string sql = @"
INSERT INTO department_settings (
    department_id,
    department_lead_person_id,
    requirement_approver_person_id,
    updated_at
)
VALUES (
    @departmentId,
    @personId,
    @personId,
    NOW()
)
ON CONFLICT (department_id) DO UPDATE
SET
    department_lead_person_id = EXCLUDED.department_lead_person_id,
    requirement_approver_person_id = EXCLUDED.requirement_approver_person_id,
    updated_at = NOW();";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("departmentId", departmentId);
        command.Parameters.AddWithValue("personId", personId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task ClearDepartmentLeadAssignmentAsync(
        NpgsqlConnection connection,
        int departmentId,
        CancellationToken cancellationToken)
    {
        const string sql = @"
DELETE FROM department_settings
WHERE department_id = @departmentId;";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("departmentId", departmentId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task EnsureDevelopmentDefaultGroupMappings(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        const string sql = @"
WITH mapping_seed(group_name, role_key, scope) AS (
    VALUES
        ('Onboarding-App-Admins', 'auth_admin', 'global'),
        ('Onboarding-App-HR', 'auth_hr', 'global'),
        ('Onboarding-App-Managers', 'auth_manager', 'global'),
        ('Onboarding-App-Access', 'auth_reader', 'global')
)
INSERT INTO directory_group_role_mappings (
    directory_group_id,
    app_role_id,
    scope,
    scope_department_id,
    is_active
)
SELECT
    dg.id,
    ar.id,
    seed.scope,
    NULL,
    TRUE
FROM mapping_seed seed
JOIN directory_groups dg ON dg.display_name = seed.group_name
JOIN app_roles ar ON ar.role_key = seed.role_key
ON CONFLICT (directory_group_id, app_role_id, scope, COALESCE(scope_department_id, -1))
DO UPDATE SET is_active = TRUE;";

        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string? SerializeJson<T>(T? value)
    {
        return value is null ? null : JsonSerializer.Serialize(value);
    }

    private sealed record DepartmentLeadSyncCandidate(long AppUserId, string DisplayName);

    private sealed record DepartmentLeadSyncState(
        int DepartmentId,
        string DepartmentName,
        long? CurrentDepartmentLeadPersonId,
        long? CurrentRequirementApproverPersonId,
        List<DepartmentLeadSyncCandidate> Candidates);

    private sealed class DepartmentLeadAuditSnapshot
    {
        public required int DepartmentId { get; init; }
        public required string DepartmentName { get; init; }
        public long? DepartmentLeadPersonId { get; init; }
        public long? RequirementApproverPersonId { get; init; }
        public string? ResolvedDisplayName { get; init; }
        public required string SyncState { get; init; }
        public IReadOnlyList<string> CandidateNames { get; init; } = [];
    }
}
