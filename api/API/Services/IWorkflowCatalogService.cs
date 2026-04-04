namespace API;

internal interface IWorkflowCatalogService
{
    Task<IReadOnlyList<DepartmentDto>> GetDepartmentsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RoleDto>> GetRolesAsync(CurrentUser currentUser, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WorkflowProcessTypeDto>> GetProcessTypesAsync(CurrentUser currentUser, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CompletedOnboardingSearchResultDto>> SearchCompletedOnboardingsAsync(string? search, CurrentUser currentUser, int limit = 20, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WorkflowTargetPersonDto>> SearchWorkflowTargetPeopleAsync(string? search, CurrentUser currentUser, int limit = 20, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RequirementDto>> GetRequirementsAsync(string? processTypeKey, CancellationToken cancellationToken = default);
    Task<WorkflowConfigDto?> GetWorkflowConfigAsync(int? roleId, string? processTypeKey, CancellationToken cancellationToken = default);
}
