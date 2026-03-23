namespace API;

// Fachservice fuer den Schritt der Abteilungsleitung zwischen Workflow-Erstellung und Aufgabenabarbeitung.
internal sealed class PostgresSupervisorStepService : ISupervisorStepService
{
    private readonly IWorkflowRepository _workflowRepository;
    private readonly IAuthorizationPolicyService _authorizationPolicy;

    public PostgresSupervisorStepService(
        IWorkflowRepository workflowRepository,
        IAuthorizationPolicyService authorizationPolicy)
    {
        _workflowRepository = workflowRepository;
        _authorizationPolicy = authorizationPolicy;
    }

    // Zeigt nur die Faelle an, die dem aktuellen Benutzer in diesem Prozessschritt wirklich zugeordnet sind.
    public async Task<List<WorkflowListItemDto>> GetAssignedWorkflows(CurrentUser currentUser)
    {
        var workflows = await _workflowRepository.GetWorkflows();
        var assignedDepartmentIds = await GetAssignedDepartmentIds(currentUser);

        return workflows
            .Where(workflow => _authorizationPolicy.CanAccessAssignedSupervisorWorkflow(
                currentUser,
                workflow.DepartmentId,
                workflow.WorkflowStatus,
                assignedDepartmentIds))
            .OrderByDescending(workflow => workflow.CreatedAt)
            .ToList();
    }

    // Laedt die Antworten eines Falls nur dann, wenn der Benutzer fuer diesen Schritt der Abteilungsleitung zustaendig ist.
    public async Task<List<WorkflowRequirementSnapshotDto>?> GetRequirements(
        Guid workflowUid,
        CurrentUser currentUser)
    {
        var workflow = await _workflowRepository.GetWorkflowByUid(workflowUid);
        if (workflow is null)
        {
            return null;
        }

        await EnsureSupervisorAccess(currentUser, workflow.DepartmentId, workflow.WorkflowStatus);

        return workflow.Requirements
            .OrderBy(requirement => requirement.SortOrder)
            .ThenBy(requirement => requirement.Id)
            .ToList();
    }

    // Das Speichern delegiert die eigentliche Persistenz an das Workflow-Repository.
    public async Task<WorkflowDetailDto?> UpdateRequirements(
        Guid workflowUid,
        IReadOnlyList<RequirementSelectionInputDto> selections,
        CurrentUser currentUser)
    {
        var workflow = await _workflowRepository.GetWorkflowByUid(workflowUid);
        if (workflow is null)
        {
            return null;
        }

        await EnsureSupervisorAccess(currentUser, workflow.DepartmentId, workflow.WorkflowStatus);
        return await _workflowRepository.CompleteSupervisorStep(workflowUid, selections, currentUser.UserId);
    }

    // Die Zustaendigkeit wird aus Rollen und den zugewiesenen Abteilungen abgeleitet.
    private async Task<HashSet<int>> GetAssignedDepartmentIds(CurrentUser currentUser)
    {
        if (_authorizationPolicy.CanManageAdminConfiguration(currentUser))
        {
            return new HashSet<int>();
        }

        if (!_authorizationPolicy.CanAccessSupervisorStep(currentUser))
        {
            return new HashSet<int>();
        }

        return await _workflowRepository.GetRequirementSelectionDepartmentIds(currentUser.UserId);
    }

    private bool IsAssignedWorkflow(CurrentUser currentUser, int workflowDepartmentId, HashSet<int> assignedDepartmentIds)
    {
        return _authorizationPolicy.CanManageAdminConfiguration(currentUser)
               || (_authorizationPolicy.CanAccessSupervisorStep(currentUser)
                   && assignedDepartmentIds.Contains(workflowDepartmentId));
    }

    // Dieser Schritt darf nur waehrend "waiting_for_supervisor" bearbeitet werden.
    private async Task EnsureSupervisorAccess(CurrentUser currentUser, int workflowDepartmentId, string workflowStatus)
    {
        var assignedDepartmentIds = await GetAssignedDepartmentIds(currentUser);
        if (!IsAssignedWorkflow(currentUser, workflowDepartmentId, assignedDepartmentIds))
        {
            throw new UnauthorizedAccessException("Workflow ist nicht der zuständigen Abteilungsleitung zugeordnet.");
        }

        if (!WorkflowStatusRules.IsWaitingForSupervisor(workflowStatus))
        {
            throw new InvalidOperationException("Workflow befindet sich nicht mehr im Schritt der Abteilungsleitung.");
        }
    }
}
