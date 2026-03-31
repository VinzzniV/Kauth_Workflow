namespace API;

internal interface IUserAuthorizationRepository
{
    Task<CurrentUser?> ResolveCurrentUser(ResolvedIdentity identity, CancellationToken cancellationToken = default);
    Task<CurrentUser?> FindOrCreateFromExternalIdentity(ResolvedIdentity identity, CancellationToken cancellationToken = default);
    Task<List<DemoLoginUserOptionDto>> GetDemoLoginUsers(CancellationToken cancellationToken = default);
    Task<List<AdminUserDto>> GetAdminUsers(CancellationToken cancellationToken = default);
    Task<List<AdminRoleDto>> GetAdminRoles(CancellationToken cancellationToken = default);
    Task<List<AdminGroupDto>> GetAdminGroups(CancellationToken cancellationToken = default);
    Task<List<AdminPermissionDto>> GetAdminPermissions(CancellationToken cancellationToken = default);
    Task<List<AdminPermissionAuditEntryDto>> GetAdminPermissionAudit(int limit = 100, CancellationToken cancellationToken = default);
    Task<List<AdminDepartmentAssignmentDto>> GetAdminDepartmentAssignments(CancellationToken cancellationToken = default);
    Task<List<AdminResponsibilityOwnerDto>> GetAdminResponsibilityOwners(CancellationToken cancellationToken = default);
    Task<AdminDepartmentAssignmentDto> CreateDepartment(string departmentName, CancellationToken cancellationToken = default);
    Task<bool> DeleteDepartment(int departmentId, CancellationToken cancellationToken = default);
    Task<AdminUserDto> CreateUser(
        string? externalKey,
        string displayName,
        string email,
        string? notificationEmail,
        int? departmentId,
        bool isActive,
        long? actorUserId = null,
        CancellationToken cancellationToken = default);
    Task<bool> DeleteUser(long userId, CancellationToken cancellationToken = default);
    Task<AdminUserDto?> UpdateUserMasterData(
        long userId,
        string? externalKey,
        string displayName,
        string email,
        string? notificationEmail,
        int? departmentId,
        bool isActive,
        long? actorUserId = null,
        CancellationToken cancellationToken = default);
    Task<AdminUserDto?> UpdateUserRoles(long userId, IReadOnlyList<int> roleIds, CancellationToken cancellationToken = default);
    Task<AdminUserDto?> UpdateUserGroups(long userId, IReadOnlyList<int> groupIds, CancellationToken cancellationToken = default);
    Task<AdminGroupDto?> UpdateGroupRoles(int groupId, IReadOnlyList<int> roleIds, CancellationToken cancellationToken = default);
    Task<AdminRoleDto?> UpdateRolePermissions(
        int roleId,
        IReadOnlyList<int> permissionIds,
        long? actorUserId = null,
        CancellationToken cancellationToken = default);
    Task<AdminUserDto?> UpdateUserPermissionOverrides(
        long userId,
        IReadOnlyList<AdminUserPermissionOverrideUpsertRequest> overrides,
        long? actorUserId = null,
        CancellationToken cancellationToken = default);
    Task<AdminDepartmentAssignmentDto?> UpdateDepartmentAssignment(
        int departmentId,
        long? departmentLeadUserId,
        long? requirementOwnerUserId,
        CancellationToken cancellationToken = default);
    Task<AdminResponsibilityOwnerDto?> UpdateResponsibilityOwner(
        int responsibilityId,
        long? appUserId,
        int? departmentId,
        CancellationToken cancellationToken = default);
}
