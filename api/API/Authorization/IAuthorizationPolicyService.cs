namespace API;

internal interface IAuthorizationPolicyService
{
    bool HasAnyRole(CurrentUser user, params string[] roleKeys);
    bool CanReadAllowedViews(CurrentUser user);
    bool CanAccessWorkflowOverview(CurrentUser user);
    bool CanReadWorkflow(CurrentUser user, string workflowStatus);
    bool CanRegularlyEditWorkflow(CurrentUser user, string workflowStatus);
    bool CanCreateOrStartWorkflow(CurrentUser user);
    bool CanEditSupervisorRequirements(CurrentUser user);
    bool CanAccessSupervisorStep(CurrentUser user);
    bool CanAccessTechnicalTasks(CurrentUser user);
    bool CanAccessTaskStatusUpdates(CurrentUser user);
    bool CanManageAdminConfiguration(CurrentUser user);
    bool CanViewTaskAssigneeIdentity(CurrentUser user);
    bool CanObserveWorkflow(CurrentUser user, int workflowDepartmentId, string workflowStatus, IReadOnlySet<int>? observableDepartmentIds);
    bool CanAccessAssignedSupervisorWorkflow(CurrentUser user, int workflowDepartmentId, string workflowStatus, IReadOnlySet<int> assignedDepartmentIds);
    bool CanUpdateTaskStatus(CurrentUser user, TaskWithWorkflowDto task);
    bool CanUpdateTaskAssignment(CurrentUser user, TaskWithWorkflowDto task);
}
