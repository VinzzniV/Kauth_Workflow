namespace API;

internal interface IRotationTaskGenerationService
{
    Task<IReadOnlyList<RotationGeneratedTaskDto>?> GetGeneratedTasksAsync(
        long planId,
        CurrentUser currentUser,
        CancellationToken cancellationToken = default);

    Task<RotationTaskRegenerationResultDto?> RegeneratePlanTasksAsync(
        long planId,
        CurrentUser currentUser,
        string reason = "manual_regenerate",
        CancellationToken cancellationToken = default);

    Task<RotationTaskRegenerationResultDto?> RegeneratePlanTasksUncheckedAsync(
        long planId,
        CurrentUser currentUser,
        string reason,
        CancellationToken cancellationToken = default);

    Task RegenerateDepartmentPlansAsync(
        int departmentId,
        CurrentUser currentUser,
        string reason,
        CancellationToken cancellationToken = default);
}
