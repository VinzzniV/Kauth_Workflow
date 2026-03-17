namespace API;

internal interface ISupervisorStepService
{
    Task<List<WorkflowListItemDto>> GetAssignedWorkflows(CurrentUser currentUser);
    Task<List<WorkflowRequirementSnapshotDto>?> GetRequirements(Guid workflowUid, CurrentUser currentUser);
    Task<WorkflowDetailDto?> UpdateRequirements(
        Guid workflowUid,
        IReadOnlyList<RequirementSelectionInputDto> selections,
        CurrentUser currentUser);
}
