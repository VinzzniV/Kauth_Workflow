namespace API;

// Zentrale Policy-Schicht fuer Rollen- und Aufgabenfreigaben innerhalb der API.
internal sealed class AuthorizationPolicyService : IAuthorizationPolicyService
{
    // Die meisten Regeln lassen sich auf "hat mindestens eine dieser Rollen" reduzieren.
    public bool HasAnyRole(CurrentUser user, params string[] roleKeys)
    {
        if (roleKeys.Length == 0)
        {
            return false;
        }

        var roleSet = user.EffectiveRoles
            .Select(role => role.RoleKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return roleKeys.Any(roleSet.Contains);
    }

    public bool HasPermission(CurrentUser user, string permissionKey, int? departmentId = null)
    {
        if (string.IsNullOrWhiteSpace(permissionKey))
        {
            return false;
        }

        if (user.HasPermission(permissionKey, departmentId))
        {
            return true;
        }

        return HasLegacyRolePermission(user, permissionKey, departmentId);
    }

    public bool CanReadAllowedViews(CurrentUser user)
    {
        return HasPermission(user, AuthorizationPermissions.AppAccess)
            || HasAnyRole(user, AuthorizationRoles.ReadAllowed);
    }

    public bool CanAccessWorkflowOverview(CurrentUser user)
    {
        return HasPermission(user, AuthorizationPermissions.WorkflowsViewAll)
            || HasPermission(user, AuthorizationPermissions.WorkflowsViewDepartment)
            || HasAnyRole(user, AuthorizationRoles.Hr, AuthorizationRoles.Admin, AuthorizationRoles.Reader, AuthorizationRoles.Manager);
    }

    // Aktive Workflows sind fuer HR und Admin lesbar; reine Leser sehen nur abgeschlossene Faelle.
    public bool CanReadWorkflow(CurrentUser user, string workflowStatus)
    {
        if (HasPermission(user, AuthorizationPermissions.WorkflowsViewAll)
            || HasPermission(user, AuthorizationPermissions.WorkflowsViewDepartment)
            || HasAnyRole(user, AuthorizationRoles.Admin, AuthorizationRoles.Hr))
        {
            return true;
        }

        return WorkflowStatusRules.IsTerminal(workflowStatus) && HasAnyRole(user, AuthorizationRoles.Reader);
    }

    // Die regulaere Bearbeitung ist strikt an die aktuelle Workflow-Phase gebunden.
    public bool CanRegularlyEditWorkflow(CurrentUser user, string workflowStatus)
    {
        return WorkflowStatusRules.Normalize(workflowStatus) switch
        {
            WorkflowStatusRules.Draft => HasAnyRole(user, AuthorizationRoles.Hr),
            WorkflowStatusRules.WaitingForSupervisor => HasAnyRole(user, AuthorizationRoles.Manager),
            WorkflowStatusRules.WaitingForDepartment => HasAnyRole(user, AuthorizationRoles.Worker),
            WorkflowStatusRules.InProgress => HasAnyRole(user, AuthorizationRoles.Worker),
            _ => false,
        };
    }

    public bool CanCreateWorkflow(CurrentUser user)
    {
        return user.EffectivePermissions.Any(permission =>
                   AuthorizationPermissions.IsWorkflowCreatePermission(permission.PermissionKey))
               || HasAnyRole(user, AuthorizationRoles.Hr, AuthorizationRoles.Manager, AuthorizationRoles.Admin);
    }

    public bool CanCreateWorkflowForProcessType(CurrentUser user, string processTypeKey, bool managerCreatableProcessType)
    {
        if (HasPermission(user, AuthorizationPermissions.WorkflowCreate(processTypeKey)))
        {
            return true;
        }

        if (HasAnyRole(user, AuthorizationRoles.Admin, AuthorizationRoles.Hr))
        {
            return true;
        }

        return managerCreatableProcessType && HasAnyRole(user, AuthorizationRoles.Manager);
    }

    public bool CanCreateOrStartWorkflow(CurrentUser user)
    {
        return HasPermission(user, AuthorizationPermissions.WorkflowsViewAll)
               && CanCreateWorkflow(user);
    }

    public bool CanEditSupervisorRequirements(CurrentUser user)
    {
        return HasPermission(user, AuthorizationPermissions.TasksExecuteSupervisor)
               || HasPermission(user, AuthorizationPermissions.TasksAssignOverride)
               || HasAnyRole(user, AuthorizationRoles.Manager, AuthorizationRoles.Admin)
               || HasDepartmentLeadSupervisorResponsibility(user);
    }

    public bool CanAccessSupervisorStep(CurrentUser user)
    {
        return HasPermission(user, AuthorizationPermissions.TasksExecuteSupervisor)
               || HasAnyRole(user, AuthorizationRoles.Manager)
               || HasDepartmentLeadSupervisorResponsibility(user);
    }

    public bool CanAccessTechnicalTasks(CurrentUser user)
    {
        return HasPermission(user, AuthorizationPermissions.TasksExecuteDepartment)
               || HasAnyRole(user, AuthorizationRoles.Worker);
    }

    public bool CanAccessTaskStatusUpdates(CurrentUser user)
    {
        return HasPermission(user, AuthorizationPermissions.TasksExecuteDepartment)
               || HasPermission(user, AuthorizationPermissions.TasksAssignOverride)
               || HasAnyRole(user, AuthorizationRoles.Worker, AuthorizationRoles.Admin);
    }

    public bool CanAccessWorkflowBuilder(CurrentUser user)
    {
        return CanCreateWorkflow(user)
               || CanManageWorkflowBuilderAdvanced(user);
    }

    public bool CanManageWorkflowBuilderAdvanced(CurrentUser user)
    {
        return CanManageAdminConfiguration(user);
    }

    public bool CanManageAdminConfiguration(CurrentUser user)
    {
        return HasPermission(user, AuthorizationPermissions.AdminPermissionsManage)
               || HasAnyRole(user, AuthorizationRoles.Admin);
    }

    public bool CanViewTaskAssigneeIdentity(CurrentUser user)
    {
        return HasPermission(user, AuthorizationPermissions.TasksAssignOverride)
               || CanManageAdminConfiguration(user)
               || HasAnyRole(user, AuthorizationRoles.Admin);
    }

    public bool CanObserveWorkflow(
        CurrentUser user,
        int workflowDepartmentId,
        string workflowStatus,
        IReadOnlySet<int>? observableDepartmentIds)
    {
        if (CanManageAdminConfiguration(user)
            || HasPermission(user, AuthorizationPermissions.WorkflowsViewAll))
        {
            return true;
        }

        if (HasDepartmentScopedPermission(user, AuthorizationPermissions.WorkflowsViewDepartment, workflowDepartmentId))
        {
            return true;
        }

        var hasDepartmentScopedWorkflowAccess =
            HasPermission(user, AuthorizationPermissions.WorkflowsViewDepartment)
            || HasAnyRole(user, AuthorizationRoles.Manager);

        if (hasDepartmentScopedWorkflowAccess || CanAccessSupervisorStep(user))
        {
            return observableDepartmentIds?.Contains(workflowDepartmentId) == true;
        }

        return CanReadWorkflow(user, workflowStatus);
    }

    public bool CanAccessAssignedSupervisorWorkflow(
        CurrentUser user,
        int workflowDepartmentId,
        string workflowStatus,
        IReadOnlySet<int> assignedDepartmentIds)
    {
        if (!WorkflowStatusRules.IsWaitingForSupervisor(workflowStatus))
        {
            return false;
        }

        if (CanManageAdminConfiguration(user))
        {
            return true;
        }

        return CanAccessSupervisorStep(user) && assignedDepartmentIds.Contains(workflowDepartmentId);
    }

    // Statusaenderungen folgen sowohl der Workflow-Phase als auch der konkreten Aufgaben-Zuweisung.
    public bool CanUpdateTaskStatus(CurrentUser user, TaskWithWorkflowDto task)
    {
        if (WorkflowStatusRules.IsTerminal(task.Workflow.WorkflowStatus))
        {
            return false;
        }

        if (task.Task.IsApprovalTask)
        {
            return false;
        }

        if (HasPermission(user, AuthorizationPermissions.TasksAssignOverride)
            || HasAnyRole(user, AuthorizationRoles.Admin))
        {
            return true;
        }

        if (!CanRegularlyEditWorkflow(user, task.Workflow.WorkflowStatus))
        {
            return false;
        }

        return MatchesTaskAssignment(user, task);
    }

    public bool CanDecideTaskApproval(CurrentUser user, TaskWithWorkflowDto task)
    {
        if (WorkflowStatusRules.IsTerminal(task.Workflow.WorkflowStatus)
            || !task.Task.IsApprovalTask
            || TerminalTaskStatuses.Contains(task.Task.Status))
        {
            return false;
        }

        if (HasPermission(user, AuthorizationPermissions.TasksAssignOverride)
            || HasAnyRole(user, AuthorizationRoles.Admin))
        {
            return true;
        }

        if (!CanAccessSupervisorStep(user))
        {
            return false;
        }

        return MatchesTaskAssignment(user, task);
    }

    // Umverteilungen bleiben ein expliziter Admin-Eingriff und sind keine regulaere Fachbearbeitung.
    public bool CanUpdateTaskAssignment(CurrentUser user, TaskWithWorkflowDto task)
    {
        return !WorkflowStatusRules.IsTerminal(task.Workflow.WorkflowStatus)
            && (HasPermission(user, AuthorizationPermissions.TasksAssignOverride)
                || HasAnyRole(user, AuthorizationRoles.Admin));
    }

    public bool CanAddTaskComment(CurrentUser user, TaskWithWorkflowDto task)
    {
        if (WorkflowStatusRules.IsTerminal(task.Workflow.WorkflowStatus)
            || TerminalTaskStatuses.Contains(task.Task.Status))
        {
            return false;
        }

        if (HasAnyRole(user, AuthorizationRoles.Admin, AuthorizationRoles.Hr, AuthorizationRoles.Manager))
        {
            return true;
        }

        return CanRegularlyEditWorkflow(user, task.Workflow.WorkflowStatus)
            && MatchesTaskAssignment(user, task);
    }

    private static bool MatchesTaskAssignment(CurrentUser user, TaskWithWorkflowDto task)
    {
        var effectiveResponsibilityIds = user.EffectiveResponsibilities
            .Select(responsibility => responsibility.ResponsibilityId)
            .ToHashSet();

        return task.Task.Assignments
            .Where(assignment => assignment.IsPrimary)
            .Any(assignment => MatchesPrimaryAssignment(user.UserId, effectiveResponsibilityIds, assignment));
    }

    private static bool MatchesPrimaryAssignment(
        long userId,
        HashSet<int> directResponsibilityIds,
        WorkflowTaskAssignmentDto assignment)
    {
        var assignmentType = assignment.AssignmentType.Trim().ToLowerInvariant();

        return assignmentType switch
        {
            "user" => assignment.AssigneeUserId.HasValue && assignment.AssigneeUserId.Value == userId,
            "responsibility" => assignment.AssigneeResponsibilityId.HasValue
                && directResponsibilityIds.Contains(assignment.AssigneeResponsibilityId.Value),
            _ => false
        };
    }

    private static readonly HashSet<string> TerminalTaskStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "done"
    };

