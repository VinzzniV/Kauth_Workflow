namespace API;

internal interface IWorkflowAutomationService
{
    Task<AdminListPageDto<ActionDefinitionDto>> GetActionDefinitionsAsync(AdminListQuery query, CancellationToken cancellationToken = default);
    Task<CursorPageDto<AutomationJobDetailDto>> GetWorkflowAutomationJobsAsync(Guid workflowUid, CursorPageQuery query, CancellationToken cancellationToken = default);
    Task<bool> TryProcessNextPendingJobAsync(CancellationToken cancellationToken = default);
}
