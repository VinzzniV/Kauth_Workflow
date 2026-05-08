namespace API;

internal sealed class WorkflowCatalogService(
    IWorkflowRepository repository,
    IAuthorizationPolicyService authorizationPolicyService,
    IWorkflowVisibilityService workflowVisibilityService) : IWorkflowCatalogService
{
    public async Task<IReadOnlyList<WorkflowStartableDefinitionDto>> GetStartableWorkflowDefinitionsAsync(
        CurrentUser currentUser,
        CancellationToken cancellationToken = default)
    {
        var definitions = await repository.GetStartableWorkflowDefinitions();
        var canCreateAny = authorizationPolicyService.HasAnyRole(
            currentUser,
            AuthorizationRoles.Hr,
            AuthorizationRoles.Admin);

        if (canCreateAny)
        {
            return definitions;
        }

        var isManager = authorizationPolicyService.HasAnyRole(currentUser, AuthorizationRoles.Manager);
        IReadOnlySet<string>? managerCreatableKeys = null;

        var result = new List<WorkflowStartableDefinitionDto>();
        foreach (var definition in definitions)
        {
            if (authorizationPolicyService.HasPermission(
                    currentUser,
                    AuthorizationPermissions.WorkflowCreate(definition.DefinitionKey)))
            {
                result.Add(definition);
                continue;
            }

            if (!isManager)
            {
                continue;
            }

            managerCreatableKeys ??= await repository.GetManagerCreatableDefinitionKeys();
            if (managerCreatableKeys.Contains(definition.DefinitionKey))
            {
                result.Add(definition);
            }
        }

        return result;
    }

    public async Task<AdminListPageDto<DepartmentDto>> GetDepartmentsAsync(AdminListQuery query, CancellationToken cancellationToken = default)
    {
        return await repository.GetDepartments(query);
    }

    public async Task<AdminListPageDto<RoleDto>> GetRolesAsync(AdminListQuery query, CurrentUser currentUser, CancellationToken cancellationToken = default)
    {
        return await repository.GetRoles(query);
    }

    public async Task<AdminListPageDto<PersonDirectoryItemDto>> GetPeopleDirectoryAsync(AdminListQuery query, CancellationToken cancellationToken = default)
    {
        return await repository.GetPeopleDirectory(query);
    }

    public async Task<IReadOnlyList<WorkflowTargetPersonSourceDto>> SearchWorkflowTargetPersonSourcesAsync(
        string? search,
        CurrentUser currentUser,
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var observableDepartmentIds = await workflowVisibilityService.GetObservableWorkflowDepartmentIds(currentUser);
        return await repository.SearchWorkflowTargetPersonSources(search, limit, observableDepartmentIds);
    }

    public async Task<IReadOnlyList<WorkflowTargetPersonDto>> SearchWorkflowTargetPeopleAsync(
        string? search,
        CurrentUser currentUser,
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var observableDepartmentIds = await workflowVisibilityService.GetObservableWorkflowDepartmentIds(currentUser);
        return await repository.SearchWorkflowTargetPeople(search, limit, observableDepartmentIds);
    }

    public async Task<IReadOnlyList<WorkflowTargetPersonDto>> SearchRotationEligiblePeopleAsync(
        string? search,
        CurrentUser currentUser,
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var observableDepartmentIds = await workflowVisibilityService.GetObservableWorkflowDepartmentIds(currentUser);
        return await repository.SearchRotationEligiblePeople(search, limit, observableDepartmentIds);
    }

    public async Task<IReadOnlyList<RequirementDto>> GetRequirementsAsync(string? workflowDefinitionKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(workflowDefinitionKey))
        {
            throw new InvalidOperationException("workflowDefinitionKey is required.");
        }

        return await repository.GetRequirements(workflowDefinitionKey);
    }

    public async Task<WorkflowConfigDto?> GetWorkflowConfigAsync(int? roleId, string? workflowDefinitionKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(workflowDefinitionKey))
        {
            throw new InvalidOperationException("workflowDefinitionKey is required.");
        }

        return await repository.GetWorkflowConfig(roleId, workflowDefinitionKey);
    }

    public async Task<AdminListPageDto<UnlinkedDirectoryIdentityDto>> GetUnlinkedDirectoryIdentitiesAsync(
        string? departmentFilter,
        bool? onlyEnabled,
        int limit,
        int offset,
        CancellationToken cancellationToken = default)
    {
        return await repository.GetUnlinkedDirectoryIdentities(departmentFilter, onlyEnabled, limit, offset);
    }

    public async Task<ImportPeopleFromDirectoryResultDto> ImportPeopleFromDirectoryAsync(
        ImportPeopleFromDirectoryRequest request,
        long? actorUserId,
        CancellationToken cancellationToken = default)
    {
        return await repository.ImportPeopleFromDirectory(request.DirectoryIdentityIds, actorUserId);
    }
}
