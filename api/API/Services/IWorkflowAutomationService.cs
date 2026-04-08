namespace API;

internal interface IWorkflowAutomationService
{
    Task<IReadOnlyList<ActionDefinitionDto>> GetActionDefinitionsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AutomationJobDetailDto>> GetWorkflowAutomationJobsAsync(Guid workflowUid, CancellationToken cancellationToken = default);
    Task<bool> TryProcessNextPendingJobAsync(CancellationToken cancellationToken = default);
}
