using Microsoft.AspNetCore.Http;

namespace API;

internal static class EndpointSupport
{
    public static MeDto ToMeDto(CurrentUser currentUser)
    {
        var username = !string.IsNullOrWhiteSpace(currentUser.ExternalKey)
            ? currentUser.ExternalKey!
            : currentUser.Email;

        var roles = currentUser.EffectiveRoles
            .Select(role => role.RoleKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(roleKey => roleKey, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var groups = currentUser.Groups
            .Select(group => group.GroupKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(groupKey => groupKey, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new MeDto
        {
            Username = username,
            DisplayName = currentUser.DisplayName,
            Email = currentUser.Email,
            Roles = roles,
            Groups = groups
        };
    }

    public static void ApplyWorkflowTaskPermissions(
        WorkflowDetailDto workflow,
        CurrentUser currentUser,
        IAuthorizationPolicyService authorizationPolicy)
    {
        var shouldRedactAssignments = ShouldRedactTaskAssignments(currentUser, authorizationPolicy);
        var shouldRedactAssigneeIdentity = ShouldRedactTaskAssigneeIdentity(currentUser, authorizationPolicy);

        foreach (var task in workflow.Tasks)
        {
            task.CanUpdateStatus = authorizationPolicy.CanUpdateTaskStatus(
                currentUser,
                new TaskWithWorkflowDto
                {
                    Task = task,
                    Workflow = new TaskWorkflowContextDto
                    {
                        WorkflowId = 0,
                        WorkflowUid = workflow.Uid,
                        WorkflowStatus = workflow.WorkflowStatus,
                        WorkflowLegacyStatus = WorkflowStatusRules.ToLegacyStatus(workflow.WorkflowStatus),
                        WorkflowCreatedAt = workflow.CreatedAt,
                        FirstName = workflow.FirstName,
                        LastName = workflow.LastName,
                        EmployeeNumber = workflow.EmployeeNumber,
                        BadgeNumber = workflow.BadgeNumber,
                        DepartmentId = workflow.DepartmentId,
                        DepartmentName = workflow.DepartmentName,
                        RoleId = workflow.RoleId,
                        RoleName = workflow.RoleName
                    }
                });

            if (shouldRedactAssignments)
            {
                task.Assignments.Clear();
                continue;
            }

            if (shouldRedactAssigneeIdentity)
            {
                RedactTaskAssigneeIdentity(task);
            }
        }
    }

    public static void ApplyTaskPermissions(
        IEnumerable<TaskWithWorkflowDto> tasks,
        CurrentUser currentUser,
        IAuthorizationPolicyService authorizationPolicy)
    {
        foreach (var task in tasks)
        {
            ApplyTaskPermissions(task, currentUser, authorizationPolicy);
        }
    }

    public static void ApplyTaskPermissions(
        TaskWithWorkflowDto task,
        CurrentUser currentUser,
        IAuthorizationPolicyService authorizationPolicy)
    {
        task.Task.CanUpdateStatus = authorizationPolicy.CanUpdateTaskStatus(currentUser, task);
        if (ShouldRedactTaskAssignments(currentUser, authorizationPolicy))
        {
            task.Task.Assignments.Clear();
            return;
        }

        if (ShouldRedactTaskAssigneeIdentity(currentUser, authorizationPolicy))
        {
            RedactTaskAssigneeIdentity(task.Task);
        }
    }

    private static bool ShouldRedactTaskAssignments(
        CurrentUser currentUser,
        IAuthorizationPolicyService authorizationPolicy)
    {
        return authorizationPolicy.HasAnyRole(currentUser, AuthorizationRoles.Reader)
            && !authorizationPolicy.HasAnyRole(
                currentUser,
                AuthorizationRoles.Admin,
                AuthorizationRoles.Hr,
                AuthorizationRoles.Manager,
                AuthorizationRoles.Worker);
    }

    private static bool ShouldRedactTaskAssigneeIdentity(
        CurrentUser currentUser,
        IAuthorizationPolicyService authorizationPolicy)
    {
        return !authorizationPolicy.CanViewTaskAssigneeIdentity(currentUser);
    }

    private static void RedactTaskAssigneeIdentity(WorkflowTaskDto task)
    {
        foreach (var assignment in task.Assignments)
        {
            if (!string.Equals(assignment.AssignmentType, "user", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            assignment.AssigneeUserId = null;
            assignment.AssigneeUserName = null;
            assignment.AssigneeUserEmail = null;
        }
    }

    public static async Task<HashSet<int>?> GetObservableWorkflowDepartmentIds(
        CurrentUser currentUser,
        IWorkflowRepository repository,
        IAuthorizationPolicyService authorizationPolicy)
    {
        if (authorizationPolicy.CanManageAdminConfiguration(currentUser)
            || authorizationPolicy.CanCreateOrStartWorkflow(currentUser))
        {
            return null;
        }

        if (authorizationPolicy.CanAccessSupervisorStep(currentUser))
        {
            var departmentIds = await repository.GetRequirementSelectionDepartmentIds(currentUser.UserId);

            foreach (var responsibility in currentUser.EffectiveResponsibilities)
            {
                if (string.Equals(responsibility.ResponsibilityType, "department_lead", StringComparison.OrdinalIgnoreCase)
                    && responsibility.DepartmentId.HasValue)
                {
                    departmentIds.Add(responsibility.DepartmentId.Value);
                }
            }

            return departmentIds;
        }

        return null;
    }

    public static bool CanObserveWorkflow(
        CurrentUser currentUser,
        int workflowDepartmentId,
        string workflowStatus,
        HashSet<int>? observableDepartmentIds,
        IAuthorizationPolicyService authorizationPolicy)
    {
        return authorizationPolicy.CanObserveWorkflow(
            currentUser,
            workflowDepartmentId,
            workflowStatus,
            observableDepartmentIds);
    }

    public static string? ExtractBearerToken(string? authorizationHeader)
    {
        if (string.IsNullOrWhiteSpace(authorizationHeader))
        {
            return null;
        }

        const string bearerPrefix = "Bearer ";
        if (!authorizationHeader.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var token = authorizationHeader[bearerPrefix.Length..].Trim();
        return string.IsNullOrWhiteSpace(token) ? null : token;
    }

    public static async Task<(CurrentUser? User, IResult? Error)> RequireAuthorization(
        IUserContext userContext,
        Func<CurrentUser, bool> authorizationRule,
        string forbiddenMessage)
    {
        var currentUser = await userContext.GetCurrentUser();
        if (currentUser is null || !currentUser.IsActive)
        {
            return (null, Results.Unauthorized());
        }

        if (!authorizationRule(currentUser))
        {
            return (null, Forbidden(forbiddenMessage));
        }

        return (currentUser, null);
    }

    public static IResult Forbidden(string message)
    {
        return Results.Json(new { message }, statusCode: StatusCodes.Status403Forbidden);
    }
}
