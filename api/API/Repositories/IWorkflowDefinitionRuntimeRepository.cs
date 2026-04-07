namespace API;

internal interface IWorkflowDefinitionRuntimeRepository
{
    Task<WorkflowDefinitionVersionDetailDto?> PublishWorkflowDefinitionVersion(long versionId);
    Task<WorkflowDefinitionRuntimeDetailDto> CreateWorkflowDefinitionInstance(
        CreateWorkflowDefinitionInstanceRequest request,
        long createdByUserId);
    Task<WorkflowDefinitionRuntimeDetailDto?> GetWorkflowDefinitionRuntimeDetail(Guid workflowUid);
    Task<List<WorkflowRuntimeEventDto>> GetWorkflowDefinitionRuntimeEvents(Guid workflowUid);
    Task<WorkflowDefinitionRuntimeDetailDto?> CompleteRuntimeFormNode(
        Guid workflowUid,
        long nodeInstanceId,
        CompleteRuntimeFormNodeRequest request,
        long actorUserId);
    Task<WorkflowDefinitionRuntimeDetailDto?> CompleteRuntimeApprovalNode(
        Guid workflowUid,
        long nodeInstanceId,
        CompleteRuntimeApprovalNodeRequest request,
        long actorUserId);
    Task<WorkflowDefinitionRuntimeDetailDto?> CompleteRuntimeTaskNode(
        Guid workflowUid,
        long nodeInstanceId,
        CompleteRuntimeTaskNodeRequest request,
        long actorUserId);
}
