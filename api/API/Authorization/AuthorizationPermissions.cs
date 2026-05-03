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

    // Suffix kommt aus dem Workflow-Definition-Key — Permission-Schema ist seit
    // Slice 6.3d-iv vollstaendig definitionsgetrieben.
    public static string WorkflowCreate(string workflowDefinitionKey)
    {
        var normalized = string.IsNullOrWhiteSpace(workflowDefinitionKey)
            ? string.Empty
            : workflowDefinitionKey.Trim().ToLowerInvariant();

        return $"{WorkflowCreatePrefix}{normalized}";
    }

    public static bool IsWorkflowCreatePermission(string permissionKey)
    {
        return !string.IsNullOrWhiteSpace(permissionKey)
            && permissionKey.StartsWith(WorkflowCreatePrefix, StringComparison.OrdinalIgnoreCase);
    }
}