    private static bool HasDepartmentLeadSupervisorResponsibility(CurrentUser user)
    {
        return user.EffectiveResponsibilities.Any(responsibility =>
            string.Equals(responsibility.ResponsibilityType, "department_lead", StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasDepartmentScopedPermission(CurrentUser user, string permissionKey, int departmentId)
    {
        return user.GetPermissionDepartmentIds(permissionKey).Contains(departmentId);
    }

    private static bool HasLegacyRolePermission(CurrentUser user, string permissionKey, int? departmentId)
    {
        var normalizedPermissionKey = permissionKey.Trim();
        return normalizedPermissionKey switch
        {
            AuthorizationPermissions.AppAccess => AuthorizationRoles.ReadAllowed.Any(user.HasRole),
            AuthorizationPermissions.UsersViewAllDepartments => user.HasRole(AuthorizationRoles.Hr) || user.HasRole(AuthorizationRoles.Admin),
            AuthorizationPermissions.UsersViewDepartment => !departmentId.HasValue && user.HasRole(AuthorizationRoles.Manager),
            AuthorizationPermissions.WorkflowsViewAll => user.HasRole(AuthorizationRoles.Hr) || user.HasRole(AuthorizationRoles.Admin),
            AuthorizationPermissions.WorkflowsViewDepartment => !departmentId.HasValue && user.HasRole(AuthorizationRoles.Manager),
            AuthorizationPermissions.TasksExecuteSupervisor => user.HasRole(AuthorizationRoles.Manager) || user.HasRole(AuthorizationRoles.Admin),
            AuthorizationPermissions.TasksExecuteDepartment => user.HasRole(AuthorizationRoles.Worker) || user.HasRole(AuthorizationRoles.Admin),
            AuthorizationPermissions.TasksAssignOverride => user.HasRole(AuthorizationRoles.Admin),
            AuthorizationPermissions.AdminDirectoryManage => user.HasRole(AuthorizationRoles.Admin),
            AuthorizationPermissions.AdminPermissionsManage => user.HasRole(AuthorizationRoles.Admin),
            _ when AuthorizationPermissions.IsWorkflowCreatePermission(normalizedPermissionKey)
                => user.HasRole(AuthorizationRoles.Hr) || user.HasRole(AuthorizationRoles.Admin),
            _ => false
        };
    }
}
