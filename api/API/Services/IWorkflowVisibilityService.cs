namespace API;

internal interface IWorkflowVisibilityService
{
    Task<HashSet<int>?> GetObservableWorkflowDepartmentIds(CurrentUser currentUser);
    bool CanObserveWorkflow(CurrentUser currentUser, int workflowDepartmentId, string workflowStatus, HashSet<int>? observableDepartmentIds);
    void ApplyWorkflowTaskPermissions(WorkflowDetailDto workflow, CurrentUser currentUser);
    void ApplyTaskPermissions(IEnumerable<TaskWithWorkflowDto> tasks, CurrentUser currentUser);
    void ApplyTaskPermissions(TaskWithWorkflowDto task, CurrentUser currentUser);
}
