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
            await EnsureDirectoryProjectionUserColumnsAsync(connection, cancellationToken);

            var groups = await LoadSecurityGroupsAsync(graphClient, cancellationToken);
            var matchedGroups = groups
                .Where(group => ShouldSyncGroup(group.Id, group.DisplayName, effectiveGroupPrefix, explicitGroupIds))
                .ToList();

            await _systemEventLogService.WriteAsync(new SystemEventLogWriteModel
            {
                Severity = "info",
                Source = "entra",
                Category = "sync",
                EventKey = "entra_groups_selected",
                Message = $"Entra sync selected {matchedGroups.Count} security groups for import.",
                Details = new
                {
                    selectionMode = string.IsNullOrWhiteSpace(effectiveGroupPrefix) ? "all_security_groups" : "prefix_or_explicit_ids",
                    appliedGroupPrefix = effectiveGroupPrefix,
                    explicitGroupIds = explicitGroupIds.Select(id => id.ToString()).ToArray(),
                    selectedGroupCount = matchedGroups.Count,
                    selectedGroups = matchedGroups
                        .Take(12)
                        .Select(group => new
                        {
                            groupId = group.Id,
                            displayName = group.DisplayName,
                            description = group.Description
                        })
                        .ToArray()
                }
            }, cancellationToken);

            foreach (var group in matchedGroups)
            {
                cancellationToken.ThrowIfCancellationRequested();

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

            var departmentSyncResult = await EnsureDirectoryDepartmentsExist(connection, cancellationToken);
            await _systemEventLogService.WriteAsync(new SystemEventLogWriteModel
            {
                Severity = "info",
                Source = "directory",
                Category = "department_projection",
                EventKey = "directory_departments_projected",
                Message = $"Directory sync projected {departmentSyncResult.ObservedDepartmentCount} department names and created {departmentSyncResult.CreatedDepartmentCount} local departments.",
                Details = new
                {
                    origin = "departments.name is created from directory_identities.department_name",
                    departmentSyncResult.ObservedDepartmentCount,
                    departmentSyncResult.CreatedDepartmentCount,
                    departmentSyncResult.ObservedDepartmentNames,
                    departmentSyncResult.CreatedDepartmentNames
                }
            }, cancellationToken);

            var appUserProjectionResult = await UpdateExistingAppUsersFromDirectory(connection, startedAt, cancellationToken);
            await _systemEventLogService.WriteAsync(new SystemEventLogWriteModel
            {
                Severity = "info",
                Source = "directory",
                Category = "user_projection",
                EventKey = "directory_users_updated",
                Message = $"Directory sync updated {appUserProjectionResult.TouchedUserCount} existing app users. New identities are not auto-imported — use POST /admin/directory/import.",
                Details = new
                {
                    origin = "Only existing app_users are updated. New directory_identities without app_user_id require explicit admin import.",
                    appUserProjectionResult.TouchedUserCount,
                    appUserProjectionResult.DirectoryAssignedUserCount,
                    appUserProjectionResult.OverrideUserCount,
                    appUserProjectionResult.UnassignedUserCount,
                    appUserProjectionResult.LinkedIdentityCount,
                    sampleUsers = appUserProjectionResult.SampleUsers
                }
            }, cancellationToken);
            if (_runtimeSettings.DevSimulationEnabled)
            {
                await EnsureDevelopmentDefaultGroupMappings(connection, cancellationToken);
            }
            var activationChanges = await UpdateDirectoryUserActivationStates(connection, cancellationToken);
            foreach (var change in activationChanges)
            {
                await _systemEventLogService.WriteAsync(new SystemEventLogWriteModel
                {
                    Severity = change.IsNowActive ? "info" : "warning",
                    Source = "directory",
                    Category = "user_activation",
                    EventKey = change.IsNowActive ? "directory_user_reactivated" : "directory_user_deactivated",
                    Message = change.IsNowActive
                        ? $"Benutzer {change.DisplayName} wurde durch den Verzeichnis-Sync reaktiviert."
                        : $"Benutzer {change.DisplayName} wurde durch den Verzeichnis-Sync deaktiviert (kein Entra-Zugriff mehr).",
                    EntityType = "app_user",
                    EntityId = change.UserId.ToString(),
                    Details = new
                    {
                        change.UserId,
                        change.DisplayName,
                        change.Email,
                        change.WasActive,
                        change.IsNowActive,
                        reason = change.IsNowActive
                            ? "User regained access via Entra group membership or permission override"
                            : "User lost all Entra group memberships granting app.access, or account was disabled in Entra"
                    }
                }, cancellationToken);
            }

            await _systemEventLogService.WriteAsync(new SystemEventLogWriteModel
            {
                Severity = "info",
                Source = "directory",
                Category = "department_lead",
                EventKey = "directory_department_assignments_skipped",
                Message = "Directory sync skipped automatic department lead assignment. Responsibilities are now managed manually by admins.",
                Details = new
                {
                    reason = "Automatic department lead resolution from Entra group memberships was disabled. Assignments in department_settings are no longer modified by the sync cycle."
                }
            }, cancellationToken);
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
            config.QueryParameters.Select = ["id", "displayName", "mail", "userPrincipalName", "accountEnabled", "department", "employeeId"];
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

    public async Task<DirectoryResponsibilityGapsDto> GetResponsibilityGapsAsync(
        CancellationToken cancellationToken = default)
    {
        var connectionString = _runtimeSettings.ConnectionString;
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return new DirectoryResponsibilityGapsDto { Gaps = [], TotalUnassignedDepartments = 0, TotalCandidatesNotYetAssigned = 0 };
        }

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        // Returns departments that have active auth_manager candidates in Entra groups,
        // together with the currently assigned department lead (if any).
        // Used to surface "people in Entra groups without a responsibility assignment" to admins.
        const string sql = @"
SELECT
    d.id              AS department_id,
    d.name            AS department_name,
    ds.department_lead_person_id,
    candidate.app_user_id,
    candidate.display_name,
    candidate.mail,
    candidate.group_name
FROM departments d
LEFT JOIN department_settings ds ON ds.department_id = d.id
JOIN (
    SELECT DISTINCT
        u.department_id,
        u.id              AS app_user_id,
        u.display_name,
        di.mail,
        dg.display_name   AS group_name
    FROM app_users u
    JOIN directory_identities di           ON di.app_user_id = u.id
    JOIN directory_group_members dgm       ON dgm.directory_identity_id = di.id
    JOIN directory_groups dg               ON dg.id = dgm.directory_group_id
    JOIN directory_group_role_mappings dgrm ON dgrm.directory_group_id = dgm.directory_group_id
    JOIN app_roles ar                      ON ar.id = dgrm.app_role_id
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

        var entryMap = new Dictionary<int, DirectoryResponsibilityGapEntry>();

        while (await reader.ReadAsync(cancellationToken))
        {
            var departmentId = reader.GetInt32(0);

            if (!entryMap.TryGetValue(departmentId, out var entry))
            {
                entry = new DirectoryResponsibilityGapEntry
                {
                    DepartmentId = departmentId,
                    DepartmentName = reader.GetString(1),
                    AssignedLeadPersonId = reader.IsDBNull(2) ? null : reader.GetInt64(2),
                    EntraGroupName = reader.IsDBNull(6) ? null : reader.GetString(6),
                    Candidates = []
                };
                entryMap[departmentId] = entry;
            }

            if (!reader.IsDBNull(3))
            {
                entry.Candidates.Add(new DirectoryResponsibilityCandidate
                {
                    AppUserId = reader.GetInt64(3),
                    DisplayName = reader.GetString(4),
                    Mail = reader.IsDBNull(5) ? null : reader.GetString(5)
                });
            }
        }

        var gaps = entryMap.Values.OrderBy(e => e.DepartmentName).ToList();
        var unassigned = gaps.Where(g => g.AssignedLeadPersonId is null).ToList();

        return new DirectoryResponsibilityGapsDto
        {
            Gaps = gaps,
            TotalUnassignedDepartments = unassigned.Count,
            TotalCandidatesNotYetAssigned = unassigned.Sum(g => g.CandidatesInEntra)
        };
    }

    public async Task<DirectoryPendingImportsDto> GetPendingImportsAsync(CancellationToken cancellationToken = default)
    {
        var connectionString = _runtimeSettings.ConnectionString;
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return new DirectoryPendingImportsDto { PendingImports = [], TotalCount = 0 };
        }

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        // Returns all directory_identities in mapped groups that have not yet been imported (no app_user_id).
        // One row per (identity × group mapping) — grouped in C# by identity id.
        const string sql = @"
SELECT
    di.id              AS directory_identity_id,
    di.entra_object_id,
    di.display_name,
    di.mail,
    di.user_principal_name,
    di.department_name,
    d.id               AS preview_department_id,
    dg.display_name    AS group_name,
    ar.role_key
FROM directory_identities di
JOIN directory_group_members dgm        ON dgm.directory_identity_id = di.id
JOIN directory_groups dg                ON dg.id = dgm.directory_group_id
JOIN directory_group_role_mappings dgrm ON dgrm.directory_group_id = dg.id
JOIN app_roles ar                       ON ar.id = dgrm.app_role_id
LEFT JOIN departments d                 ON LOWER(d.name) = LOWER(BTRIM(COALESCE(di.department_name, '')))
WHERE di.app_user_id IS NULL
  AND dgrm.is_active = TRUE
  AND di.account_enabled = TRUE
ORDER BY di.display_name, di.id, dg.display_name;";

        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var identityMap = new Dictionary<long, DirectoryPendingImportDto>();

        while (await reader.ReadAsync(cancellationToken))
        {
            var directoryIdentityId = reader.GetInt64(0);

            if (!identityMap.TryGetValue(directoryIdentityId, out var dto))
            {
                dto = new DirectoryPendingImportDto
                {
                    DirectoryIdentityId = directoryIdentityId,
                    EntraObjectId = reader.GetGuid(1),
                    DisplayName = reader.GetString(2),
                    Mail = reader.IsDBNull(3) ? null : reader.GetString(3),
                    UserPrincipalName = reader.GetString(4),
                    DepartmentName = reader.IsDBNull(5) ? null : reader.GetString(5),
                    PreviewDepartmentId = reader.IsDBNull(6) ? null : reader.GetInt32(6),
                    GroupNames = [],
                    PreviewRoleKeys = []
                };
                identityMap[directoryIdentityId] = dto;
            }

            var groupName = reader.IsDBNull(7) ? null : reader.GetString(7);
            if (groupName is not null && !dto.GroupNames.Contains(groupName))
            {
                dto.GroupNames.Add(groupName);
            }

            var roleKey = reader.IsDBNull(8) ? null : reader.GetString(8);
            if (roleKey is not null && !dto.PreviewRoleKeys.Contains(roleKey))
            {
                dto.PreviewRoleKeys.Add(roleKey);
            }
        }

        var pendingImports = identityMap.Values.OrderBy(d => d.DisplayName).ToList();
        return new DirectoryPendingImportsDto
        {
            PendingImports = pendingImports,
            TotalCount = pendingImports.Count
        };
    }

    public async Task<DirectoryImportResultDto> ImportIdentitiesAsync(
        DirectoryImportRequest request,
        long? actorUserId = null,
        CancellationToken cancellationToken = default)
    {
        var connectionString = _runtimeSettings.ConnectionString;
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return new DirectoryImportResultDto
            {
                ImportedCount = 0,
                FailedCount = request.DirectoryIdentityIds.Count,
                Imported = [],
                Failed = request.DirectoryIdentityIds
                    .Select(id => new DirectoryImportFailureEntry { DirectoryIdentityId = id, Reason = "Database not configured." })
                    .ToList()
            };
        }

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        var imported = new List<DirectoryImportSuccessEntry>();
        var failed = new List<DirectoryImportFailureEntry>();

        foreach (var directoryIdentityId in request.DirectoryIdentityIds)
        {
            try
            {
                var entry = await ImportSingleIdentityAsync(connection, directoryIdentityId, cancellationToken);
                if (entry is null)
                {
                    failed.Add(new DirectoryImportFailureEntry
                    {
                        DirectoryIdentityId = directoryIdentityId,
                        Reason = "Identity not found or already imported."
                    });
                }
                else
                {
                    imported.Add(entry);
                    await _systemEventLogService.WriteAsync(new SystemEventLogWriteModel
                    {
                        Severity = "info",
                        Source = "admin",
                        Category = "directory_import",
                        EventKey = "directory_identity_imported",
                        Message = $"Directory identity {entry.DisplayName} imported as app user {entry.AppUserId}.",
                        ActorUserId = actorUserId,
                        EntityType = "app_user",
                        EntityId = entry.AppUserId.ToString(),
                        Details = new { entry.DirectoryIdentityId, entry.AppUserId, entry.DisplayName }
                    }, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to import directory identity {DirectoryIdentityId}.", directoryIdentityId);
                failed.Add(new DirectoryImportFailureEntry
                {
                    DirectoryIdentityId = directoryIdentityId,
                    Reason = ex.Message
                });
            }
        }

        return new DirectoryImportResultDto
        {
            ImportedCount = imported.Count,
            FailedCount = failed.Count,
            Imported = imported,
            Failed = failed
        };
    }

    private static async Task<DirectoryImportSuccessEntry?> ImportSingleIdentityAsync(
        NpgsqlConnection connection,
        long directoryIdentityId,
        CancellationToken cancellationToken)
    {
        // INSERT only when app_user_id IS NULL — guards against double-import.
        const string insertSql = @"
INSERT INTO app_users (
    display_name, email, external_key, entra_object_id,
    directory_synced, last_directory_synced_at,
    department_id, department_source,
    is_active
)
SELECT
    di.display_name,
    COALESCE(di.mail, di.user_principal_name),
    di.entra_object_id::text,
    di.entra_object_id,
    TRUE,
    NOW(),
    dept.id,
    CASE WHEN dept.id IS NULL THEN 'unassigned' ELSE 'directory' END,
    TRUE
FROM directory_identities di
LEFT JOIN departments dept ON LOWER(dept.name) = LOWER(BTRIM(COALESCE(di.department_name, '')))
WHERE di.id = @directoryIdentityId
  AND di.app_user_id IS NULL
RETURNING id, display_name, department_id;";

        long newAppUserId;
        string displayName;
        int? departmentId;

        await using (var insertCmd = new NpgsqlCommand(insertSql, connection))
        {
            insertCmd.Parameters.AddWithValue("directoryIdentityId", directoryIdentityId);
            await using var reader = await insertCmd.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }
            newAppUserId = reader.GetInt64(0);
            displayName = reader.GetString(1);
            departmentId = reader.IsDBNull(2) ? null : reader.GetInt32(2);
        }

        const string linkIdentitySql = @"
UPDATE directory_identities
SET app_user_id = @appUserId
WHERE id = @directoryIdentityId;";

        await using (var linkCmd = new NpgsqlCommand(linkIdentitySql, connection))
        {
            linkCmd.Parameters.AddWithValue("appUserId", newAppUserId);
            linkCmd.Parameters.AddWithValue("directoryIdentityId", directoryIdentityId);
            await linkCmd.ExecuteNonQueryAsync(cancellationToken);
        }

        const string peopleSql = @"
INSERT INTO people (app_user_id, department_id, directory_identity_id, updated_at)
VALUES (@appUserId, @departmentId, @directoryIdentityId, NOW())
ON CONFLICT (app_user_id) DO UPDATE
SET
    department_id = EXCLUDED.department_id,
    directory_identity_id = EXCLUDED.directory_identity_id,
    updated_at = NOW();";

        await using (var peopleCmd = new NpgsqlCommand(peopleSql, connection))
        {
            peopleCmd.Parameters.AddWithValue("appUserId", newAppUserId);
            var deptParam = peopleCmd.Parameters.Add("departmentId", NpgsqlDbType.Integer);
            deptParam.Value = (object?)departmentId ?? DBNull.Value;
            peopleCmd.Parameters.AddWithValue("directoryIdentityId", directoryIdentityId);
            await peopleCmd.ExecuteNonQueryAsync(cancellationToken);
        }

        return new DirectoryImportSuccessEntry
        {
            DirectoryIdentityId = directoryIdentityId,
            AppUserId = newAppUserId,
            DisplayName = displayName
        };
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
INSERT INTO directory_identities (entra_object_id, user_principal_name, mail, display_name, account_enabled, department_name, employee_number, last_synced_at)
VALUES (@entraObjectId, @userPrincipalName, @mail, @displayName, @accountEnabled, @departmentName, @employeeNumber, NOW())
ON CONFLICT (entra_object_id) DO UPDATE SET
    user_principal_name = EXCLUDED.user_principal_name,
    mail = EXCLUDED.mail,
    display_name = EXCLUDED.display_name,
    account_enabled = EXCLUDED.account_enabled,
    department_name = EXCLUDED.department_name,
    employee_number = EXCLUDED.employee_number,
    last_synced_at = NOW()
RETURNING id;";

        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("entraObjectId", entraObjectId);
        cmd.Parameters.AddWithValue("userPrincipalName", user.UserPrincipalName ?? user.Id ?? entraObjectId.ToString());
        cmd.Parameters.AddWithValue("mail", (object?)user.Mail ?? DBNull.Value);
        cmd.Parameters.AddWithValue("displayName", user.DisplayName ?? user.UserPrincipalName ?? entraObjectId.ToString());
        cmd.Parameters.AddWithValue("accountEnabled", user.AccountEnabled ?? true);
        cmd.Parameters.AddWithValue("departmentName", (object?)Normalize(user.Department) ?? DBNull.Value);
        cmd.Parameters.Add("employeeNumber", NpgsqlDbType.Integer).Value =
            (object?)ParseDirectoryEmployeeNumber(user.EmployeeId) ?? DBNull.Value;
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
  AND u.external_key IS NOT NULL
  AND u.external_key = di.entra_object_id::text;";

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

    private static async Task<DirectoryDepartmentSyncResult> EnsureDirectoryDepartmentsExist(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        const string sql = @"
WITH source_departments AS (
    SELECT DISTINCT BTRIM(di.department_name) AS name
    FROM directory_identities di
    WHERE di.department_name IS NOT NULL
      AND BTRIM(di.department_name) <> ''
),
inserted AS (
    INSERT INTO departments (name)
    SELECT name
    FROM source_departments
    ON CONFLICT (name) DO NOTHING
    RETURNING name
)
SELECT
    (SELECT COUNT(*)::int FROM source_departments),
    COALESCE((SELECT ARRAY_AGG(name ORDER BY name) FROM source_departments), ARRAY[]::text[]),
    (SELECT COUNT(*)::int FROM inserted),
    COALESCE((SELECT ARRAY_AGG(name ORDER BY name) FROM inserted), ARRAY[]::text[]);";

        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return new DirectoryDepartmentSyncResult(
                reader.GetInt32(0),
                reader.GetFieldValue<string[]>(1),
                reader.GetInt32(2),
                reader.GetFieldValue<string[]>(3));
        }

        return new DirectoryDepartmentSyncResult(0, [], 0, []);
    }

    private static async Task<DirectoryUserProjectionResult> UpdateExistingAppUsersFromDirectory(
        NpgsqlConnection connection,
        DateTime startedAt,
        CancellationToken cancellationToken)
    {
        await EnsureDirectoryProjectionUserColumnsAsync(connection, cancellationToken);

        // Only updates existing app_users. New identities from Entra are NOT automatically
        // promoted to app_users — they remain in directory_identities until an admin imports them.
        const string sql = @"
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

        const string linkPeopleByEmployeeNumberSql = @"
WITH linkable_identities AS (
    SELECT
        di.id AS directory_identity_id,
        di.app_user_id,
        di.employee_number
    FROM directory_identities di
    WHERE di.app_user_id IS NOT NULL
      AND di.employee_number IS NOT NULL
),
matched AS (
    UPDATE people p
    SET
        app_user_id = COALESCE(p.app_user_id, linkable_identities.app_user_id),
        directory_identity_id = linkable_identities.directory_identity_id,
        updated_at = NOW()
    FROM linkable_identities
    WHERE p.employee_number = linkable_identities.employee_number
      AND (
          p.app_user_id IS NULL
          OR p.app_user_id = linkable_identities.app_user_id
      )
      AND (
          p.directory_identity_id IS NULL
          OR p.directory_identity_id = linkable_identities.directory_identity_id
      )
    RETURNING
        p.id AS matched_person_id,
        linkable_identities.app_user_id,
        linkable_identities.directory_identity_id,
        linkable_identities.employee_number
)
INSERT INTO person_match_audit_log (
    matched_person_id, app_user_id, directory_identity_id, employee_number,
    match_strategy, match_score, fallback_used, source, detail
)
SELECT
    matched.matched_person_id,
    matched.app_user_id,
    matched.directory_identity_id,
    matched.employee_number,
    'employee_number',
    1.00,
    false,
    'directory_sync_bulk',
    NULL
FROM matched;";

        await using (var linkPeopleCommand = new NpgsqlCommand(linkPeopleByEmployeeNumberSql, connection))
        {
            await linkPeopleCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        const string personSql = @"
INSERT INTO people (app_user_id, department_id, directory_identity_id, updated_at)
SELECT
    u.id,
    u.department_id,
    di.id,
    NOW()
FROM app_users u
JOIN directory_identities di ON di.app_user_id = u.id
WHERE NOT EXISTS (
    SELECT 1
    FROM people existing
    WHERE existing.app_user_id = u.id
       OR existing.directory_identity_id = di.id
       OR (
            di.employee_number IS NOT NULL
            AND existing.employee_number = di.employee_number
       )
)
ON CONFLICT (app_user_id) DO UPDATE
SET
    department_id = EXCLUDED.department_id,
    directory_identity_id = EXCLUDED.directory_identity_id,
    updated_at = NOW();";

        await using var personCommand = new NpgsqlCommand(personSql, connection);
        await personCommand.ExecuteNonQueryAsync(cancellationToken);

        var touchedUserCount = 0;
        var directoryAssignedUserCount = 0;
        var overrideUserCount = 0;
        var unassignedUserCount = 0;
        var linkedIdentityCount = 0;
        var sampleUsers = new List<DirectoryUserProjectionSample>();

        const string summarySql = @"
WITH touched_users AS (
    SELECT
        u.id,
        u.display_name,
        u.email,
        u.department_source,
        u.department_override_active
    FROM app_users u
    WHERE u.directory_synced = TRUE
      AND u.last_directory_synced_at >= @startedAt
)
SELECT
    COUNT(*)::int,
    COUNT(*) FILTER (WHERE department_source = 'directory')::int,
    COUNT(*) FILTER (WHERE department_source = 'override')::int,
    COUNT(*) FILTER (WHERE department_source = 'unassigned')::int,
    (
        SELECT COUNT(*)::int
        FROM directory_identities di
        WHERE di.app_user_id IS NOT NULL
          AND di.last_synced_at >= @startedAt
    )
FROM touched_users;";

        await using (var summaryCommand = new NpgsqlCommand(summarySql, connection))
        {
            summaryCommand.Parameters.AddWithValue("startedAt", startedAt);
            await using var reader = await summaryCommand.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                touchedUserCount = reader.GetInt32(0);
                directoryAssignedUserCount = reader.GetInt32(1);
                overrideUserCount = reader.GetInt32(2);
                unassignedUserCount = reader.GetInt32(3);
                linkedIdentityCount = reader.GetInt32(4);
            }
        }

        const string sampleSql = @"
SELECT
    u.id,
    u.display_name,
    u.email,
    u.department_source,
    u.department_override_active,
    department.name AS department_name,
    di.department_name AS directory_department_name,
    di.user_principal_name
FROM app_users u
LEFT JOIN departments department ON department.id = u.department_id
LEFT JOIN LATERAL (
    SELECT
        latest.department_name,
        latest.user_principal_name
    FROM directory_identities latest
    WHERE latest.app_user_id = u.id
    ORDER BY latest.last_synced_at DESC NULLS LAST, latest.id DESC
    LIMIT 1
) di ON TRUE
WHERE u.directory_synced = TRUE
  AND u.last_directory_synced_at >= @startedAt
ORDER BY u.display_name, u.id
LIMIT 12;";

        await using (var sampleCommand = new NpgsqlCommand(sampleSql, connection))
        {
            sampleCommand.Parameters.AddWithValue("startedAt", startedAt);
            await using var reader = await sampleCommand.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                sampleUsers.Add(new DirectoryUserProjectionSample(
                    reader.GetInt64(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.GetBoolean(4),
                    reader.IsDBNull(5) ? null : reader.GetString(5),
                    reader.IsDBNull(6) ? null : reader.GetString(6),
                    reader.IsDBNull(7) ? null : reader.GetString(7)));
            }
        }

        return new DirectoryUserProjectionResult(
            touchedUserCount,
            directoryAssignedUserCount,
            overrideUserCount,
            unassignedUserCount,
            linkedIdentityCount,
            sampleUsers);
    }

    private static async Task EnsureDirectoryProjectionUserColumnsAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        const string sql = @"
