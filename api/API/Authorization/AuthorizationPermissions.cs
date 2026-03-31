namespace API;

internal static class AuthorizationPermissions
{
    public const string AppAccess = "app.access";
    public const string UsersViewDepartment = "users.view_department";
    public const string UsersViewAllDepartments = "users.view_all_departments";
    public const string WorkflowsViewDepartment = "workflows.view_department";
    public const string WorkflowsViewAll = "workflows.view_all";
    public const string WorkflowCreatePrefix = "workflows.create.";
    public const string TasksExecuteSupervisor = "tasks.execute.supervisor";
    public const string TasksExecuteDepartment = "tasks.execute.department";
    public const string TasksAssignOverride = "tasks.assign.override";
    public const string AdminDirectoryManage = "admin.directory.manage";
    public const string AdminPermissionsManage = "admin.permissions.manage";

    public static readonly string[] WorkflowCreatePermissions =
    {
        "workflows.create.onboarding",
        "workflows.create.offboarding",
        "workflows.create.department_change",
        "workflows.create.position_change",
        "workflows.create.role_change",
        "workflows.create.name_change"
    };

    public static string WorkflowCreate(string processTypeKey)
    {
        var normalized = string.IsNullOrWhiteSpace(processTypeKey)
            ? string.Empty
            : processTypeKey.Trim().ToLowerInvariant();

        return $"{WorkflowCreatePrefix}{normalized}";
    }

    public static bool IsWorkflowCreatePermission(string permissionKey)
    {
        return !string.IsNullOrWhiteSpace(permissionKey)
            && permissionKey.StartsWith(WorkflowCreatePrefix, StringComparison.OrdinalIgnoreCase);
    }
}
