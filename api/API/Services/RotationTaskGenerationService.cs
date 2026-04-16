namespace API;

internal sealed class RotationTaskGenerationService(
    IRotationRepository rotationRepository,
    IWorkflowVisibilityService workflowVisibilityService) : IRotationTaskGenerationService
{
    public async Task<IReadOnlyList<RotationGeneratedTaskDto>?> GetGeneratedTasksAsync(
        long planId,
        CurrentUser currentUser,
        CancellationToken cancellationToken = default)
    {
        var plan = await rotationRepository.GetRotationPlan(planId);
        if (plan is null)
        {
            return null;
        }

        await EnsureVisibleAsync(plan.DepartmentId, currentUser);
        return await rotationRepository.GetRotationGeneratedTasks(planId);
    }

    public async Task<RotationTaskRegenerationResultDto?> RegeneratePlanTasksAsync(
        long planId,
        CurrentUser currentUser,
        string reason = "manual_regenerate",
        CancellationToken cancellationToken = default)
    {
        var plan = await rotationRepository.GetRotationPlan(planId);
        if (plan is null)
        {
            return null;
        }

        await EnsureVisibleAsync(plan.DepartmentId, currentUser);
        return await rotationRepository.SynchronizeRotationGeneratedTasks(planId, currentUser.UserId, reason);
    }

    public async Task<RotationTaskRegenerationResultDto?> RegeneratePlanTasksUncheckedAsync(
        long planId,
        CurrentUser currentUser,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var plan = await rotationRepository.GetRotationPlan(planId);
        if (plan is null)
        {
            return null;
        }

        return await rotationRepository.SynchronizeRotationGeneratedTasks(planId, currentUser.UserId, reason);
    }

    public async Task RegenerateDepartmentPlansAsync(
        int departmentId,
        CurrentUser currentUser,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var planIds = await rotationRepository.GetRotationPlanIdsForDepartment(departmentId);
        foreach (var planId in planIds)
        {
            await rotationRepository.SynchronizeRotationGeneratedTasks(planId, currentUser.UserId, reason);
        }
    }

    private async Task EnsureVisibleAsync(int? departmentId, CurrentUser currentUser)
    {
        var observableDepartmentIds = await workflowVisibilityService.GetObservableWorkflowDepartmentIds(currentUser);
        if (observableDepartmentIds is null)
        {
            return;
        }

        if (!departmentId.HasValue || !observableDepartmentIds.Contains(departmentId.Value))
        {
            throw new UnauthorizedAccessException("Der Durchlaufplan liegt außerhalb Ihrer freigegebenen Abteilungen.");
        }
    }
}
