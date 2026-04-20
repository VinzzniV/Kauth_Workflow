using Microsoft.Extensions.Logging;

namespace API;

internal sealed class PersonLifecycleProjectionService(
    IWorkflowRepository repository,
    ILogger<PersonLifecycleProjectionService> logger) : IPersonLifecycleProjectionService
{
    public async Task ApplyCompletedWorkflowProjectionAsync(
        Guid workflowUid,
        long? actorUserId = null,
        CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        await repository.ApplyPersonLifecycleProjection(workflowUid, actorUserId);
        logger.LogDebug(
            "Applied person lifecycle projection for workflow {WorkflowUid} by actor {ActorUserId}.",
            workflowUid,
            actorUserId);
    }
}
