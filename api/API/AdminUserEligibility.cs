namespace API;

internal static class AdminUserEligibility
{
    private static readonly AuthorizationPolicyService AuthorizationPolicy = new();

    public static bool CanAccessSupervisorStep(AdminUserDto user)
    {
        ArgumentNullException.ThrowIfNull(user);

        return user.IsActive && AuthorizationPolicy.CanAccessSupervisorStep(ToCurrentUser(user));
    }

    private static CurrentUser ToCurrentUser(AdminUserDto user)
    {
        return new CurrentUser
        {
            UserId = user.UserId,
            ExternalKey = user.ExternalKey,
            DisplayName = user.DisplayName,
            Email = user.Email,
            IsActive = user.IsActive,
            DepartmentId = user.DepartmentId,
            DepartmentName = user.DepartmentName,
            IdentityProvider = "admin",
            DirectorySynced = user.DirectorySynced,
            DepartmentSource = user.DepartmentSource,
            DepartmentOverrideActive = user.DepartmentOverrideActive,
            Groups = [],
            DirectRoles = [],
            GroupRoles = [],
            EffectiveRoles = user.EffectiveRoles.Select(role => new CurrentUserRole
            {
                RoleId = role.RoleId,
                RoleKey = role.RoleKey,
                RoleName = role.RoleName,
                RoleKind = role.RoleKind,
                AssignmentSource = "admin",
                Scope = role.Scope,
                ScopeDepartmentId = role.ScopeDepartmentId,
                ScopeDepartmentName = role.ScopeDepartmentName
            }).ToList(),
            EffectivePermissions = user.EffectivePermissions.Select(permission => new CurrentUserPermission
            {
                PermissionId = permission.PermissionId,
                PermissionKey = permission.PermissionKey,
                PermissionName = permission.PermissionName,
                Scope = permission.Scope,
                ScopeDepartmentId = permission.ScopeDepartmentId,
                ScopeDepartmentName = permission.ScopeDepartmentName
            }).ToList(),
            DirectResponsibilities = [],
            GroupResponsibilities = [],
            EffectiveResponsibilities = []
        };
    }
}
