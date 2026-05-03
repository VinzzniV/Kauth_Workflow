namespace API;

internal sealed class WorkflowVisibilityService(
    IWorkflowRepository repository,
    IAuthorizationPolicyService authorizationPolicyService) : IWorkflowVisibilityService
{
    public async Task<HashSet<int>?> GetObservableWorkflowDepartmentIds(CurrentUser currentUser)
    {
        if (authorizationPolicyService.CanManageAdminConfiguration(currentUser)
            || authorizationPolicyService.HasPermission(currentUser, AuthorizationPermissions.WorkflowsViewAll)
            || authorizationPolicyService.HasPermission(currentUser, AuthorizationPermissions.UsersViewAllDepartments))
        {
            return null;
        }

        var departmentIds = new HashSet<int>();
        departmentIds.UnionWith(currentUser.GetPermissionDepartmentIds(AuthorizationPermissions.WorkflowsViewDepartment));
        departmentIds.UnionWith(currentUser.GetPermissionDepartmentIds(AuthorizationPermissions.UsersViewDepartment));
        departmentIds.UnionWith(currentUser.GetPermissionDepartmentIds(AuthorizationPermissions.TasksExecuteSupervisor));
        departmentIds.UnionWith(currentUser.GetPermissionDepartmentIds(AuthorizationPermissions.TasksExecuteDepartment));

        if (authorizationPolicyService.CanAccessSupervisorStep(currentUser))
        {
            departmentIds.UnionWith(await repository.GetRequirementSelectionDepartmentIds(currentUser.UserId));

            foreach (var responsibility in currentUser.EffectiveResponsibilities)
            {
                if (string.Equals(responsibility.ResponsibilityType, "department_lead", StringComparison.OrdinalIgnoreCase)
                    && responsibility.DepartmentId.HasValue)
                {
                    departmentIds.Add(responsibility.DepartmentId.Value);
                }
            }
        }

        var hasDepartmentScopedAccess = departmentIds.Count > 0
            || currentUser.HasPermission(AuthorizationPermissions.WorkflowsViewDepartment)
            || currentUser.HasPermission(AuthorizationPermissions.UsersViewDepartment)
            || currentUser.HasPermission(AuthorizationPermissions.TasksExecuteSupervisor)
            || currentUser.HasPermission(AuthorizationPermissions.TasksExecuteDepartment)
            || currentUser.HasRole(AuthorizationRoles.Manager);

        return hasDepartmentScopedAccess ? departmentIds : null;
    }

    public bool CanObserveWorkflow(
        CurrentUser currentUser,
        int workflowDepartmentId,
        string workflowStatus,
        HashSet<int>? observableDepartmentIds)
    {
        return authorizationPolicyService.CanObserveWorkflow(
            currentUser,
            workflowDepartmentId,
            workflowStatus,
            observableDepartmentIds);
    }

    public void ApplyWorkflowTaskPermissions(WorkflowDetailDto workflow, CurrentUser currentUser)
    {
        var shouldRedactAssignments = ShouldRedactTaskAssignments(currentUser);
        var shouldRedactComments = ShouldRedactTaskComments(currentUser);
        var shouldRedactAssigneeIdentity = ShouldRedactTaskAssigneeIdentity(currentUser);

        foreach (var task in workflow.Tasks)
        {
            var taskContext = new TaskWithWorkflowDto
            {
                TaskRef = WorkflowTaskRef.Build(task.Id),
                TaskFamily = TaskFamilyNames.Workflow,
                Task = task,
                Workflow = new TaskWorkflowContextDto
                {
                    WorkflowId = 0,
                    WorkflowUid = workflow.Uid,
                    WorkflowStatus = workflow.WorkflowStatus,
                    WorkflowCreatedAt = workflow.CreatedAt,
                    FirstName = workflow.FirstName,
                    LastName = workflow.LastName,
                    EmployeeNumber = workflow.EmployeeNumber,
                    BadgeNumber = workflow.BadgeNumber,
                    DepartmentId = workflow.DepartmentId,
                    DepartmentName = workflow.DepartmentName,
                    RoleId = workflow.RoleId,
                    RoleName = workflow.RoleName
                },
                Rotation = null
            };

            task.CanUpdateStatus = authorizationPolicyService.CanUpdateTaskStatus(currentUser, taskContext);
            task.CanDecideApproval = authorizationPolicyService.CanDecideTaskApproval(currentUser, taskContext);
            task.CanAddComment = authorizationPolicyService.CanAddTaskComment(currentUser, taskContext);

            if (shouldRedactComments)
            {
                task.Comments.Clear();
            }

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

    public void ApplyTaskPermissions(IEnumerable<TaskWithWorkflowDto> tasks, CurrentUser currentUser)
    {
        foreach (var task in tasks)
        {
            ApplyTaskPermissions(task, currentUser);
        }
    }

    public void ApplyTaskPermissions(TaskWithWorkflowDto task, CurrentUser currentUser)
    {
        task.Task.CanUpdateStatus = authorizationPolicyService.CanUpdateTaskStatus(currentUser, task);
        task.Task.CanDecideApproval = authorizationPolicyService.CanDecideTaskApproval(currentUser, task);
        task.Task.CanAddComment = authorizationPolicyService.CanAddTaskComment(currentUser, task);
        if (ShouldRedactTaskComments(currentUser))
        {
            task.Task.Comments.Clear();
        }

        if (ShouldRedactTaskAssignments(currentUser))
        {
            task.Task.Assignments.Clear();
            return;
        }

        if (ShouldRedactTaskAssigneeIdentity(currentUser))
        {
            RedactTaskAssigneeIdentity(task.Task);
        }
    }

    private bool ShouldRedactTaskAssignments(CurrentUser currentUser)
    {
        return authorizationPolicyService.HasAnyRole(currentUser, AuthorizationRoles.Reader)
            && !authorizationPolicyService.HasAnyRole(
                currentUser,
                AuthorizationRoles.Admin,
                AuthorizationRoles.Hr,
                AuthorizationRoles.Manager,
                AuthorizationRoles.Worker);
    }

    private bool ShouldRedactTaskComments(CurrentUser currentUser)
    {
        return ShouldRedactTaskAssignments(currentUser);
    }

    private bool ShouldRedactTaskAssigneeIdentity(CurrentUser currentUser)
    {
        return !authorizationPolicyService.CanViewTaskAssigneeIdentity(currentUser);
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
}
