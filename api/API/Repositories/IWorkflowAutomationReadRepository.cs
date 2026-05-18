namespace API;

internal interface IWorkflowAutomationReadRepository
{
    Task<AdminListPageDto<ActionDefinitionDto>> GetAdminActionDefinitions(AdminListQuery query, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AutomationJobDetailDto>> GetAutomationJobs(Guid workflowUid, CancellationToken cancellationToken = default);
    Task<CursorPageDto<AutomationJobDetailDto>> GetAutomationJobs(Guid workflowUid, CursorPageQuery query, CancellationToken cancellationToken = default);
    // Slice 7: Live-Status-Aggregation pro Approval. Filter auf workflow_node_instance_id
    // (long/bigint; 1:1 mit Approval ueber automation_approvals.workflow_node_instance_id).
    Task<IReadOnlyList<AutomationJobDetailDto>> GetAutomationJobsForNodeInstance(long workflowNodeInstanceId, CancellationToken cancellationToken = default);
}
