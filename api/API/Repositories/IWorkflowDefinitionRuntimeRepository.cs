namespace API;

internal interface IWorkflowDefinitionRuntimeRepository
{
    Task<WorkflowDefinitionVersionDetailDto?> PublishWorkflowDefinitionVersion(long versionId);
    Task<WorkflowDefinitionRuntimeDetailDto?> GetWorkflowDefinitionRuntimeDetail(Guid workflowUid);
    Task<List<WorkflowRuntimeEventDto>> GetWorkflowDefinitionRuntimeEvents(Guid workflowUid);
    Task<CursorPageDto<WorkflowRuntimeEventDto>> GetWorkflowDefinitionRuntimeEvents(Guid workflowUid, CursorPageQuery query);
}
