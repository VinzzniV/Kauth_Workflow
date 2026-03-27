namespace API;

/// <summary>
/// Synchronizes groups and identities from an external directory (Microsoft Entra ID)
/// into the local directory projection tables.
/// </summary>
internal interface IDirectorySyncService
{
    Task<DirectorySyncResult> SyncAllAsync(string? groupPrefixOverride = null, CancellationToken cancellationToken = default);
    Task<DirectorySyncStatusDto> GetSyncStatusAsync(CancellationToken cancellationToken = default);
    Task<List<AdminDirectoryGroupDto>> GetGroupsAsync(CancellationToken cancellationToken = default);
    Task<List<AdminDirectoryIdentityDto>> GetIdentitiesAsync(int limit = 100, int offset = 0, CancellationToken cancellationToken = default);
    Task<List<AdminDirectoryMappingAuditEntryDto>> GetMappingAuditAsync(int limit = 50, CancellationToken cancellationToken = default);
    Task<AdminDirectoryGroupRoleMappingDto> UpsertGroupRoleMappingAsync(
        AdminDirectoryGroupRoleMappingUpsertRequest request,
        long? actorUserId = null,
        CancellationToken cancellationToken = default);
    Task<bool> DeleteGroupRoleMappingAsync(int mappingId, long? actorUserId = null, CancellationToken cancellationToken = default);
}

public sealed class DirectorySyncResult
{
    public required string Status { get; init; }
    public int GroupsSynced { get; init; }
    public int IdentitiesSynced { get; init; }
    public int MembershipsSynced { get; init; }
    public string? ErrorMessage { get; init; }
    public string? AppliedGroupPrefix { get; init; }
}

public sealed class DirectorySyncStatusDto
{
    public DateTime? LastSyncAt { get; init; }
    public string? LastSyncStatus { get; init; }
    public int TotalGroups { get; init; }
    public int TotalIdentities { get; init; }
    public int TotalMappings { get; init; }
    public string? LastError { get; init; }
    public string? ConfiguredGroupPrefix { get; init; }
}

public sealed class DirectorySyncRequest
{
    public string? GroupPrefix { get; init; }
}
