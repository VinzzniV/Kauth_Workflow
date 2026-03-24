namespace API;

// Zentrale Policy-Schicht fuer Rollen- und Aufgabenfreigaben innerhalb der API.
internal sealed class AuthorizationPolicyService : IAuthorizationPolicyService
{
    private const string SupervisorRequirementTaskKey = "supervisor_fills_document";

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

    public bool CanReadAllowedViews(CurrentUser user)
    {
        return HasAnyRole(user, AuthorizationRoles.ReadAllowed);
    }

    public bool CanAccessWorkflowOverview(CurrentUser user)
    {
        return HasAnyRole(user, AuthorizationRoles.Hr, AuthorizationRoles.Admin, AuthorizationRoles.Reader, AuthorizationRoles.Manager);
    }

    // Aktive Workflows sind fuer HR und Admin lesbar; reine Leser sehen nur abgeschlossene Faelle.
    public bool CanReadWorkflow(CurrentUser user, string workflowStatus)
    {
        if (HasAnyRole(user, AuthorizationRoles.Admin, AuthorizationRoles.Hr))
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

    public bool CanCreateOrStartWorkflow(CurrentUser user)
    {
        return HasAnyRole(user, AuthorizationRoles.Hr);
    }

    public bool CanEditSupervisorRequirements(CurrentUser user)
    {
        return HasAnyRole(user, AuthorizationRoles.Manager, AuthorizationRoles.Admin);
    }

    public bool CanAccessSupervisorStep(CurrentUser user)
    {
        return HasAnyRole(user, AuthorizationRoles.Manager);
    }

    public bool CanAccessTechnicalTasks(CurrentUser user)
    {
        return HasAnyRole(user, AuthorizationRoles.Worker);
    }

    public bool CanAccessTaskStatusUpdates(CurrentUser user)
    {
        return HasAnyRole(user, AuthorizationRoles.Worker, AuthorizationRoles.Admin);
    }

    public bool CanManageAdminConfiguration(CurrentUser user)
    {
        return HasAnyRole(user, AuthorizationRoles.Admin);
    }

    public bool CanViewTaskAssigneeIdentity(CurrentUser user)
    {
        return HasAnyRole(user, AuthorizationRoles.Admin);
    }

    public bool CanObserveWorkflow(
        CurrentUser user,
        int workflowDepartmentId,
        string workflowStatus,
        IReadOnlySet<int>? observableDepartmentIds)
    {
        if (CanManageAdminConfiguration(user) || CanCreateOrStartWorkflow(user))
        {
            return true;
        }

        if (CanAccessSupervisorStep(user))
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

        if (task.Task.TaskKey.Equals(SupervisorRequirementTaskKey, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (HasAnyRole(user, AuthorizationRoles.Admin))
        {
            return true;
        }

        if (!CanRegularlyEditWorkflow(user, task.Workflow.WorkflowStatus))
        {
            return false;
        }

        return MatchesTaskAssignment(user, task);
    }

    // Umverteilungen bleiben ein expliziter Admin-Eingriff und sind keine regulaere Fachbearbeitung.
    public bool CanUpdateTaskAssignment(CurrentUser user, TaskWithWorkflowDto task)
    {
        return !WorkflowStatusRules.IsTerminal(task.Workflow.WorkflowStatus)
            && HasAnyRole(user, AuthorizationRoles.Admin);
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
}
