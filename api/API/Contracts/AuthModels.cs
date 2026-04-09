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
    /// <summary>
    /// How this role was assigned: "direct", "group" (app group), or "directory_group" (Entra group mapping).
    /// </summary>
    public required string AssignmentSource { get; init; }
    public int? GroupId { get; init; }
    public string? GroupKey { get; init; }
    /// <summary>
    /// When AssignmentSource is "directory_group", the display name of the Entra group that granted this role.
    /// </summary>
    public string? DirectoryGroupName { get; init; }
    public string Scope { get; init; } = "global";
    public int? ScopeDepartmentId { get; init; }
    public string? ScopeDepartmentName { get; init; }
}

public sealed class CurrentUserPermission
{
    public required int PermissionId { get; init; }
    public required string PermissionKey { get; init; }
    public required string PermissionName { get; init; }
    public string Scope { get; init; } = "global";
    public int? ScopeDepartmentId { get; init; }
    public string? ScopeDepartmentName { get; init; }
}

public sealed class CurrentUserPermissionScope
{
    public required string PermissionKey { get; init; }
    public string Scope { get; init; } = "global";
    public int? ScopeDepartmentId { get; init; }
    public string? ScopeDepartmentName { get; init; }
}

public sealed class CurrentUserPermissionOverride
{
    public required long OverrideId { get; init; }
    public required int PermissionId { get; init; }
    public required string PermissionKey { get; init; }
    public required string PermissionName { get; init; }
    public required string Effect { get; init; }
    public string Scope { get; init; } = "global";
    public int? ScopeDepartmentId { get; init; }
    public string? ScopeDepartmentName { get; init; }
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
    public bool DirectorySynced { get; init; }
    public string DepartmentSource { get; init; } = "local";
    public bool DepartmentOverrideActive { get; init; }
    public required List<CurrentUserGroup> Groups { get; init; }
    public required List<CurrentUserRole> DirectRoles { get; init; }
    public required List<CurrentUserRole> GroupRoles { get; init; }
    public required List<CurrentUserRole> EffectiveRoles { get; init; }
    public List<CurrentUserPermission> EffectivePermissions { get; init; } = [];
    public List<CurrentUserPermissionScope> PermissionScopes { get; init; } = [];
    public List<CurrentUserPermissionOverride> PermissionOverrides { get; init; } = [];
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

