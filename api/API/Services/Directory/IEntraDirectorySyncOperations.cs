using Npgsql;

namespace API.Services.Directory;

public sealed record DirectoryDepartmentSyncResult(
    int ObservedDepartmentCount,
    IReadOnlyList<string> ObservedDepartmentNames,
    int CreatedDepartmentCount,
    IReadOnlyList<string> CreatedDepartmentNames);

public sealed record DirectoryUserProjectionSample(
    long UserId,
    string DisplayName,
    string Email,
    string DepartmentSource,
    bool DepartmentOverrideActive,
    string? DepartmentName,
    string? DirectoryDepartmentName,
    string? UserPrincipalName);

public sealed record DirectoryUserProjectionResult(
    int TouchedUserCount,
    int DirectoryAssignedUserCount,
    int OverrideUserCount,
    int UnassignedUserCount,
    int LinkedIdentityCount,
    IReadOnlyList<DirectoryUserProjectionSample> SampleUsers);

public interface IEntraDirectorySyncOperations
{
    Task EnsureDirectoryProjectionUserColumnsAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken);

    Task<Dictionary<Guid, long>> UpsertDirectoryIdentitiesBatchAsync(
        NpgsqlConnection connection,
        IReadOnlyList<(Guid EntraObjectId, EntraDirectoryUser User)> validUsers,
        CancellationToken cancellationToken);

    Task InsertGroupMembershipsBatchAsync(
        NpgsqlConnection connection,
        int directoryGroupId,
        long[] directoryIdentityIds,
        CancellationToken cancellationToken);

    Task AutoLinkIdentitiesToAppUsersAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken);

    Task<DirectoryDepartmentSyncResult> EnsureDirectoryDepartmentsExistAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken);

    Task<DirectoryUserProjectionResult> UpdateExistingAppUsersFromDirectoryAsync(
        NpgsqlConnection connection,
        DateTime startedAt,
        CancellationToken cancellationToken);

    Task EnsureDevelopmentDefaultGroupMappingsAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken);
}
