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
    Task<CursorPageDto<AdminDirectoryMappingAuditEntryDto>> GetMappingAuditAsync(CursorPageQuery query, CancellationToken cancellationToken = default);
    Task<AdminDirectoryGroupRoleMappingDto> UpsertGroupRoleMappingAsync(
        AdminDirectoryGroupRoleMappingUpsertRequest request,
        long? actorUserId = null,
        CancellationToken cancellationToken = default);
    Task<bool> DeleteGroupRoleMappingAsync(int mappingId, long? actorUserId = null, CancellationToken cancellationToken = default);
    Task<DirectoryResponsibilityGapsDto> GetResponsibilityGapsAsync(CancellationToken cancellationToken = default);
    Task<DirectoryPendingImportsDto> GetPendingImportsAsync(CancellationToken cancellationToken = default);
    Task<DirectoryImportResultDto> ImportIdentitiesAsync(DirectoryImportRequest request, long? actorUserId = null, CancellationToken cancellationToken = default);
}

public sealed class DirectoryResponsibilityGapsDto
{
    public required List<DirectoryResponsibilityGapEntry> Gaps { get; init; }
    public int TotalUnassignedDepartments { get; init; }
    public int TotalCandidatesNotYetAssigned { get; init; }
}

public sealed class DirectoryResponsibilityGapEntry
{
    public required int DepartmentId { get; init; }
    public required string DepartmentName { get; init; }
    public string? EntraGroupName { get; init; }
    public long? AssignedLeadPersonId { get; init; }
    public required List<DirectoryResponsibilityCandidate> Candidates { get; init; }
    public int CandidatesInEntra => Candidates.Count;
}

public sealed class DirectoryResponsibilityCandidate
{
    public required long AppUserId { get; init; }
    public required string DisplayName { get; init; }
    public string? Mail { get; init; }
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

public sealed class DirectoryPendingImportsDto
{
    public required List<DirectoryPendingImportDto> PendingImports { get; init; }
    public int TotalCount { get; init; }
}

public sealed class DirectoryPendingImportDto
{
    public required long DirectoryIdentityId { get; init; }
    public required Guid EntraObjectId { get; init; }
    public required string DisplayName { get; init; }
    public string? Mail { get; init; }
    public string? UserPrincipalName { get; init; }
    public string? DepartmentName { get; init; }
    public int? PreviewDepartmentId { get; init; }
    public required List<string> GroupNames { get; init; }
    public required List<string> PreviewRoleKeys { get; init; }
}

public sealed class DirectoryImportRequest
{
    public required List<long> DirectoryIdentityIds { get; init; }
}

public sealed class DirectoryImportResultDto
{
    public int ImportedCount { get; init; }
    public int FailedCount { get; init; }
    public required List<DirectoryImportSuccessEntry> Imported { get; init; }
    public required List<DirectoryImportFailureEntry> Failed { get; init; }
}

public sealed class DirectoryImportSuccessEntry
{
    public required long DirectoryIdentityId { get; init; }
    public required long AppUserId { get; init; }
    public required string DisplayName { get; init; }
}

public sealed class DirectoryImportFailureEntry
{
    public required long DirectoryIdentityId { get; init; }
    public required string Reason { get; init; }
}
