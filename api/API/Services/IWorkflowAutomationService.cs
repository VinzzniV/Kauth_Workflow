namespace API;

internal interface IWorkflowAutomationService
{
    Task<AdminListPageDto<ActionDefinitionDto>> GetActionDefinitionsAsync(AdminListQuery query, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AutomationJobDetailDto>> GetWorkflowAutomationJobsAsync(Guid workflowUid, CancellationToken cancellationToken = default);
    Task<bool> TryProcessNextPendingJobAsync(CancellationToken cancellationToken = default);
}
