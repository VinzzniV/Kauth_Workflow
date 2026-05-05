namespace API;

internal interface IWorkflowCatalogService
{
    Task<AdminListPageDto<DepartmentDto>> GetDepartmentsAsync(AdminListQuery query, CancellationToken cancellationToken = default);
    Task<AdminListPageDto<RoleDto>> GetRolesAsync(AdminListQuery query, CurrentUser currentUser, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WorkflowStartableDefinitionDto>> GetStartableWorkflowDefinitionsAsync(CurrentUser currentUser, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WorkflowTargetPersonSourceDto>> SearchWorkflowTargetPersonSourcesAsync(string? search, CurrentUser currentUser, int limit = 20, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WorkflowTargetPersonDto>> SearchWorkflowTargetPeopleAsync(string? search, CurrentUser currentUser, int limit = 20, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WorkflowTargetPersonDto>> SearchRotationEligiblePeopleAsync(string? search, CurrentUser currentUser, int limit = 20, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RequirementDto>> GetRequirementsAsync(string? workflowDefinitionKey, CancellationToken cancellationToken = default);
    Task<WorkflowConfigDto?> GetWorkflowConfigAsync(int? roleId, string? workflowDefinitionKey, CancellationToken cancellationToken = default);
}