ALTER TABLE app_users
    ADD COLUMN IF NOT EXISTS directory_synced BOOLEAN NOT NULL DEFAULT FALSE,
    ADD COLUMN IF NOT EXISTS last_directory_synced_at TIMESTAMPTZ,
    ADD COLUMN IF NOT EXISTS department_source VARCHAR(32) NOT NULL DEFAULT 'local',
    ADD COLUMN IF NOT EXISTS department_override_active BOOLEAN NOT NULL DEFAULT FALSE;

ALTER TABLE app_users
    DROP CONSTRAINT IF EXISTS chk_app_users_department_source;

ALTER TABLE app_users
    ADD CONSTRAINT chk_app_users_department_source
    CHECK (department_source IN ('local', 'directory', 'override', 'unassigned'));";

        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<IReadOnlyList<DirectoryUserActivationChange>> UpdateDirectoryUserActivationStates(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        // before MATERIALIZED captures the state before the UPDATE runs.
        // updated CTE runs the UPDATE with RETURNING to get new values.
        // Final SELECT joins both to identify changed users only.
        const string sql = @"
WITH before AS MATERIALIZED (
    SELECT u.id, u.display_name, u.email, u.is_active
    FROM app_users u
    WHERE u.directory_synced = TRUE
),
role_based_access AS (
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
),
updated AS (
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
      AND u.directory_synced = TRUE
    RETURNING u.id, u.is_active AS new_is_active
)
SELECT
    updated.id,
    b.display_name,
    b.email,
    b.is_active AS was_active,
    updated.new_is_active
FROM updated
JOIN before b ON b.id = updated.id
WHERE b.is_active IS DISTINCT FROM updated.new_is_active;";

        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var changes = new List<DirectoryUserActivationChange>();
        while (await reader.ReadAsync(cancellationToken))
        {
            changes.Add(new DirectoryUserActivationChange(
                UserId: reader.GetInt64(0),
                DisplayName: reader.GetString(1),
                Email: reader.IsDBNull(2) ? null : reader.GetString(2),
                WasActive: reader.GetBoolean(3),
                IsNowActive: reader.GetBoolean(4)));
        }

        return changes;
    }

    private async Task<DepartmentLeadSyncSummary> SyncDepartmentLeadAssignmentsFromDirectory(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        var states = await LoadDepartmentLeadSyncStatesAsync(connection, cancellationToken);
        var resolvedDepartments = new List<string>();
        var missingDepartments = new List<string>();
        var conflictDepartments = new List<string>();

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

                resolvedDepartments.Add($"{state.DepartmentName}: {candidate.DisplayName}");
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
                    Details = new
                    {
                        state.DepartmentId,
                        state.DepartmentName,
                        candidate.AppUserId,
                        candidate.DisplayName,
                        origin = "Resolved from active directory-synced app user in the same department with auth_manager assigned via directory group mappings",
                        currentAssignmentSource = "department_settings.department_lead_person_id and requirement_approver_person_id"
                    }
                }, cancellationToken);
                continue;
            }

            if (state.Candidates.Count == 0)
            {
                if (state.CurrentDepartmentLeadPersonId.HasValue || state.CurrentRequirementApproverPersonId.HasValue)
                {
                    missingDepartments.Add(state.DepartmentName);
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
                        Details = new
                        {
                            state.DepartmentId,
                            state.DepartmentName,
                            origin = "No active directory-synced app user in this department currently resolves to auth_manager via directory group mappings"
                        }
                    }, cancellationToken);
                }

                continue;
            }

            conflictDepartments.Add($"{state.DepartmentName}: {string.Join(", ", state.Candidates.Select(candidate => candidate.DisplayName))}");
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
                    origin = "Multiple active directory-synced app users in the same department resolve to auth_manager via directory group mappings",
                    candidateNames = state.Candidates.Select(candidate => candidate.DisplayName).ToArray()
                }
            }, cancellationToken);
        }

        return new DepartmentLeadSyncSummary(
            states.Count,
            resolvedDepartments.Count,
            missingDepartments.Count,
            conflictDepartments.Count,
            resolvedDepartments,
            missingDepartments,
            conflictDepartments);
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
        const string matchExistingSql = @"
