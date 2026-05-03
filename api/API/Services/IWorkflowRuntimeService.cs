namespace API;

internal interface IWorkflowRuntimeService
{
    Task<WorkflowTargetPersonDto> CreatePersonAsync(CreatePersonRequest request, CurrentUser currentUser, CancellationToken cancellationToken = default);
    Task<WorkflowCreateResponse> CreateWorkflowAsync(CreateWorkflowRequest request, CurrentUser currentUser, CancellationToken cancellationToken = default);
    Task<WorkflowListPageDto> GetWorkflowsAsync(
        string? status,
        int? department,
        string? workflowDefinitionKey,
        string? search,
        string? responsibility,
        int? limit,
        int? offset,
        CurrentUser currentUser,
        CancellationToken cancellationToken = default);
    Task<WorkflowDetailDto?> GetWorkflowByUidAsync(Guid workflowUid, CurrentUser currentUser, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WorkflowAuditEntryDto>?> GetWorkflowAuditLogAsync(Guid workflowUid, int limit, int offset, CurrentUser currentUser, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TaskWithWorkflowDto>?> GetWorkflowTasksAsync(Guid workflowUid, CurrentUser currentUser, CancellationToken cancellationToken = default);
    Task<bool> ArchiveWorkflowAsync(Guid workflowUid, CurrentUser currentUser, CancellationToken cancellationToken = default);
    Task<bool> DeleteWorkflowAsync(Guid workflowUid, CurrentUser currentUser, CancellationToken cancellationToken = default);
    Task<PersonWorkflowHistoryDto?> GetPersonWorkflowHistoryAsync(long personId, CurrentUser currentUser, CancellationToken cancellationToken = default);
}
