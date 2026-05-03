namespace API;

internal interface IWorkflowAutomationReadRepository
{
    Task<IReadOnlyList<ActionDefinitionDto>> GetAdminActionDefinitions(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AutomationJobDetailDto>> GetAutomationJobs(Guid workflowUid, CancellationToken cancellationToken = default);
}