    public bool HasPermission(string permissionKey, int? departmentId = null)
    {
        if (string.IsNullOrWhiteSpace(permissionKey))
        {
            return false;
        }

        return EffectivePermissions.Any(permission =>
        {
            if (!string.Equals(permission.PermissionKey, permissionKey.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (string.Equals(permission.Scope, "global", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!departmentId.HasValue)
            {
                return true;
            }

            return permission.ScopeDepartmentId == departmentId.Value;
        });
    }

    public bool HasAnyPermission(params string[] permissionKeys)
    {
        if (permissionKeys.Length == 0)
        {
            return false;
        }

        return permissionKeys.Any(permissionKey => HasPermission(permissionKey));
    }

    public IReadOnlySet<int> GetPermissionDepartmentIds(string permissionKey)
    {
        return EffectivePermissions
            .Where(permission =>
                string.Equals(permission.PermissionKey, permissionKey.Trim(), StringComparison.OrdinalIgnoreCase)
                && !string.Equals(permission.Scope, "global", StringComparison.OrdinalIgnoreCase)
                && permission.ScopeDepartmentId.HasValue)
            .Select(permission => permission.ScopeDepartmentId!.Value)
            .ToHashSet();
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

// Rueckgaben fuer Dev-Simulation und Admin-Verwaltung.
public sealed class SimulationLoginUserOptionDto
{
    public required long UserId { get; init; }
    public required string Username { get; init; }
    public required string DisplayName { get; init; }
    public required string Email { get; init; }
    public string? DepartmentName { get; init; }
}

public sealed class SimulationLoginRequest
{
    public long? UserId { get; init; }
}

public sealed class MeDto
{
    public required string Username { get; init; }
    public required string DisplayName { get; init; }
    public required string Email { get; init; }
    public required List<string> Roles { get; init; }
    public required List<string> Groups { get; init; }
    public List<string> Permissions { get; init; } = [];
    public List<CurrentUserPermissionScope> PermissionScopes { get; init; } = [];
    public bool DirectorySynced { get; init; }
    public string DepartmentSource { get; init; } = "local";
    public bool DepartmentOverrideActive { get; init; }
}

public sealed class SimulationLoginResponse
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
    public string Scope { get; init; } = "global";
    public int? ScopeDepartmentId { get; init; }
    public string? ScopeDepartmentName { get; init; }
    public required bool IsActive { get; init; }
    public List<AdminPermissionDto> Permissions { get; init; } = [];
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
    public required bool HasManagerAccess { get; init; }
    public bool CanAccessSupervisorStep { get; set; }
    public int? DepartmentId { get; init; }
    public string? DepartmentName { get; init; }
    public bool DirectorySynced { get; init; }
    public string DepartmentSource { get; init; } = "local";
    public bool DepartmentOverrideActive { get; init; }
    public long? DirectoryIdentityId { get; init; }
    public string? UserPrincipalName { get; init; }
    public string? DirectoryDisplayName { get; init; }
    public required List<AdminRoleDto> Roles { get; init; }
    public required List<AdminGroupRefDto> Groups { get; init; }
    public List<AdminRoleDto> EffectiveRoles { get; init; } = [];
    public List<AdminPermissionOverrideDto> PermissionOverrides { get; init; } = [];
    public List<AdminPermissionGrantDto> EffectivePermissions { get; init; } = [];
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

public sealed class AdminPermissionDto
{
    public required int PermissionId { get; init; }
    public required string PermissionKey { get; init; }
    public required string PermissionName { get; init; }
    public string? Description { get; init; }
    public required string ScopeKind { get; init; }
    public required string Category { get; init; }
    public required bool IsActive { get; init; }
}

public class AdminPermissionGrantDto
{
    public required int PermissionId { get; init; }
    public required string PermissionKey { get; init; }
    public required string PermissionName { get; init; }
    public string Scope { get; init; } = "global";
    public int? ScopeDepartmentId { get; init; }
    public string? ScopeDepartmentName { get; init; }
}

public sealed class AdminPermissionOverrideDto : AdminPermissionGrantDto
{
    public required long OverrideId { get; init; }
    public required string Effect { get; init; }
}

public sealed class AdminUserPermissionOverrideUpsertRequest
{
    public int PermissionId { get; init; }
    public string? Effect { get; init; }
    public string? Scope { get; init; }
    public int? ScopeDepartmentId { get; init; }
}

public sealed class AdminUserPermissionOverrideUpdateRequest
{
    public required List<AdminUserPermissionOverrideUpsertRequest> Overrides { get; init; }
}

public sealed class AdminRolePermissionUpdateRequest
{
    public required List<int> PermissionIds { get; init; }
}

public sealed class AdminPermissionAuditEntryDto
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

public sealed class AdminResponsibilityCreateRequest
{
    public required string ResponsibilityName { get; init; }
    public int? DepartmentId { get; init; }
}

public sealed class AdminNotificationEmailConfigurationDto
{
    public required bool Enabled { get; init; }
    public required string Mode { get; init; }
    public string? SenderEmail { get; init; }
    public required string FrontendBaseUrl { get; init; }
    public string? TestRecipientEmail { get; init; }
    public string? SandboxRedirectEmail { get; init; }
    public required bool NotifyOnWorkflowCreated { get; init; }
    public required bool NotifyOnTaskReady { get; init; }
    public required bool NotifyOnWorkflowCompleted { get; init; }
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
    public string? SenderEmail { get; init; }
    public required string FrontendBaseUrl { get; init; }
    public string? TestRecipientEmail { get; init; }
    public string? SandboxRedirectEmail { get; init; }
    public bool NotifyOnWorkflowCreated { get; init; } = true;
    public bool NotifyOnTaskReady { get; init; } = true;
    public bool NotifyOnWorkflowCompleted { get; init; } = true;
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

public sealed class AdminGraphApplicationConfigurationDto
{
    public string? TenantId { get; init; }
    public string? ClientId { get; init; }
    public required bool HasClientSecret { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public required string ConfigurationSource { get; init; }
    public required string ConfigurationStatus { get; init; }
    public string? ConfigurationMessage { get; init; }
}

internal sealed class DevSimulationSession
{
    public required string Token { get; init; }
    public required long UserId { get; init; }
    public required string IdentityKey { get; init; }
    public required DateTime ExpiresAtUtc { get; init; }
}
