namespace API.Services.Directory;

public enum EntraGraphInitStatus
{
    Ready,
    MissingCredentials,
    Failed
}

public sealed record EntraGraphInitResult(
    EntraGraphInitStatus Status,
    string? ErrorMessage = null,
    string? ExceptionType = null);

public sealed record EntraSecurityGroup(
    string? Id,
    string? DisplayName,
    string? Description);

public sealed record EntraDirectoryUser(
    string? Id,
    string? UserPrincipalName,
    string? Mail,
    string? DisplayName,
    bool? AccountEnabled,
    string? Department,
    string? EmployeeId);

public interface IEntraGraphClient
{
    Task<EntraGraphInitResult> InitializeAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<EntraSecurityGroup>> LoadSecurityGroupsAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<EntraDirectoryUser>> LoadGroupMembersAsync(string groupId, CancellationToken cancellationToken);
}
