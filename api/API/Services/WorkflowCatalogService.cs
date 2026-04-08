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

            var managerCreatable = await repository.IsManagerCreatableProcessType(definition.PrimaryLegacyProcessTypeKey);
            if (managerCreatable && authorizationPolicyService.HasAnyRole(currentUser, AuthorizationRoles.Manager))
            {
                result.Add(definition);
            }
        }

        return result;
    }

    public async Task<IReadOnlyList<DepartmentDto>> GetDepartmentsAsync(CancellationToken cancellationToken = default)
    {
        return await repository.GetDepartments();
    }

    public async Task<IReadOnlyList<RoleDto>> GetRolesAsync(CurrentUser currentUser, CancellationToken cancellationToken = default)
    {
        return await repository.GetRoles();
    }

    public async Task<IReadOnlyList<WorkflowProcessTypeDto>> GetProcessTypesAsync(CurrentUser currentUser, CancellationToken cancellationToken = default)
    {
        var managerOnly = authorizationPolicyService.HasAnyRole(currentUser, AuthorizationRoles.Manager)
            && !authorizationPolicyService.HasAnyRole(currentUser, AuthorizationRoles.Hr, AuthorizationRoles.Admin);

        return await repository.GetActiveProcessTypes(managerOnly);
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

    public async Task<IReadOnlyList<RequirementDto>> GetRequirementsAsync(string? processTypeKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(processTypeKey))
        {
            throw new InvalidOperationException("processTypeKey is required.");
        }

        return await repository.GetRequirements(processTypeKey);
    }

    public async Task<WorkflowConfigDto?> GetWorkflowConfigAsync(int? roleId, string? processTypeKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(processTypeKey))
        {
            throw new InvalidOperationException("processTypeKey is required.");
        }

        return await repository.GetWorkflowConfig(roleId, processTypeKey);
    }
}