WITH latest_identity AS (
    SELECT
        di.id,
        di.employee_number
    FROM directory_identities di
    WHERE di.app_user_id = @userId
    ORDER BY di.last_synced_at DESC NULLS LAST, di.id DESC
    LIMIT 1
),
matched AS (
    UPDATE people p
    SET
        app_user_id = COALESCE(p.app_user_id, @userId),
        directory_identity_id = COALESCE(latest_identity.id, p.directory_identity_id),
        updated_at = NOW()
    FROM latest_identity
    WHERE latest_identity.employee_number IS NOT NULL
      AND p.employee_number = latest_identity.employee_number
      AND (
          p.app_user_id IS NULL
          OR p.app_user_id = @userId
      )
      AND (
          p.directory_identity_id IS NULL
          OR p.directory_identity_id = latest_identity.id
      )
    RETURNING p.id, latest_identity.id AS directory_identity_id, latest_identity.employee_number
),
audit AS (
    INSERT INTO person_match_audit_log (
        matched_person_id, app_user_id, directory_identity_id, employee_number,
        match_strategy, match_score, fallback_used, source, detail
    )
    SELECT
        matched.id,
        @userId,
        matched.directory_identity_id,
        matched.employee_number,
        'employee_number',
        1.00,
        false,
        'directory_sync_per_user',
        NULL
    FROM matched
    RETURNING 1
)
SELECT id FROM matched
LIMIT 1;";

        await using (var matchCommand = new NpgsqlCommand(matchExistingSql, connection))
        {
            matchCommand.Parameters.AddWithValue("userId", userId);
            var matchedId = await matchCommand.ExecuteScalarAsync(cancellationToken);
            if (matchedId is long longMatch)
            {
                return longMatch;
            }

            if (matchedId is int intMatch)
            {
                return intMatch;
            }
        }

        const string sql = @"
