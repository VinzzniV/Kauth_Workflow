namespace API;

// DTOs und Domänenmodelle fuer Identitaet, aktuellen Benutzer und Admin-Stammdaten.
public sealed class ResolvedIdentity
{
    public long? UserId { get; init; }
    public string? ExternalKey { get; init; }
    public string? Email { get; init; }
    public string? DisplayName { get; init; }
    public required string Provider { get; init; }
}

public sealed class CurrentUserGroup
{
    public required int GroupId { get; init; }
    public required string GroupKey { get; init; }
    public required string GroupName { get; init; }
    public string? Description { get; init; }
}

public sealed class CurrentUserRole
{
    public required int RoleId { get; init; }
    public required string RoleKey { get; init; }
    public required string RoleName { get; init; }
    public required string RoleKind { get; init; }
    public required string AssignmentSource { get; init; }
    public int? GroupId { get; init; }
    public string? GroupKey { get; init; }
}

public sealed class CurrentUserResponsibility
{
    public required int ResponsibilityId { get; init; }
    public required string ResponsibilityKey { get; init; }
    public required string ResponsibilityName { get; init; }
    public required string ResponsibilityType { get; init; }
    public int? DepartmentId { get; init; }
    public string? DepartmentName { get; init; }
    public required string AssignmentSource { get; init; }
    public int? GroupId { get; init; }
    public string? GroupKey { get; init; }
}

public sealed class CurrentUser
{
    public required long UserId { get; init; }
    public string? ExternalKey { get; init; }
    public required string DisplayName { get; init; }
    public required string Email { get; init; }
    public bool IsActive { get; init; }
    public int? DepartmentId { get; init; }
    public string? DepartmentName { get; init; }
    public required string IdentityProvider { get; init; }
    public required List<CurrentUserGroup> Groups { get; init; }
    public required List<CurrentUserRole> DirectRoles { get; init; }
    public required List<CurrentUserRole> GroupRoles { get; init; }
    public required List<CurrentUserRole> EffectiveRoles { get; init; }
    public required List<CurrentUserResponsibility> DirectResponsibilities { get; init; }
    public required List<CurrentUserResponsibility> GroupResponsibilities { get; init; }
    public required List<CurrentUserResponsibility> EffectiveResponsibilities { get; init; }

