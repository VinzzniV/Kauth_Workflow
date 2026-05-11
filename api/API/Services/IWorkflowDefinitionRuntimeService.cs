namespace API;

internal interface IWorkflowDefinitionRuntimeService
{
    Task<WorkflowDefinitionRuntimeDetailDto> CreateWorkflowInstanceAsync(
        CreateWorkflowDefinitionInstanceRequest request,
        CurrentUser currentUser,
        CancellationToken cancellationToken = default);
    Task<WorkflowDefinitionRuntimeDetailDto?> GetWorkflowInstanceAsync(
        Guid workflowUid,
        CurrentUser currentUser,
        CancellationToken cancellationToken = default);
    Task<CursorPageDto<WorkflowRuntimeEventDto>> GetWorkflowInstanceEventsAsync(
        Guid workflowUid,
        CursorPageQuery query,
        CurrentUser currentUser,
        CancellationToken cancellationToken = default);
    Task<WorkflowDefinitionRuntimeDetailDto?> CompleteFormNodeAsync(
        Guid workflowUid,
        long nodeInstanceId,
        CompleteRuntimeFormNodeRequest request,
        CurrentUser currentUser,
        CancellationToken cancellationToken = default);
    Task<WorkflowDefinitionRuntimeDetailDto?> CompleteApprovalNodeAsync(
        Guid workflowUid,
        long nodeInstanceId,
        CompleteRuntimeApprovalNodeRequest request,
        CurrentUser currentUser,
        CancellationToken cancellationToken = default);
    Task<WorkflowDefinitionRuntimeDetailDto?> CompleteTaskNodeAsync(
        Guid workflowUid,
        long nodeInstanceId,
        CompleteRuntimeTaskNodeRequest request,
        CurrentUser currentUser,
        CancellationToken cancellationToken = default);
}