WITH inserted AS (
    INSERT INTO people (app_user_id, department_id, directory_identity_id, updated_at)
    SELECT
        u.id,
        u.department_id,
        latest_identity.id,
        NOW()
    FROM app_users u
    LEFT JOIN LATERAL (
        SELECT di.id, di.employee_number
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
    RETURNING id, app_user_id, directory_identity_id, (xmax = 0) AS was_inserted
),
latest_identity_for_audit AS (
    SELECT di.id, di.employee_number
    FROM directory_identities di
    WHERE di.app_user_id = @userId
    ORDER BY di.last_synced_at DESC NULLS LAST, di.id DESC
    LIMIT 1
),
audit AS (
    INSERT INTO person_match_audit_log (
        matched_person_id, app_user_id, directory_identity_id, employee_number,
        match_strategy, match_score, fallback_used, source, detail
    )
    SELECT
        inserted.id,
        inserted.app_user_id,
        inserted.directory_identity_id,
        latest_identity_for_audit.employee_number,
        CASE WHEN inserted.was_inserted THEN 'created_new' ELSE 'app_user_upsert' END,
        NULL,
        true,
        'directory_sync_per_user',
        NULL
    FROM inserted
    LEFT JOIN latest_identity_for_audit ON TRUE
    RETURNING 1
)
SELECT id FROM inserted;";

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

    private static int? ParseDirectoryEmployeeNumber(string? employeeId)
    {
        var normalized = Normalize(employeeId);
        return int.TryParse(normalized, out var employeeNumber) && employeeNumber > 0
            ? employeeNumber
            : null;
    }

    private sealed record DepartmentLeadSyncCandidate(long AppUserId, string DisplayName);

    private sealed record DirectoryDepartmentSyncResult(
        int ObservedDepartmentCount,
        IReadOnlyList<string> ObservedDepartmentNames,
        int CreatedDepartmentCount,
        IReadOnlyList<string> CreatedDepartmentNames);

    private sealed record DirectoryUserProjectionSample(
        long UserId,
        string DisplayName,
        string Email,
        string DepartmentSource,
        bool DepartmentOverrideActive,
        string? DepartmentName,
        string? DirectoryDepartmentName,
        string? UserPrincipalName);

    private sealed record DirectoryUserProjectionResult(
        int TouchedUserCount,
        int DirectoryAssignedUserCount,
        int OverrideUserCount,
        int UnassignedUserCount,
        int LinkedIdentityCount,
        IReadOnlyList<DirectoryUserProjectionSample> SampleUsers);

    private sealed record DepartmentLeadSyncSummary(
        int TotalDepartments,
        int ResolvedCount,
        int MissingCount,
        int ConflictCount,
        IReadOnlyList<string> ResolvedDepartments,
        IReadOnlyList<string> MissingDepartments,
        IReadOnlyList<string> ConflictDepartments);

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

    private sealed record DirectoryUserActivationChange(
        long UserId,
        string DisplayName,
        string? Email,
        bool WasActive,
        bool IsNowActive);
}
