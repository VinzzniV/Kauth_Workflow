namespace API;

internal interface IWorkflowAutomationReadRepository
{
    Task<AdminListPageDto<ActionDefinitionDto>> GetAdminActionDefinitions(AdminListQuery query, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AutomationJobDetailDto>> GetAutomationJobs(Guid workflowUid, CancellationToken cancellationToken = default);
    Task<CursorPageDto<AutomationJobDetailDto>> GetAutomationJobs(Guid workflowUid, CursorPageQuery query, CancellationToken cancellationToken = default);
}
