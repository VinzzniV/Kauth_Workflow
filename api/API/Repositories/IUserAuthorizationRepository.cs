namespace API;

internal interface IUserAuthorizationRepository
{
    Task<CurrentUser?> ResolveCurrentUser(ResolvedIdentity identity, CancellationToken cancellationToken = default);
    Task<CurrentUser?> FindOrCreateFromExternalIdentity(ResolvedIdentity identity, CancellationToken cancellationToken = default);
    Task<List<SimulationLoginUserOptionDto>> GetSimulationLoginUsers(CancellationToken cancellationToken = default);
    Task<List<AdminUserDto>> GetAdminUsers(CancellationToken cancellationToken = default);
    Task<AdminListPageDto<AdminUserDto>> GetAdminUsers(AdminListQuery query, CancellationToken cancellationToken = default);
    Task<List<AdminRoleDto>> GetAdminRoles(CancellationToken cancellationToken = default);
    Task<AdminListPageDto<AdminRoleDto>> GetAdminRoles(AdminListQuery query, CancellationToken cancellationToken = default);
    Task<List<AdminGroupDto>> GetAdminGroups(CancellationToken cancellationToken = default);
    Task<AdminListPageDto<AdminGroupDto>> GetAdminGroups(AdminListQuery query, CancellationToken cancellationToken = default);
    Task<List<AdminPermissionDto>> GetAdminPermissions(CancellationToken cancellationToken = default);
    Task<AdminListPageDto<AdminPermissionDto>> GetAdminPermissions(AdminListQuery query, CancellationToken cancellationToken = default);
    Task<CursorPageDto<AdminPermissionAuditEntryDto>> GetAdminPermissionAudit(CursorPageQuery query, CancellationToken cancellationToken = default);
    Task<AdminListPageDto<AdminDepartmentAssignmentDto>> GetAdminDepartmentAssignments(AdminListQuery query, CancellationToken cancellationToken = default);
    Task<AdminListPageDto<AdminRoleDto>> GetAdminDepartmentPositions(AdminListQuery query, CancellationToken cancellationToken = default);
    Task<AdminListPageDto<AdminResponsibilityOwnerDto>> GetAdminResponsibilityOwners(AdminListQuery query, CancellationToken cancellationToken = default);
    Task<AdminDepartmentAssignmentDto> CreateDepartment(string departmentName, CancellationToken cancellationToken = default);
    Task<bool> DeleteDepartment(int departmentId, CancellationToken cancellationToken = default);
    Task<AdminRoleDto> CreateDepartmentPosition(
        int departmentId,
        string positionName,
        CancellationToken cancellationToken = default);
    Task<List<EntraJobTitleDto>> GetDepartmentEntraJobTitles(int departmentId, CancellationToken cancellationToken = default);
    Task<ImportPositionsFromEntraResult> ImportDepartmentPositionsFromEntra(int departmentId, IReadOnlyList<string> jobTitles, CancellationToken cancellationToken = default);
    Task<AdminRoleDto?> UpdateDepartmentPosition(
        int positionId,
        string positionName,
        bool isActive,
        CancellationToken cancellationToken = default);
    Task<bool> DeleteDepartmentPosition(int positionId, CancellationToken cancellationToken = default);
    Task<AdminResponsibilityOwnerDto> CreateResponsibility(
        string responsibilityName,
        int? departmentId,
        CancellationToken cancellationToken = default);
    Task<bool> DeleteResponsibility(int responsibilityId, CancellationToken cancellationToken = default);
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
