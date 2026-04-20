namespace API;

internal interface IPersonLifecycleProjectionService
{
    Task ApplyCompletedWorkflowProjectionAsync(
        Guid workflowUid,
        long? actorUserId = null,
        CancellationToken cancellationToken = default);
}
