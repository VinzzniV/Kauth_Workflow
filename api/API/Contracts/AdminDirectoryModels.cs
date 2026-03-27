namespace API;

public sealed class AdminDirectoryGroupRoleMappingDto
{
    public required int MappingId { get; init; }
    public required int DirectoryGroupId { get; init; }
    public required int AppRoleId { get; init; }
    public required string AppRoleKey { get; init; }
    public required string AppRoleName { get; init; }
    public required string AppRoleKind { get; init; }
    public int? RoleDepartmentId { get; init; }
    public string? RoleDepartmentName { get; init; }
    public required string Scope { get; init; }
    public int? ScopeDepartmentId { get; init; }
    public required bool IsActive { get; init; }
}

public sealed class AdminDirectoryGroupDto
{
    public required int DirectoryGroupId { get; init; }
    public required string ExternalGroupId { get; init; }
    public required string DisplayName { get; init; }
    public string? Description { get; init; }
    public DateTime? LastSyncedAt { get; init; }
    public required int MemberCount { get; init; }
    public required List<AdminDirectoryGroupRoleMappingDto> RoleMappings { get; init; }
}

public sealed class AdminDirectoryIdentityDto
{
    public required long DirectoryIdentityId { get; init; }
    public required string EntraObjectId { get; init; }
    public required string UserPrincipalName { get; init; }
    public string? Mail { get; init; }
    public required string DisplayName { get; init; }
    public required bool AccountEnabled { get; init; }
    public long? AppUserId { get; init; }
    public string? AppUserDisplayName { get; init; }
    public DateTime? LastSyncedAt { get; init; }
}

public sealed class AdminDirectoryMappingAuditEntryDto
{
    public required long AuditEntryId { get; init; }
    public long? ActorUserId { get; init; }
    public string? ActorDisplayName { get; init; }
    public required string EventType { get; init; }
    public required string EntityType { get; init; }
    public string? Detail { get; init; }
    public string? OldValue { get; init; }
    public string? NewValue { get; init; }
    public required DateTime CreatedAt { get; init; }
}

public sealed class AdminDirectoryGroupRoleMappingUpsertRequest
{
    public required int DirectoryGroupId { get; init; }
    public required int AppRoleId { get; init; }
    public string? Scope { get; init; }
    public int? ScopeDepartmentId { get; init; }
    public bool IsActive { get; init; } = true;
}