    // Komfortmethoden vereinfachen Policy-Pruefungen in Services und Endpunkten.
    public bool HasRole(string roleKey)
    {
        if (string.IsNullOrWhiteSpace(roleKey))
        {
            return false;
        }

        return EffectiveRoles.Any(
            role => string.Equals(role.RoleKey, roleKey.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public bool HasResponsibility(string responsibilityKey)
    {
        if (string.IsNullOrWhiteSpace(responsibilityKey))
        {
            return false;
        }

        return EffectiveResponsibilities.Any(
            responsibility => string.Equals(
                responsibility.ResponsibilityKey,
                responsibilityKey.Trim(),
                StringComparison.OrdinalIgnoreCase));
    }
}

// Rueckgaben fuer Demo-Login und Admin-Verwaltung.
public sealed class DemoLoginUserOptionDto
{
    public required long UserId { get; init; }
    public required string Username { get; init; }
    public required string DisplayName { get; init; }
    public required string Email { get; init; }
    public string? DepartmentName { get; init; }
}

public sealed class DemoLoginRequest
{
    public string? Username { get; init; }
}

public sealed class MeDto
{
    public required string Username { get; init; }
    public required string DisplayName { get; init; }
    public required string Email { get; init; }
    public required List<string> Roles { get; init; }
    public required List<string> Groups { get; init; }
}

public sealed class DemoLoginResponse
{
    public required string Token { get; init; }
    public required DateTime ExpiresAtUtc { get; init; }
    public required MeDto User { get; init; }
}

public sealed class AdminRoleDto
{
    public required int RoleId { get; init; }
    public required string RoleKey { get; init; }
    public required string RoleName { get; init; }
    public required string RoleKind { get; init; }
    public int? DepartmentId { get; init; }
    public string? DepartmentName { get; init; }
    public required bool IsActive { get; init; }
}

public class AdminGroupRefDto
{
    public required int GroupId { get; init; }
    public required string GroupKey { get; init; }
    public required string GroupName { get; init; }
    public string? Description { get; init; }
    public required bool IsActive { get; init; }
}

public sealed class AdminGroupDto : AdminGroupRefDto
{
    public required List<AdminRoleDto> Roles { get; init; }
}

public sealed class AdminUserDto
{
    public required long UserId { get; init; }
    public string? ExternalKey { get; init; }
    public required string DisplayName { get; init; }
    public required string Email { get; init; }
    public string? NotificationEmail { get; init; }
    public required bool IsActive { get; init; }
    public int? DepartmentId { get; init; }
    public string? DepartmentName { get; init; }
    public required List<AdminRoleDto> Roles { get; init; }
    public required List<AdminGroupRefDto> Groups { get; init; }
}

public sealed class AdminDepartmentAssignmentDto
{
    public required int DepartmentId { get; init; }
    public required string DepartmentName { get; init; }
    public long? DepartmentLeadUserId { get; init; }
    public string? DepartmentLeadDisplayName { get; init; }
    public long? RequirementOwnerUserId { get; init; }
    public string? RequirementOwnerDisplayName { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

public sealed class AdminResponsibilityOwnerDto
{
    public required int ResponsibilityId { get; init; }
    public required string ResponsibilityKey { get; init; }
    public string? SystemKey { get; init; }
    public required string ResponsibilityName { get; init; }
    public required string ResponsibilityType { get; init; }
    public int? DepartmentId { get; init; }
    public string? DepartmentName { get; init; }
    public long? AppUserId { get; init; }
    public string? AppUserDisplayName { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

public sealed class AdminUserMasterDataUpdateRequest
{
    public string? ExternalKey { get; init; }
    public required string DisplayName { get; init; }
    public required string Email { get; init; }
    public string? NotificationEmail { get; init; }
    public int? DepartmentId { get; init; }
    public required bool IsActive { get; init; }
}

public sealed class AdminUserCreateRequest
{
    public string? ExternalKey { get; init; }
    public required string DisplayName { get; init; }
    public required string Email { get; init; }
    public string? NotificationEmail { get; init; }
    public int? DepartmentId { get; init; }
    public bool IsActive { get; init; } = true;
}

public sealed class AdminUserRoleUpdateRequest
{
    public required List<int> RoleIds { get; init; }
}

public sealed class AdminUserGroupUpdateRequest
{
    public required List<int> GroupIds { get; init; }
}

public sealed class AdminGroupRoleUpdateRequest
{
    public required List<int> RoleIds { get; init; }
}

public sealed class AdminDepartmentAssignmentUpdateRequest
{
    public long? DepartmentLeadUserId { get; init; }
    public long? RequirementOwnerUserId { get; init; }
}

public sealed class AdminDepartmentCreateRequest
{
    public required string DepartmentName { get; init; }
}

public sealed class AdminResponsibilityOwnerUpdateRequest
{
    public long? AppUserId { get; init; }
    public int? DepartmentId { get; init; }
}

public sealed class AdminNotificationEmailConfigurationDto
{
    public required bool Enabled { get; init; }
    public required string Mode { get; init; }
    public string? TenantId { get; init; }
    public string? ClientId { get; init; }
    public string? SenderEmail { get; init; }
    public required string FrontendBaseUrl { get; init; }
    public string? TestRecipientEmail { get; init; }
    public required string LastTestStatus { get; init; }
    public DateTime? LastTestAt { get; init; }
    public string? LastError { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public required bool HasClientSecret { get; init; }
    public required string ConfigurationStatus { get; init; }
    public string? ConfigurationMessage { get; init; }
}

public sealed class AdminNotificationEmailConfigurationUpdateRequest
{
    public required bool Enabled { get; init; }
    public string? TenantId { get; init; }
    public string? ClientId { get; init; }
    public string? ClientSecret { get; init; }
    public string? SenderEmail { get; init; }
    public required string FrontendBaseUrl { get; init; }
    public string? TestRecipientEmail { get; init; }
}

public sealed class AdminNotificationEmailTestRequest
{
    public string? RecipientEmail { get; init; }
}

public sealed class AdminNotificationEmailTestResultDto
{
    public required bool Success { get; init; }
    public required string Status { get; init; }
    public required string Message { get; init; }
    public required string RecipientEmail { get; init; }
}

public sealed class AdminNotificationEmailTestResponse
{
    public required AdminNotificationEmailConfigurationDto Configuration { get; init; }
    public required AdminNotificationEmailTestResultDto Result { get; init; }
}

internal sealed class DemoSession
{
    public required string Token { get; init; }
    public required long UserId { get; init; }
    public required string IdentityKey { get; init; }
    public required DateTime ExpiresAtUtc { get; init; }
}
