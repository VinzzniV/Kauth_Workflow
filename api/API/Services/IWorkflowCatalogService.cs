namespace API;

internal interface IWorkflowCatalogService
{
    Task<AdminListPageDto<DepartmentDto>> GetDepartmentsAsync(AdminListQuery query, CancellationToken cancellationToken = default);
    Task<AdminListPageDto<RoleDto>> GetRolesAsync(AdminListQuery query, CurrentUser currentUser, CancellationToken cancellationToken = default);
    Task<AdminListPageDto<PersonDirectoryItemDto>> GetPeopleDirectoryAsync(AdminListQuery query, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WorkflowStartableDefinitionDto>> GetStartableWorkflowDefinitionsAsync(string? search, int? limit, CurrentUser currentUser, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WorkflowTargetPersonSourceDto>> SearchWorkflowTargetPersonSourcesAsync(string? search, CurrentUser currentUser, int limit = 20, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WorkflowTargetPersonDto>> SearchWorkflowTargetPeopleAsync(string? search, CurrentUser currentUser, int limit = 20, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WorkflowTargetPersonDto>> SearchRotationEligiblePeopleAsync(string? search, CurrentUser currentUser, int limit = 20, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RequirementDto>> GetRequirementsAsync(string? workflowDefinitionKey, CancellationToken cancellationToken = default);
    Task<WorkflowConfigDto?> GetWorkflowConfigAsync(int? roleId, string? workflowDefinitionKey, CancellationToken cancellationToken = default);
    // A1: Entra-Identitaeten ohne people-Record — Basis fuer retroaktiven Import.
    Task<AdminListPageDto<UnlinkedDirectoryIdentityDto>> GetUnlinkedDirectoryIdentitiesAsync(
        string? departmentFilter,
        bool? onlyEnabled,
        int limit,
        int offset,
        CancellationToken cancellationToken = default);
    Task<ImportPeopleFromDirectoryResultDto> ImportPeopleFromDirectoryAsync(
        ImportPeopleFromDirectoryRequest request,
        long? actorUserId,
        CancellationToken cancellationToken = default);
    // A3: Inline-Bearbeitung fehlender Stammdaten auf der Mitarbeiterkarte.
    Task<bool> UpdatePersonAsync(long personId, UpdatePersonRequest request, CancellationToken cancellationToken = default);
}
