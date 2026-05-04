namespace API;

internal interface IWorkflowLifecycleService
{
    Task<TaskWithWorkflowDto?> UpdateTaskStatusAsync(long taskId, string status, long actorUserId);
    Task<TaskWithWorkflowDto?> UpdateTaskStatusByRefAsync(string taskRef, string status, long actorUserId);
    Task<TaskWithWorkflowDto?> DecideTaskApprovalAsync(long taskId, TaskApprovalDecisionRequest request, long actorUserId);
    Task<TaskWithWorkflowDto?> DecideTaskApprovalByRefAsync(string taskRef, TaskApprovalDecisionRequest request, long actorUserId);
    Task OnAutomationJobCompletedAsync(ClaimedAutomationJobRecord job, WorkflowAutomationHandlerResult result, CancellationToken cancellationToken);
    Task<WorkflowDefinitionRuntimeDetailDto> CreateWorkflowInstanceAsync(CreateWorkflowDefinitionInstanceRequest request, long actorUserId);
    Task<WorkflowDefinitionRuntimeDetailDto?> CompleteFormNodeAsync(Guid workflowUid, long nodeInstanceId, CompleteRuntimeFormNodeRequest request, long actorUserId);
    Task<WorkflowDefinitionRuntimeDetailDto?> CompleteApprovalNodeAsync(Guid workflowUid, long nodeInstanceId, CompleteRuntimeApprovalNodeRequest request, long actorUserId);
    Task<WorkflowDefinitionRuntimeDetailDto?> CompleteTaskNodeAsync(Guid workflowUid, long nodeInstanceId, CompleteRuntimeTaskNodeRequest request, long actorUserId);
}
